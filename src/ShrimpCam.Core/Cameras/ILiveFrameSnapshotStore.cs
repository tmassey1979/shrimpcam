namespace ShrimpCam.Core.Cameras;

public interface ILiveFrameSnapshotStore
{
    ILiveFrameSnapshotRecorder CreateRecorder();

    Task<bool> TryWriteLatestFrameAsync(string outputPath, CancellationToken cancellationToken);
}

public interface ILiveFrameSnapshotRecorder : IDisposable
{
    void Observe(ReadOnlyMemory<byte> buffer);
}
