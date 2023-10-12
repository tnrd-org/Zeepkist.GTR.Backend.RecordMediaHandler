using TNRD.Zeepkist.GTR.DTOs.Rabbit;

namespace TNRD.Zeepkist.GTR.Backend.RecordMediaHandler;

internal class Worker : BackgroundService
{
    private readonly ILogger<Worker> logger;
    private readonly ItemQueue itemQueue;
    private readonly IServiceProvider provider;
    private readonly SemaphoreSlim semaphore;

    public Worker(ILogger<Worker> logger, ItemQueue itemQueue, IServiceProvider provider)
    {
        this.logger = logger;
        this.itemQueue = itemQueue;
        this.provider = provider;

        semaphore = new SemaphoreSlim(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                List<UploadRecordMediaRequest> items = itemQueue.GetItemsFromQueue();
                List<Task> tasks = new();

                foreach (UploadRecordMediaRequest request in items)
                {
                    Task task = Task.Run(async () => { await StartJob(request, stoppingToken); }, stoppingToken);
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);

                if (!itemQueue.HasItems())
                {
                    logger.LogInformation("No more items in queue, waiting for new items");
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception e)
            {
                logger.LogCritical(e, "Error occurred while processing media upload");
            }
        }
    }

    private async Task StartJob(UploadRecordMediaRequest request, CancellationToken stoppingToken)
    {
        await semaphore.WaitAsync(stoppingToken);

        IServiceScope? scope = null;

        try
        {
            scope = provider.CreateScope();

            UploadMediaJob job =
                ActivatorUtilities.CreateInstance<UploadMediaJob>(scope.ServiceProvider, request);

            bool success = await job.Execute();

            if (!success)
            {
                logger.LogInformation("Failed to upload media for record {Id}", request.Id);
                itemQueue.AddToQueue(request);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while processing media upload");
        }
        finally
        {
            scope?.Dispose();
            semaphore.Release();
        }
    }
}
