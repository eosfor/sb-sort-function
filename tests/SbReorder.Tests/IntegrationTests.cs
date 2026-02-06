using System.Text.Json;
using System.Threading;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using src.Services;
using src.Settings;

namespace SbReorder.Tests;

public class IntegrationTests : IAsyncLifetime
{
    private readonly string? _conn;
    private readonly string? _skipReason;
    private ServiceBusClient? _procClient;
    private ServiceBusClient? _seedClient;
    private ServiceBusAdministrationClient? _admin;
    private readonly ServiceBusOptions _options = new();

    public IntegrationTests()
    {
        _conn = Environment.GetEnvironmentVariable("ServiceBusConnection");
        if (string.IsNullOrWhiteSpace(_conn))
        {
            _skipReason = "Set ServiceBusConnection to run integration tests (emulator, Standard, or Premium).";
            return;
        }

        _options.Connection = _conn;
        _options.UseTransactions = Environment.GetEnvironmentVariable("ServiceBusUseTransactions")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        var sbOptions = new ServiceBusClientOptions
        {
            EnableCrossEntityTransactions = _options.UseTransactions
        };
        _procClient = new ServiceBusClient(_conn, sbOptions);
        _seedClient = new ServiceBusClient(_conn, new ServiceBusClientOptions { EnableCrossEntityTransactions = false });
        _admin = new ServiceBusAdministrationClient(_conn);
    }

    public async Task InitializeAsync()
    {
        if (_skipReason is not null || _procClient is null || _admin is null)
        {
            return;
        }
        Console.WriteLine("[Init] Ensuring entities...");
        await EnsureEntitiesAsync();
        Console.WriteLine("[Init] Entities ready, purging...");
        await PurgeAsync("NO_SESSION", "NO_SESS_SUB");
        await PurgeSessionsAsync("NO_SESSION", "STATE_SUB");
        Console.WriteLine("[Init] Purge done");
    }

