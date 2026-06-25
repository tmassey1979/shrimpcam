namespace ShrimpCam.Core.Authentication;

public sealed record BootstrapAdministratorRequest(
    string UserName,
    string Password);
