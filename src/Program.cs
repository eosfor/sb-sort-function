using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using src.Services;
using src.Settings;
using src.Workers;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Bind configuration to options
builder.Services.Configure<ServiceBusOptions>(builder.Configuration.GetSection("ServiceBus"));
builder.Services.PostConfigure<ServiceBusOptions>(opts =>
{
    opts.Connection ??= builder.Configuration.GetConnectionString("ServiceBus")
        ?? builder.Configuration["ServiceBusConnection"];
    if (!bool.TryParse(builder.Configuration["ServiceBus:UseTransactions"], out var cfgTx))
    {
        bool.TryParse(builder.Configuration["ServiceBusUseTransactions"], out cfgTx);
    }
    opts.UseTransactions = cfgTx;
});

// Service Bus client with cross-entity transactions enabled
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var connectionString = cfg.GetSection("ServiceBus")["Connection"]
                           ?? cfg.GetConnectionString("ServiceBus")
                           ?? cfg["ServiceBusConnection"]
                           ?? throw new InvalidOperationException("ServiceBus connection string is not configured.");
    var useTx = false;
    var sectionVal = cfg.GetSection("ServiceBus")["UseTransactions"];
    if (!string.IsNullOrEmpty(sectionVal) && bool.TryParse(sectionVal, out var parsed)) useTx = parsed;
    var envVal = cfg["ServiceBusUseTransactions"];
    if (!useTx && !string.IsNullOrEmpty(envVal) && bool.TryParse(envVal, out var envParsed)) useTx = envParsed;

    var options = new ServiceBusClientOptions
    {
        TransportType = ServiceBusTransportType.AmqpTcp,
        EnableCrossEntityTransactions = useTx
    };
    return new ServiceBusClient(connectionString, options);
});

builder.Services.AddSingleton<ReorderProcessor>();
builder.Services.AddHostedService<ReorderWorker>();

builder.Build().Run();
