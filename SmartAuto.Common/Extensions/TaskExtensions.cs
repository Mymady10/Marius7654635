namespace SmartAuto.Common.Extensions;

public static class TaskExtensions
{
    /// <summary>Fire and forget, logging any exceptions to the provided action.</summary>
    public static void FireAndForget(this Task task, Action<Exception>? onError = null)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception is not null)
                onError?.Invoke(t.Exception.GetBaseException());
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
