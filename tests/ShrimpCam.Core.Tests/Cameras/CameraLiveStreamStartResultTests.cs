using ShrimpCam.Core.Cameras;

namespace ShrimpCam.Core.Tests.Cameras;

public sealed class CameraLiveStreamStartResultTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Success_factory_returns_successful_result_with_session()
    {
        var session = new StubCameraLiveStreamSession();

        try
        {
            var result = CameraLiveStreamStartResult.Success(session);

            result.Succeeded.Should().BeTrue();
            result.FailureReason.Should().BeNull();
            result.Session.Should().Be(session);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Failure_factory_returns_failed_result_without_session()
    {
        var result = CameraLiveStreamStartResult.Failure(CameraLiveStreamFailureReasons.CameraUnavailable);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(CameraLiveStreamFailureReasons.CameraUnavailable);
        result.Session.Should().BeNull();
    }

    private sealed class StubCameraLiveStreamSession : ICameraLiveStreamSession
    {
        public string ContentType => LiveStreamConstants.ContentType;

        public Stream Content { get; } = new MemoryStream();

        public ValueTask DisposeAsync() => Content.DisposeAsync();
    }
}
