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
        "ReconnectRetryAttempts",
        "ReconnectBackoffSeconds",
        "IntervalMinutes",
        "ActiveStartHourUtc",
        "ActiveEndHourUtc",
        "MotionThreshold",
        "MotionCooldownSeconds",
        "DatabasePath",
        "ImageRootPath",
        "TimelapseRootPath",
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
        options.Camera.ReconnectRetryAttempts.Should().Be(2);
        options.Camera.ReconnectBackoffSeconds.Should().Be(1);
        options.Capture.Enabled.Should().BeFalse();
        options.Capture.IntervalMinutes.Should().Be(5);
        options.Capture.ActiveStartHourUtc.Should().Be(6);
        options.Capture.ActiveEndHourUtc.Should().Be(22);
        options.Capture.MotionHighlightsEnabled.Should().BeFalse();
        options.Capture.MotionThreshold.Should().Be(0.35d);
        options.Capture.MotionCooldownSeconds.Should().Be(300);
        options.Storage.DatabasePath.Should().BeEmpty();
        options.Storage.ImageRootPath.Should().BeEmpty();
        options.Storage.TimelapseRootPath.Should().BeEmpty();
        options.Storage.RetentionDays.Should().Be(30);
        options.Security.HostMode.Should().Be("InternetExposed");
        options.Security.InitialAdministrator.Enabled.Should().BeTrue();
        options.Security.InitialAdministrator.UserName.Should().Be("admin");
        options.Security.InitialAdministrator.Password.Should().Be("AdminPass1234");
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
                ReconnectRetryAttempts = 2,
                ReconnectBackoffSeconds = 1,
            },
            Capture = new CaptureOptions
            {
                Enabled = true,
                IntervalMinutes = 5,
                ActiveStartHourUtc = 8,
                ActiveEndHourUtc = 20,
                MotionHighlightsEnabled = true,
                MotionThreshold = 0.45d,
                MotionCooldownSeconds = 180,
            },
            Storage = new StorageOptions { DatabasePath = "data/shrimpcam.db", ImageRootPath = "data/images", TimelapseRootPath = "data/timelapse", RetentionDays = 30 },
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
                ReconnectRetryAttempts = -1,
                ReconnectBackoffSeconds = 0,
            },
            Capture = new CaptureOptions
            {
                Enabled = true,
                IntervalMinutes = 0,
                ActiveStartHourUtc = -1,
                ActiveEndHourUtc = 25,
                MotionThreshold = 0d,
                MotionCooldownSeconds = -1,
            },
            Storage = new StorageOptions { DatabasePath = string.Empty, ImageRootPath = string.Empty, TimelapseRootPath = string.Empty, RetentionDays = 0 },
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
