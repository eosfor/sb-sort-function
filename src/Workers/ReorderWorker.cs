using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using src.Services;
using src.Settings;

namespace src.Workers;

/// <summary>
/// Background worker that sequentially accepts sessions from the input subscription
/// and processes them via ReorderProcessor. The first operation on the client is
/// the session receive, enabling send-via for cross-entity transactions.
/// </summary>
public class ReorderWorker : IHostedService
{
    private readonly ServiceBusClient _client;
    private readonly ReorderProcessor _processor;
    private readonly ServiceBusOptions _options;
    private readonly ILogger<ReorderWorker> _logger;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public ReorderWorker(ServiceBusClient client, ReorderProcessor processor, IOptions<ServiceBusOptions> options, ILogger<ReorderWorker> logger)
    {
        _client = client;
        _processor = processor;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = Task.Run(ProcessLoopAsync);
        _logger.LogInformation("ReorderWorker started");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts != null)
        {
            _cts.Cancel();
        }
        if (_loop != null)
        {
            try { await _loop; } catch (OperationCanceledException) { }
        }
        _logger.LogInformation("ReorderWorker stopped");
    }

    private async Task ProcessLoopAsync()
    {
        if (_cts == null) return;
        var ct = _cts.Token;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var receiver = await _client.AcceptNextSessionAsync(
                    _options.InputTopic,
                    _options.InputSubscription,
                    new ServiceBusSessionReceiverOptions(),
                    ct);

                if (receiver == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                    continue;
                }

                _logger.LogInformation("Accepted session {SessionId}", receiver.SessionId);
                var accessors = ReorderProcessor.SessionStateAccessors.FromSessionReceiver(receiver, _options.UseTransactions);

                while (!ct.IsCancellationRequested)
                {
                    var msg = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), ct);
                    if (msg == null)
                    {
                        break;
                    }

                    await _processor.ProcessAsync(msg, accessors, ct);
                }
            }
            catch (TaskCanceledException)
            {
                // graceful shutdown
                break;
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceBusy)
            {
                _logger.LogWarning("Service busy; retrying in 2s: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in processing loop; retry in 2s");
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }
}
