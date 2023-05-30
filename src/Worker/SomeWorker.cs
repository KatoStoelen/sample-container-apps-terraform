namespace Worker;

public class SomeWorker : BackgroundService
{
    private readonly ILogger<SomeWorker> _logger;

    public SomeWorker(ILogger<SomeWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);

            await Task.Delay(5000, stoppingToken);
        }
    }
}
