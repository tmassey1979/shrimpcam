namespace ShrimpCam.Core.Diagnostics;

public interface IDiagnosticsBundleService
{
    Task<DiagnosticsBundle> GenerateAsync(CancellationToken cancellationToken);
}
