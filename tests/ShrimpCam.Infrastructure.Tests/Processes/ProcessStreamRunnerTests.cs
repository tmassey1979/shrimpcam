using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Infrastructure.Processes;

#pragma warning disable CA2007

namespace ShrimpCam.Infrastructure.Tests.Processes;

public sealed class ProcessStreamRunnerTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Start_async_streams_stdout_and_collects_exit_diagnostics()
    {
        var runner = new ProcessStreamRunner();
        var command = CreatePowerShellCommand(
            "[Console]::Out.Write('frame-01'); [Console]::Error.Write('camera-warning')");

        await using var handle = await runner.StartAsync(command, CancellationToken.None).ConfigureAwait(true);

        using var reader = new StreamReader(handle.StandardOutput, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var stdout = await reader.ReadToEndAsync().ConfigureAwait(true);
        var exitResult = await handle.WaitForExitAsync(CancellationToken.None).ConfigureAwait(true);

        stdout.Should().Be("frame-01");
        exitResult.ExitCode.Should().Be(0);
        exitResult.StandardError.Should().Contain("camera-warning");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Dispose_async_stops_a_running_process()
    {
        var runner = new ProcessStreamRunner();
        var command = CreatePowerShellCommand("Start-Sleep -Seconds 30");

        var stopwatch = Stopwatch.StartNew();
        await using var handle = await runner.StartAsync(command, CancellationToken.None).ConfigureAwait(true);

        await handle.DisposeAsync().ConfigureAwait(true);

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    private static ProcessRequest CreatePowerShellCommand(string script) =>
        new(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "powershell" : "pwsh",
            $"-NoProfile -NonInteractive -Command \"{script}\"");
}

#pragma warning restore CA2007
