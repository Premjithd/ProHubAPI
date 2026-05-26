namespace ServiceProviderAPI.Services;

/// <summary>
/// Singleton that ensures we never exceed Nominatim's 1 req/sec usage policy.
/// All callers await WaitAsync() before sending a request.
/// </summary>
public class NominatimThrottle
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private DateTime _lastRequest = DateTime.MinValue;
    private static readonly TimeSpan MinGap = TimeSpan.FromMilliseconds(1100);

    public async Task WaitAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var wait = MinGap - (DateTime.UtcNow - _lastRequest);
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct);
            _lastRequest = DateTime.UtcNow;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
