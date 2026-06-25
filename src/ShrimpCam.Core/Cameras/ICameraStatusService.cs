namespace ShrimpCam.Core.Cameras;

public interface ICameraStatusService
{
    CameraStatusSnapshot GetSnapshot();

    void ReportOnline();

    void ReportDegraded(string reason);
}
