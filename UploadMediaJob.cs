using FluentResults;
using Microsoft.EntityFrameworkCore;
using TNRD.Zeepkist.GTR.Backend.RecordMediaHandler.Google;
using TNRD.Zeepkist.GTR.Database;
using TNRD.Zeepkist.GTR.Database.Models;
using TNRD.Zeepkist.GTR.DTOs.Rabbit;

namespace TNRD.Zeepkist.GTR.Backend.RecordMediaHandler;

public class UploadMediaJob
{
    private readonly ILogger<UploadMediaJob> logger;
    private readonly IGoogleUploadService uploadService;
    private readonly UploadRecordMediaRequest request;
    private readonly GTRContext context;

    public UploadMediaJob(
        ILogger<UploadMediaJob> logger,
        IGoogleUploadService uploadService,
        UploadRecordMediaRequest request,
        GTRContext context
    )
    {
        this.logger = logger;
        this.uploadService = uploadService;
        this.request = request;
        this.context = context;
    }

    public async Task<bool> Execute()
    {
        Record? record = await context.Records.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id);

        if (record == null)
        {
            logger.LogCritical("Record {Record} not found", request.Id);
            return false;
        }

        string identifier = Guid.NewGuid().ToString();

        Task<Result<string>> uploadGhostTask =
            uploadService.UploadGhost(identifier, request.GhostData, CancellationToken.None);
        Task<Result<string>> uploadScreenshotTask =
            uploadService.UploadScreenshot(identifier, request.ScreenshotData, CancellationToken.None);

        await Task.WhenAll(uploadGhostTask, uploadScreenshotTask);

        if (uploadGhostTask.IsFaulted)
        {
            logger.LogCritical("Failed to upload ghost for record {Record}", request.Id);
            return false;
        }

        Result<string> uploadGhostResult = uploadGhostTask.Result;
        if (uploadGhostResult.IsFailed)
        {
            logger.LogCritical("Failed to upload ghost for record {Record}: {Result}", request.Id, uploadGhostResult);
            return false;
        }

        if (uploadScreenshotTask.IsFaulted)
        {
            logger.LogCritical("Failed to upload screenshot for record {Record}", request.Id);
            return false;
        }

        Result<string> uploadScreenshotResult = uploadScreenshotTask.Result;
        if (uploadScreenshotResult.IsFailed)
        {
            logger.LogCritical("Failed to upload screenshot for record {Record}: {Result}",
                request.Id,
                uploadScreenshotResult);
            return false;
        }

        context.Media.Add(new Media()
        {
            Record = request.Id,
            GhostUrl = uploadGhostResult.Value,
            ScreenshotUrl = uploadScreenshotResult.Value,
            DateCreated = DateTime.UtcNow
        });

        int savedChanges = await context.SaveChangesAsync();

        if (savedChanges != 1)
        {
            logger.LogWarning("No saved changes when uploading media for record {Record}", request.Id);
        }

        return true;
    }
}
