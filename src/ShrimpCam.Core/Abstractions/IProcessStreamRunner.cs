namespace ShrimpCam.Core.Abstractions;

public interface IProcessStreamRunner
{
    Task<IProcessStreamHandle> StartAsync(ProcessRequest request, CancellationToken cancellationToken);
}
