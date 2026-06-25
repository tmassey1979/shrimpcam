namespace ShrimpCam.Core.Abstractions;

public interface IProcessStreamHandle : IAsyncDisposable
{
    Stream StandardOutput { get; }

    Task<ProcessResult> WaitForExitAsync(CancellationToken cancellationToken);
}
