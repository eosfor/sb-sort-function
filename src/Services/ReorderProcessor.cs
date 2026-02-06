using System.Text.Json;
using System.Linq;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using src.Settings;

namespace src.Services;

/// <summary>
/// Reorders out-of-order messages from NO_SESSION/STATE_SUB by persisting ordering
/// state in the same subscription's session state and forwarding ordered
/// messages to ORDERED_TOPIC/SESS_SUB under the same SessionId.
/// </summary>
public class ReorderProcessor
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusOptions _options;
    private readonly ILogger<ReorderProcessor> _logger;
    private readonly bool _transactionsSupported;

    public ReorderProcessor(ServiceBusClient client, IOptions<ServiceBusOptions> options, ILogger<ReorderProcessor> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
        var conn = (_options.Connection ?? string.Empty).ToLowerInvariant();
        var isEmulator = conn.Contains("usedevelopmentemulator=true");
        _transactionsSupported = _options.UseTransactions && !isEmulator;
    }

    private record OrderSeq(int Order, long SeqNumber);

    private class SessionState
    {
        public int LastSeenOrder { get; set; }
        public List<OrderSeq> Deferred { get; set; } = new();
        public static SessionState Empty => new() { LastSeenOrder = 0, Deferred = new() };
    }

    public record SessionStateAccessors(
        string SessionId,
        Func<CancellationToken, Task<BinaryData>> GetAsync,
        Func<BinaryData, CancellationToken, Task> SetAsync,
        Func<ServiceBusReceivedMessage, CancellationToken, Task> CompleteAsync,
        Func<ServiceBusReceivedMessage, CancellationToken, Task> DeferAsync,
        Func<ServiceBusReceivedMessage, string, string, CancellationToken, Task> DeadLetterAsync,
        Func<long, CancellationToken, Task<ServiceBusReceivedMessage?>> ReceiveDeferredAsync,
        bool SupportsTransactions)
    {
        public static SessionStateAccessors FromSessionReceiver(ServiceBusSessionReceiver receiver, bool supportsTransactions = true) =>
            new(receiver.SessionId,
                ct => receiver.GetSessionStateAsync(ct),
                (data, ct) => receiver.SetSessionStateAsync(data, ct),
                (msg, ct) => receiver.CompleteMessageAsync(msg, ct),
                (msg, ct) => receiver.DeferMessageAsync(msg, cancellationToken: ct),
                (msg, reason, description, ct) => receiver.DeadLetterMessageAsync(msg, deadLetterReason: reason, deadLetterErrorDescription: description, cancellationToken: ct),
                (seq, ct) => receiver.ReceiveDeferredMessageAsync(seq, cancellationToken: ct),
                supportsTransactions);
    }

    private record Envelope(string sessionId, int order);

    public async Task ProcessAsync(ServiceBusReceivedMessage message, SessionStateAccessors stateAccessors, CancellationToken ct = default)
    {
        var envelope = ParseEnvelope(message);
        var sessionId = message.SessionId ?? envelope.sessionId;
        var order = envelope.order;

        _logger.LogInformation("Received seq={Seq} sessionId={SessionId} order={Order} lockedUntil={Lock}",
            message.SequenceNumber, sessionId, order, message.LockedUntil.ToUniversalTime());

        var state = await LoadStateAsync(stateAccessors, ct);
        var expected = state.LastSeenOrder + 1;

        if (state.LastSeenOrder == 0)
        {
            await HandleFirstAsync(message, sessionId, order, state, stateAccessors, ct);
            return;
        }

        if (order == expected)
        {
            await HandleInOrderAsync(message, sessionId, order, state, stateAccessors, ct);
            return;
        }

        await HandleOutOfOrderAsync(message, sessionId, order, expected, state, stateAccessors, ct);
    }

    private async Task HandleFirstAsync(ServiceBusReceivedMessage msg, string sessionId, int order,
        SessionState state, SessionStateAccessors accessors, CancellationToken ct)
    {
        await SaveStateWithWorkAsync(accessors, state, ct,
            async () =>
            {
                await ForwardAndCompleteAsync(msg, sessionId, accessors, ct);
                state.LastSeenOrder = order;
            });
        _logger.LogInformation("First message committed seq={Seq} order={Order}", msg.SequenceNumber, order);
    }

    private async Task HandleInOrderAsync(ServiceBusReceivedMessage msg, string sessionId, int order,
        SessionState state, SessionStateAccessors accessors, CancellationToken ct)
    {
        await SaveStateWithWorkAsync(accessors, state, ct,
            async () =>
            {
                await ForwardAndCompleteAsync(msg, sessionId, accessors, ct);
                state.LastSeenOrder = order;
                await DrainDeferredAsync(state, sessionId, accessors, ct);
            });
        _logger.LogInformation("In-order committed seq={Seq} order={Order} deferredLeft={Deferred}",
            msg.SequenceNumber, order, state.Deferred.Count);
    }

    private async Task HandleOutOfOrderAsync(ServiceBusReceivedMessage msg, string sessionId, int order,
        int expected, SessionState state, SessionStateAccessors accessors, CancellationToken ct)
    {
        if (order < expected)
        {
            _logger.LogWarning("Order too small, dead-lettering seq={Seq} order={Order} expected>={Expected}",
                msg.SequenceNumber, order, expected);
            await DeadLetterInputAsync(msg, accessors, ct);
            return;
        }

        await SaveStateWithWorkAsync(accessors, state, ct,
            async () =>
            {
                state.Deferred.Add(new(order, msg.SequenceNumber));
                await DeferInputAsync(msg, accessors, ct);
            });
        _logger.LogInformation("Deferred seq={Seq} order={Order} expected={Expected} deferredCount={Deferred}",
            msg.SequenceNumber, order, expected, state.Deferred.Count);
    }

    private async Task DrainDeferredAsync(SessionState state, string sessionId, SessionStateAccessors accessors, CancellationToken ct)
    {
        if (state.Deferred.Count == 0)
        {
            return;
        }

        await using var orderedSender = CreateOrderedSender();

        var ordered = state.Deferred.OrderBy(d => d.Order).ToList();
        var expected = state.LastSeenOrder + 1;
        var remaining = new List<OrderSeq>();

        foreach (var entry in ordered)
        {
            if (entry.Order != expected)
            {
                remaining.Add(entry);
                continue;
            }

            var deferred = await accessors.ReceiveDeferredAsync(entry.SeqNumber, ct);
            if (deferred == null)
            {
                _logger.LogWarning("Deferred seq={Seq} order={Order} not found (expired/locked)", entry.SeqNumber, entry.Order);
                remaining.Add(entry);
                continue;
            }

            await ForwardAndCompleteAsync(orderedSender, deferred, sessionId, accessors, ct);
            state.LastSeenOrder = entry.Order;
            expected++;
        }

        state.Deferred.Clear();
        state.Deferred.AddRange(remaining);
    }

    private async Task SaveStateAsync(SessionStateAccessors accessors, SessionState state, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(state);
        await accessors.SetAsync(BinaryData.FromString(json), ct);
        _logger.LogInformation("State saved session={Session} lastSeen={LastSeen} deferred={Deferred}",
            accessors.SessionId, state.LastSeenOrder, state.Deferred.Count);
    }

    private async Task SaveStateWithWorkAsync(SessionStateAccessors accessors, SessionState state, CancellationToken ct, Func<Task> work)
    {
        if (_transactionsSupported && accessors.SupportsTransactions)
        {
            using var scope = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeAsyncFlowOption.Enabled);
            await work();
            await SaveStateAsync(accessors, state, ct);
            scope.Complete();
        }
        else
        {
            await work();
            await SaveStateAsync(accessors, state, ct);
        }
    }

    private static async Task<SessionState> LoadStateAsync(SessionStateAccessors accessors, CancellationToken ct)
    {
        var data = await accessors.GetAsync(ct);
        if (data == null || data.ToMemory().Length == 0)
        {
            return SessionState.Empty;
        }

        try
        {
            return data.ToObjectFromJson<SessionState>() ?? SessionState.Empty;
        }
        catch
        {
            return SessionState.Empty;
        }
    }

    private async Task ForwardAndCompleteAsync(ServiceBusReceivedMessage msg, string sessionId, SessionStateAccessors accessors, CancellationToken ct)
    {
        await using var sender = CreateOrderedSender();
        await ForwardAndCompleteAsync(sender, msg, sessionId, accessors, ct);
    }

    private async Task ForwardAndCompleteAsync(ServiceBusSender sender, ServiceBusReceivedMessage msg, string sessionId, SessionStateAccessors accessors, CancellationToken ct)
    {
        var outMsg = CloneForOrdered(msg, sessionId);
        await sender.SendMessageAsync(outMsg, ct);
        await accessors.CompleteAsync(msg, ct);
        _logger.LogInformation("Forwarded+completed seq={Seq} order={Order} -> ordered topic", msg.SequenceNumber, GetOrderFromBody(msg));
    }

    private ServiceBusSender CreateOrderedSender()
    {
        return _client.CreateSender(_options.OrderedTopic);
    }

    private static ServiceBusMessage CloneForOrdered(ServiceBusReceivedMessage msg, string sessionId)
    {
        var order = GetOrderFromBody(msg);
        var clone = new ServiceBusMessage(msg.Body)
        {
            ContentType = msg.ContentType,
            CorrelationId = msg.CorrelationId,
            Subject = msg.Subject,
            MessageId = msg.MessageId,
            SessionId = sessionId
        };
        clone.ApplicationProperties["order"] = order;
        return clone;
    }

    private Task DeferInputAsync(ServiceBusReceivedMessage msg, SessionStateAccessors accessors, CancellationToken ct) =>
        accessors.DeferAsync(msg, ct);

    private Task DeadLetterInputAsync(ServiceBusReceivedMessage msg, SessionStateAccessors accessors, CancellationToken ct) =>
        accessors.DeadLetterAsync(msg, "Order lower than expected", "Monotonic ordering violation", ct);

    private static Envelope ParseEnvelope(ServiceBusReceivedMessage message)
    {
        try
        {
            return JsonSerializer.Deserialize<Envelope>(message.Body) ?? throw new InvalidOperationException("Envelope missing");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse message body", ex);
        }
    }

    private static int GetOrderFromBody(ServiceBusReceivedMessage message)
    {
        var env = ParseEnvelope(message);
        return env.order;
    }
}
