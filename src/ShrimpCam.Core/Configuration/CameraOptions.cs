using System.ComponentModel.DataAnnotations;

namespace ShrimpCam.Core.Configuration;

public sealed class CameraOptions
{
    [Required]
    [RegularExpression("Windows|Linux", ErrorMessage = "Camera platform must be Linux or Windows.")]
    public string Platform { get; init; } = "Windows";

    [Required]
    [MinLength(1)]
    public string Source { get; init; } = string.Empty;
}
