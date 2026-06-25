using System.ComponentModel.DataAnnotations;
using ShrimpCam.Core.Configuration;

namespace ShrimpCam.Core.Tests.Configuration;

public sealed class ShrimpCamOptionsValidationTests
{
    private static readonly string[] ExpectedInvalidMemberNames =
    [
        "Platform",
        "Source",
        "CaptureWidth",
        "CaptureHeight",
        "StreamWidth",
        "StreamHeight",
        "StreamFramesPerSecond",
        "IntervalMinutes",
        "ActiveStartHourUtc",
        "ActiveEndHourUtc",
        "ImageRootPath",
        "RetentionDays",
        "HostMode",
    ];

    [Fact]
    [Trait("Category", "Unit")]
    public void Default_options_match_expected_bootstrap_values()
    {
        var options = new ShrimpCamOptions();

        options.Camera.Platform.Should().Be("Windows");
        options.Camera.Source.Should().BeEmpty();
        options.Camera.CaptureWidth.Should().Be(1920);
        options.Camera.CaptureHeight.Should().Be(1080);
        options.Camera.StreamWidth.Should().Be(1280);
        options.Camera.StreamHeight.Should().Be(720);
        options.Camera.StreamFramesPerSecond.Should().Be(15);
        options.Capture.Enabled.Should().BeFalse();
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
            Camera = new CameraOptions
            {
                Platform = "Windows",
                Source = "Logitech C920",
                CaptureWidth = 1920,
                CaptureHeight = 1080,
                StreamWidth = 1280,
                StreamHeight = 720,
                StreamFramesPerSecond = 15,
            },
            Capture = new CaptureOptions { Enabled = true, IntervalMinutes = 5, ActiveStartHourUtc = 8, ActiveEndHourUtc = 20 },
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
            Camera = new CameraOptions
            {
                Platform = "Other",
                Source = string.Empty,
                CaptureWidth = 0,
                CaptureHeight = 0,
                StreamWidth = 0,
                StreamHeight = 0,
                StreamFramesPerSecond = 0,
            },
            Capture = new CaptureOptions { Enabled = true, IntervalMinutes = 0, ActiveStartHourUtc = -1, ActiveEndHourUtc = 25 },
            Storage = new StorageOptions { ImageRootPath = string.Empty, RetentionDays = 0 },
            Security = new SecurityOptions { HostMode = "Unknown" },
        };

        var results = ValidateNested(options);

        results.Should().NotBeEmpty();
        results.SelectMany(result => result.MemberNames).Should().Contain(ExpectedInvalidMemberNames);
    }

    private static List<ValidationResult> ValidateNested(ShrimpCamOptions options)
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
