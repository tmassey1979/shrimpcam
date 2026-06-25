using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Tests.Configuration;

public sealed class ShrimpCamOptionsValidationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Default_options_match_expected_bootstrap_values()
    {
        var options = new ShrimpCamOptions();

        options.Camera.Platform.Should().Be("Windows");
        options.Camera.Source.Should().BeEmpty();
        options.Capture.IntervalMinutes.Should().Be(5);
        options.Capture.ActiveStartHourUtc.Should().Be(6);
        options.Capture.ActiveEndHourUtc.Should().Be(22);
        options.Storage.ImageRootPath.Should().BeEmpty();
        options.Storage.RetentionDays.Should().Be(30);
        options.Security.HostMode.Should().Be("InternetExposed");
    }

    [Fact]
    [Trait("Category", "Unit")]
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
    [Trait("Category", "Unit")]
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
