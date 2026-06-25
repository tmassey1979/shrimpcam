using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Tests.Configuration;

public sealed class ShrimpCamOptionsValidationTests
{
    [Fact]
    public void Valid_options_pass_data_annotations_validation()
    {
        var options = new ShrimpCamOptions
        {
            Camera = new CameraOptions { Platform = "Windows", Source = "Logitech C920" },
            Capture = new CaptureOptions { IntervalMinutes = 5, ActiveStartHourUtc = 8, ActiveEndHourUtc = 20 },
            Storage = new StorageOptions { ImageRootPath = "data/images", RetentionDays = 30 },
            Security = new SecurityOptions { HostMode = "InternetExposed" },
        };

        ValidateNested(options).Should().BeEmpty();
    }

    [Fact]
    public void Invalid_options_fail_data_annotations_validation()
    {
        var options = new ShrimpCamOptions
        {
            Camera = new CameraOptions { Platform = "Other", Source = string.Empty },
            Capture = new CaptureOptions { IntervalMinutes = 0, ActiveStartHourUtc = -1, ActiveEndHourUtc = 25 },
            Storage = new StorageOptions { ImageRootPath = string.Empty, RetentionDays = 0 },
            Security = new SecurityOptions { HostMode = "Unknown" },
        };

        var results = ValidateNested(options);

        results.Should().NotBeEmpty();
        results.SelectMany(result => result.MemberNames).Should().Contain(
            new[]
            {
                "Platform",
                "Source",
                "IntervalMinutes",
                "ActiveStartHourUtc",
                "ActiveEndHourUtc",
                "ImageRootPath",
                "RetentionDays",
                "HostMode",
            });
    }

    private static IReadOnlyList<ValidationResult> ValidateNested(ShrimpCamOptions options)
    {
        var results = new List<ValidationResult>();

        foreach (var property in typeof(ShrimpCamOptions).GetProperties())
        {
            var value = property.GetValue(options);
            Validator.TryValidateObject(value!, new ValidationContext(value!), results, validateAllProperties: true);
        }

        return results;
    }
}
