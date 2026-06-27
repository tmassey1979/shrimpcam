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

    private sealed class ProcessStreamHandle : IProcessStreamHandle
    {
        private readonly Process _process;
        private readonly Task<string> _standardErrorTask;
        private bool _disposed;

        public ProcessStreamHandle(Process process)
        {
            _process = process;
            _standardErrorTask = process.StandardError.ReadToEndAsync();
        }

        public Stream StandardOutput => _process.StandardOutput.BaseStream;

        public async Task<ProcessResult> WaitForExitAsync(CancellationToken cancellationToken)
        {
            var standardOutputTask = _process.StandardOutput.ReadToEndAsync(cancellationToken);

            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var standardOutput = await standardOutputTask.ConfigureAwait(false);
            var standardError = await _standardErrorTask.ConfigureAwait(false);

            return new ProcessResult(_process.ExitCode, standardOutput, standardError);
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
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit();
                }
            }
            catch (InvalidOperationException)
            {
            }

            _process.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