    public async Task DisposeAsync()
    {
        if (_procClient is not null) await _procClient.DisposeAsync();
        if (_seedClient is not null) await _seedClient.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Reorders_Shuffled_Stream()
    {
        if (_skipReason is not null)
        {
            Console.WriteLine($"SKIP: {_skipReason}");
            return;
        }
        var sessionId = $"it-session-1-{Guid.NewGuid():N}";
        var orderList = Enumerable.Range(1, 12).OrderBy(_ => Guid.NewGuid()).ToList();
        await SendInputMessagesAsync(orderList, sessionId);

        using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }));
        var processor = new ReorderProcessor(_procClient!, Options.Create(_options), loggerFactory.CreateLogger<ReorderProcessor>());
        await ProcessAllInputAsync(processor, sessionId);

        var ordered = await ReceiveOrderedAsync(sessionId);
        var dlq = await ReceiveDlqOrdersAsync();
        ordered.Should().BeInAscendingOrder();
        ordered.Concat(dlq).OrderBy(x => x).Should().Equal(orderList.OrderBy(x => x));
    }

    [Fact]
    public async Task LowerOrder_Goes_To_DLQ()
    {
        if (_skipReason is not null)
        {
            Console.WriteLine($"SKIP: {_skipReason}");
            return;
        }
        var sessionId = $"it-session-2-{Guid.NewGuid():N}";
        var orders = new[] { 1, 2, 1 }; // second '1' should dead-letter
        await SendInputMessagesAsync(orders, sessionId);

        using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }));
        var processor = new ReorderProcessor(_procClient!, Options.Create(_options), loggerFactory.CreateLogger<ReorderProcessor>());
        await ProcessAllInputAsync(processor, sessionId);

        var ordered = await ReceiveOrderedAsync(sessionId);
        ordered.Should().Equal(new[] { 1, 2 });

        var dlqOrders = await ReceiveDlqOrdersAsync();
        dlqOrders.Should().Contain(1);
    }

    private async Task SendInputMessagesAsync(IEnumerable<int> orders, string sessionId)
    {
        await using var sender = _seedClient!.CreateSender("NO_SESSION");
        foreach (var order in orders)
        {
            var payload = JsonSerializer.Serialize(new { sessionId, order });
            var msg = new ServiceBusMessage(payload)
            {
                ContentType = "application/json",
                MessageId = $"{sessionId}-{order}"
            };
            msg.SessionId = sessionId;
            msg.ApplicationProperties["order"] = order;
            await sender.SendMessageAsync(msg);
        }
    }

    private async Task ProcessAllInputAsync(ReorderProcessor processor, string sessionId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Console.WriteLine($"[Process] Accepting session {sessionId}");
        await using var receiver = await _procClient!.AcceptSessionAsync("NO_SESSION", "STATE_SUB", sessionId, new ServiceBusSessionReceiverOptions(), cts.Token);
        Console.WriteLine($"[Process] Accepted session {sessionId}");
        var accessors = ReorderProcessor.SessionStateAccessors.FromSessionReceiver(receiver, _options.UseTransactions);

        var empty = 0;
        var iter = 0;
        const int maxIter = 40;
        while (empty < 3 && iter < maxIter)
        {
            iter++;
            var msg = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(1));
            if (msg == null)
            {
                empty++;
                Console.WriteLine($"[Process] iter={iter} received=null empty={empty}");
                continue;
            }
            empty = 0;
            Console.WriteLine($"[Process] iter={iter} seq={msg.SequenceNumber} order={JsonSerializer.Deserialize<Envelope>(msg.Body)!.order}");
            await processor.ProcessAsync(msg, accessors);
        }
    }

    private async Task<List<int>> ReceiveOrderedAsync(string sessionId)
    {
        var results = new List<int>();
        await using var sessReceiver = await _seedClient!.AcceptSessionAsync("ORDERED_TOPIC", "SESS_SUB", sessionId);
        var empty = 0;
        while (empty < 3)
        {
            var msg = await sessReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2));
            if (msg == null)
            {
                empty++;
                continue;
            }
            empty = 0;
            results.Add(JsonSerializer.Deserialize<Envelope>(msg.Body)!.order);
            await sessReceiver.CompleteMessageAsync(msg);
        }
        return results;
    }

    private async Task<List<int>> ReceiveDlqOrdersAsync()
    {
        var list = new List<int>();
        await using var dlqReceiver = _seedClient!.CreateReceiver("NO_SESSION", "STATE_SUB", new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter
        });
        while (true)
        {
            var msg = await dlqReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(1));
            if (msg == null) break;
            list.Add(JsonSerializer.Deserialize<Envelope>(msg.Body)!.order);
            await dlqReceiver.CompleteMessageAsync(msg);
        }
        return list;
    }

    private async Task PurgeAsync(string topic, string subscription, bool isSession = false)
    {
        try
        {
            await using var receiver = _seedClient!.CreateReceiver(topic, subscription);
            while (true)
            {
                var msg = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(0.5));
                if (msg == null) break;
                await receiver.CompleteMessageAsync(msg);
            }
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException)
        {
            // Swallow for emulator readiness or missing entity; tests will fail later if critical
        }
    }

    private async Task PurgeSessionsAsync(string topic, string subscription)
    {
        try
        {
            while (true)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await using var session = await _seedClient!.AcceptNextSessionAsync(topic, subscription, new ServiceBusSessionReceiverOptions(), cts.Token);
                    if (session == null) break;
                    while (true)
                    {
                        var msg = await session.ReceiveMessageAsync(TimeSpan.FromSeconds(0.5));
                        if (msg == null) break;
                        await session.CompleteMessageAsync(msg);
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException)
        {
            // ignore missing/lock issues during cleanup
        }
    }

    private async Task EnsureEntitiesAsync()
    {
        if (_conn.Contains("usedevelopmentemulator=true", StringComparison.OrdinalIgnoreCase))
        {
            // Emulator already seeded via config.json; management endpoint may not be reachable from container.
            return;
        }

        // NO_SESSION topic + NO_SESS_SUB (no sessions)
        if (!await _admin.TopicExistsAsync("NO_SESSION"))
        {
            await _admin.CreateTopicAsync(new CreateTopicOptions("NO_SESSION"));
        }
        if (!await _admin.SubscriptionExistsAsync("NO_SESSION", "NO_SESS_SUB"))
        {
            var subOptions = new CreateSubscriptionOptions("NO_SESSION", "NO_SESS_SUB")
            {
                RequiresSession = false,
                LockDuration = TimeSpan.FromMinutes(1),
                DefaultMessageTimeToLive = TimeSpan.FromHours(1),
                MaxDeliveryCount = 10
            };
            await _admin.CreateSubscriptionAsync(subOptions);
        }

        // State subscription on input topic (session-enabled)
        if (!await _admin.SubscriptionExistsAsync("NO_SESSION", "STATE_SUB"))
        {
            var stateSub = new CreateSubscriptionOptions("NO_SESSION", "STATE_SUB")
            {
                RequiresSession = true,
                LockDuration = TimeSpan.FromMinutes(1),
                DefaultMessageTimeToLive = TimeSpan.FromHours(1),
                MaxDeliveryCount = 10
            };
            await _admin.CreateSubscriptionAsync(stateSub);
        }

        // ORDERED_TOPIC with session-enabled SESS_SUB and SYSTEM_SUB
        if (!await _admin.TopicExistsAsync("ORDERED_TOPIC"))
        {
            await _admin.CreateTopicAsync(new CreateTopicOptions("ORDERED_TOPIC"));
        }

        await EnsureSessionSubAsync("ORDERED_TOPIC", "SESS_SUB");
        await EnsureSessionSubAsync("ORDERED_TOPIC", "SYSTEM_SUB");
    }

    private async Task EnsureSessionSubAsync(string topic, string subscription)
    {
        if (await _admin.SubscriptionExistsAsync(topic, subscription))
        {
            return;
        }

        var subOptions = new CreateSubscriptionOptions(topic, subscription)
        {
            RequiresSession = true,
            LockDuration = TimeSpan.FromMinutes(1),
            DefaultMessageTimeToLive = TimeSpan.FromHours(1),
            MaxDeliveryCount = 10
        };
        await _admin.CreateSubscriptionAsync(subOptions);
    }

    private record Envelope(string sessionId, int order);
}
