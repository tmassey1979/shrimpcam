namespace ShrimpCam.Core.Cameras;

public interface ICameraLiveStreamSession : IAsyncDisposable
{
    string ContentType { get; }

    Stream Content { get; }
}
