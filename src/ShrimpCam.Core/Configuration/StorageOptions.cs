using System.ComponentModel.DataAnnotations;

namespace ShrimpCam.Core.Configuration;

public sealed class StorageOptions
{
    [Required]
    [MinLength(1)]
    public string ImageRootPath { get; init; } = string.Empty;

    [Range(1, 3650)]
    public int RetentionDays { get; init; } = 30;
}
