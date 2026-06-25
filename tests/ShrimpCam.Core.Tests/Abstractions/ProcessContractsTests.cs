using ShrimpCam.Core.Abstractions;

namespace ShrimpCam.Core.Tests.Abstractions;

public sealed class ProcessContractsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Process_request_exposes_constructor_values()
    {
        var request = new ProcessRequest("ffmpeg", "-i input", "captures");

        request.FileName.Should().Be("ffmpeg");
        request.Arguments.Should().Be("-i input");
        request.WorkingDirectory.Should().Be("captures");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Process_result_exposes_constructor_values()
    {
        var result = new ProcessResult(1, "stdout", "stderr");

        result.ExitCode.Should().Be(1);
        result.StandardOutput.Should().Be("stdout");
        result.StandardError.Should().Be("stderr");
    }
}
