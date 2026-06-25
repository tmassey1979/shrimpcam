using System.ComponentModel.DataAnnotations;

namespace ShrimpCam.Core.Configuration;

public sealed class InitialAdministratorOptions
{
    public bool Enabled { get; init; } = true;

    [Required]
    [MinLength(1)]
    public string UserName { get; init; } = "admin";

    [Required]
    [MinLength(12)]
    public string Password { get; init; } = "AdminPass1234";
}
