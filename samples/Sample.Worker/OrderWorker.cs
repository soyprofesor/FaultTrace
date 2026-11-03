using FaultTrace.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace Sample.Worker;

// Handles a few messages and stops. A real worker would read them from a queue such as Kafka.
public sealed class OrderWorker : BackgroundService
{
    private const int MaxAttempts = 3;

    private readonly IFaultTranslator _translator;
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<OrderWorker> _logger;

    public OrderWorker(
        IFaultTranslator translator,
        IConfiguration configuration,
        IHostApplicationLifetime lifetime,
        ILogger<OrderWorker> logger)
    {
        _translator = translator;
        _configuration = configuration;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failedOnce = false;

        await HandleAsync("works", () => { }, stoppingToken);
        await HandleAsync(
            "passes on the second attempt",
            () =>
            {
                if (!failedOnce)
                {
                    failedOnce = true;
                    throw new TimeoutException();
                }
            },
            stoppingToken);
        await HandleAsync("keeps timing out", () => throw new TimeoutException(), stoppingToken);
        await HandleAsync("has a bug", () => throw new InvalidOperationException("Internal details"), stoppingToken);

        var connectionString = _configuration.GetConnectionString("Oracle");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogInformation("Set ConnectionStrings__Oracle to run the Oracle example");
        }
        else
        {
            await HandleAsync("is rejected by the database", () => RaiseBusinessError(connectionString), stoppingToken);
        }

        _lifetime.StopApplication();
    }

    private async Task HandleAsync(string message, Action handle, CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                handle();
                _logger.LogInformation("Message that {Message} is done on attempt {Attempt}", message, attempt);
                return;
            }
            catch (Exception exception)
            {
                // The translator has already logged the details under this error ID.
                var fault = _translator.Translate(exception);

                if (!fault.IsTransient)
                {
                    _logger.LogWarning("Message that {Message} goes to the dead-letter queue: {ErrorId} {Code}", message, fault.ErrorId, fault.Code);
                    return;
                }

                _logger.LogInformation("Message that {Message} will be retried after {ErrorId} {Code}", message, fault.ErrorId, fault.Code);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), stoppingToken);
        }

        _logger.LogWarning("Message that {Message} goes to the dead-letter queue after {Attempts} attempts", message, MaxAttempts);
    }

    private static void RaiseBusinessError(string connectionString)
    {
        using var connection = new OracleConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "BEGIN RAISE_APPLICATION_ERROR(-20001, 'The limit is exceeded'); END;";
        command.ExecuteNonQuery();
    }
}
