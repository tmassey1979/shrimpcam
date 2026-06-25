using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Captures;

public interface IMotionHighlightService
{
    Task<MotionHighlightResult> EvaluateAsync(
        ShrimpCamOptions options,
        MotionHighlightEvent motionEvent,
        CancellationToken cancellationToken);
}
