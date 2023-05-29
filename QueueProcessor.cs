using FluentResults;
using Microsoft.EntityFrameworkCore;
using TNRD.Zeepkist.GTR.Backend.RecordMediaHandler.Google;
using TNRD.Zeepkist.GTR.Backend.RecordMediaHandler.Rabbit;
using TNRD.Zeepkist.GTR.Database;
using TNRD.Zeepkist.GTR.Database.Models;
using TNRD.Zeepkist.GTR.DTOs.Rabbit;

namespace TNRD.Zeepkist.GTR.Backend.RecordMediaHandler;

internal class QueueProcessor : IHostedService
{
    private readonly MediaQueue mediaQueue;
    private readonly IGoogleUploadService uploadService;
    private readonly IServiceProvider serviceProvider;
    private readonly IRabbitPublisher publisher;
    private readonly ILogger<QueueProcessor> logger;

    private readonly CancellationTokenSource cts;
    private readonly List<IServiceScope> scopes;
    private readonly List<Task> tasks;

    public QueueProcessor(
        MediaQueue mediaQueue,
        IGoogleUploadService uploadService,
        IServiceProvider serviceProvider,
        ILogger<QueueProcessor> logger,
        IRabbitPublisher publisher
    )
    {
        this.mediaQueue = mediaQueue;
        this.uploadService = uploadService;
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        this.publisher = publisher;

        cts = new CancellationTokenSource();
        scopes = new List<IServiceScope>();
        tasks = new List<Task>();
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (int i = 0; i < 10; i++) // Amount should probably be an option
        {
            logger.LogInformation("Creating processor #{Index}", i + 1);
            IServiceScope scope = serviceProvider.CreateScope();
            scopes.Add(scope);

            tasks.Add(ProcessQueue(scope.ServiceProvider, cts.Token));
            await Task.Delay(250, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        cts.Cancel();

        await Task.WhenAll(tasks);

        foreach (IServiceScope serviceScope in scopes)
        {
            serviceScope.Dispose();
        }

        cts.Dispose();
    }

    private async Task ProcessQueue(IServiceProvider provider, CancellationToken ct)
    {
        Random rnd = new(Environment.TickCount);
        GTRContext context = provider.GetRequiredService<GTRContext>();

        while (!ct.IsCancellationRequested)
        {
            int ms = rnd.Next(1000, 2500);

            if (!mediaQueue.HasItems())
                goto DELAY;

            Result<UploadRecordMediaRequest> result = mediaQueue.GetItemFromQueue();
            if (result.IsFailed)
            {
                logger.LogWarning("Unable to get item from queue");
                goto DELAY;
            }

            UploadRecordMediaRequest media = result.Value;

            string identifier = Guid.NewGuid().ToString();

            Record? record = await context.Records.FirstOrDefaultAsync(x => x.Id == media.Id, CancellationToken.None);
            if (record == null)
            {
                logger.LogError("Unable to find record");
                goto DELAY;
            }

            Task<Result<string>> uploadGhost =
                uploadService.UploadGhost(identifier, media.GhostData, CancellationToken.None);
            Task<Result<string>> uploadScreenshot =
                uploadService.UploadScreenshot(identifier, media.ScreenshotData, CancellationToken.None);

            await Task.WhenAll(uploadGhost, uploadScreenshot);

            record.GhostUrl = uploadGhost.Result.Value;
            record.ScreenshotUrl = uploadScreenshot.Result.Value;

            await context.SaveChangesAsync(CancellationToken.None);

            publisher.Publish("records",
                new RecordId
                {
                    Id = record.Id
                });

            ms = rnd.Next(10, 100);

            DELAY:
            await Task.Delay(ms, ct);
        }
    }
}
