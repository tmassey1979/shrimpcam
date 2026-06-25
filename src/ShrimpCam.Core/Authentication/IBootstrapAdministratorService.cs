namespace ShrimpCam.Core.Authentication;

public interface IBootstrapAdministratorService
{
    Task<BootstrapAdministratorResult> BootstrapAsync(
        BootstrapAdministratorRequest request,
        CancellationToken cancellationToken);
}
