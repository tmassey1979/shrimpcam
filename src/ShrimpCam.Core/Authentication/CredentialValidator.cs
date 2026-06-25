namespace ShrimpCam.Core.Authentication;

public static class CredentialValidator
{
    public static bool IsValid(AuthenticationRequest request) =>
        request is not null
        && !string.IsNullOrWhiteSpace(request.UserName)
        && !string.IsNullOrWhiteSpace(request.Password);
}
