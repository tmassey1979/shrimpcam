namespace ShrimpCam.Core.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
