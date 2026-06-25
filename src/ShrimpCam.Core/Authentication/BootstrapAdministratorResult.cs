namespace ShrimpCam.Core.Authentication;

public sealed record BootstrapAdministratorResult(
    bool Succeeded,
    string? FailureReason,
    BootstrapAdministratorUser? User)
{
    public static BootstrapAdministratorResult Success(BootstrapAdministratorUser user) => new(true, null, user);

    public static BootstrapAdministratorResult Failure(string failureReason) => new(false, failureReason, null);
}
