using System.Diagnostics;
using ShrimpCam.Core.Abstractions;

namespace ShrimpCam.Infrastructure.Processes;

internal sealed class ProcessStreamRunner : IProcessStreamRunner
{
    public Task<IProcessStreamHandle> StartAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            Arguments = request.Arguments,
            WorkingDirectory = request.WorkingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = startInfo };
        process.Start();

        return Task.FromResult<IProcessStreamHandle>(new ProcessStreamHandle(process));
    }

    private sealed class ProcessStreamHandle(Process process) : IProcessStreamHandle
    {
        private bool _disposed;

        public Stream StandardOutput => process.StandardOutput.BaseStream;

        public async Task<ProcessResult> WaitForExitAsync(CancellationToken cancellationToken)
        {
            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var standardOutput = await standardOutputTask.ConfigureAwait(false);
            var standardError = await standardErrorTask.ConfigureAwait(false);

            return new ProcessResult(process.ExitCode, standardOutput, standardError);
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
            }
            catch (InvalidOperationException)
            {
            }

            process.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
