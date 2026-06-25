using System.ComponentModel.DataAnnotations;

namespace ShrimpCam.Core.Configuration;

public sealed class SecurityOptions
{
    [Required]
    [RegularExpression(
        "LocalOnly|RemoteReady|InternetExposed",
        ErrorMessage = "Host mode must be LocalOnly, RemoteReady, or InternetExposed.")]
    public string HostMode { get; init; } = "InternetExposed";
}
