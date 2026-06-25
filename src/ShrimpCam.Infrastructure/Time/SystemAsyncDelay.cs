using ShrimpCam.Core.Abstractions;

namespace ShrimpCam.Infrastructure.Time;

internal sealed class SystemAsyncDelay : IAsyncDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);
}
