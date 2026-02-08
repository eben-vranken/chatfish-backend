namespace BackEnd.Util;

using BackEnd.Services;

public class StoryMessagePollingWorker : BackgroundService
{
    private readonly StoryMessageService _service;

    public StoryMessagePollingWorker(StoryMessageService service)
    {
        _service = service;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine($"[StoryMessagePollingWorker] {DateTime.Now}: Worker starting...");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _service.ProcessDueMessagesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StoryMessagePollingWorker] {DateTime.Now}: Error processing messages: {ex.Message}");
            }
            
            await Task.Delay(10000, stoppingToken);
        }

        Console.WriteLine($"[StoryMessagePollingWorker] {DateTime.Now}: Worker stopping...");
    }
}