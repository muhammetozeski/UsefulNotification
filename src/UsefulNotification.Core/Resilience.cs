using Polly;
using Polly.Retry;
using Polly.Timeout;
namespace UsefulNotification;

/// <summary>Executes isolated asynchronous operations using shared Polly strategy objects.</summary>
internal static class Resilience
{
    /// <summary>Applies retry and timeout policies without allowing late completion to change a returned result.</summary>
    /// <typeparam name="T">Operation result type.</typeparam>
    /// <param name="operation">An isolated operation that honors its token and does not commit shared state.</param>
    /// <param name="cancellationToken">The caller's cancellation request.</param>
    /// <param name="retry">Optional complete retry strategy; defaults to the application's policy object.</param>
    /// <param name="timeout">Optional complete timeout strategy; defaults to the application's policy object.</param>
    /// <returns>The completed operation result; failures reach the outer exception boundary.</returns>
    public static async Task<T> ExecuteResilientlyAsync<T>(Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default, RetryStrategyOptions? retry = null, TimeoutStrategyOptions? timeout = null)
    {
        var pipeline = new ResiliencePipelineBuilder().AddRetry(retry ?? AppConstants.Retry).AddTimeout(timeout ?? AppConstants.Timeout).Build();
        return await pipeline.ExecuteAsync(async token =>
        {
            Task<T> pending = operation(token);
            try { return await pending.WaitAsync(token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                _ = pending.ContinueWith(static completed =>
                {
                    if (completed.Exception is { } error) ExceptionBoundary.LogSafely(error.ToString());
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                throw;
            }
        }, cancellationToken);
    }
}
