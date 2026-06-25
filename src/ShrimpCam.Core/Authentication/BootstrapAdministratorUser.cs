namespace ShrimpCam.Core.Authentication;

public sealed record BootstrapAdministratorUser(
    Guid UserId,
    string UserName,
    string RoleName);
