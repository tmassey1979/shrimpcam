using ShrimpCam.Core.Abstractions;

namespace ShrimpCam.Infrastructure.Time;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
