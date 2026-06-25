namespace ShrimpCam.Core.Cameras;

public sealed record CameraResourceLease(string Owner) : IAsyncDisposable
{
    private readonly Func<ValueTask>? _releaseAsync;
    private bool _disposed;

    public CameraResourceLease(string owner, Func<ValueTask> releaseAsync)
        : this(owner)
    {
        _releaseAsync = releaseAsync;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_releaseAsync is not null)
        {
            await _releaseAsync().ConfigureAwait(false);
        }
    }
}
