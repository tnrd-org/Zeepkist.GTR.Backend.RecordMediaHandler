using System.Collections.Concurrent;
using FluentResults;
using TNRD.Zeepkist.GTR.DTOs.Rabbit;

namespace TNRD.Zeepkist.GTR.Backend.RecordMediaHandler;

internal class ItemQueue
{
    private readonly AutoResetEvent resetEvent = new(true);
    private readonly List<UploadRecordMediaRequest> items = new();

    public bool HasItems()
    {
        resetEvent.WaitOne();

        try
        {
            return items.Count > 0;
        }
        finally
        {
            resetEvent.Reset();
        }
    }

    public void AddToQueue(UploadRecordMediaRequest item)
    {
        resetEvent.WaitOne();
        try
        {
            items.Add(item);
        }
        finally
        {
            resetEvent.Set();
        }
    }

    public List<UploadRecordMediaRequest> GetItemsFromQueue()
    {
        resetEvent.WaitOne();

        try
        {
            List<UploadRecordMediaRequest> copy = items.ToList();
            items.Clear();
            return copy;
        }
        finally
        {
            resetEvent.Set();
        }
    }
}
