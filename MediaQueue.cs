using System.Collections.Concurrent;
using FluentResults;
using TNRD.Zeepkist.GTR.DTOs.Rabbit;

namespace TNRD.Zeepkist.GTR.Backend.RecordMediaHandler;

internal class MediaQueue
{
    private readonly AutoResetEvent resetEvent = new(true);
    private readonly ConcurrentQueue<UploadRecordMediaRequest> items = new();

    public bool HasItems()
    {
        return !items.IsEmpty;
    }

    public void AddToQueue(UploadRecordMediaRequest item)
    {
        resetEvent.WaitOne();
        try
        {
            items.Enqueue(item);
        }
        finally
        {
            resetEvent.Set();
        }
    }

    public Result<UploadRecordMediaRequest> GetItemFromQueue()
    {
        resetEvent.WaitOne();
        try
        {
            Result<UploadRecordMediaRequest> result = items.TryDequeue(out UploadRecordMediaRequest? item)
                ? Result.Ok(item)
                : Result.Fail("Failed to dequeue");

            return result;
        }
        finally
        {
            resetEvent.Set();
        }
    }
}
