namespace ShrimpCam.Core.Authentication;

public sealed record AuthenticationRequest(
    string UserName,
    string Password);
