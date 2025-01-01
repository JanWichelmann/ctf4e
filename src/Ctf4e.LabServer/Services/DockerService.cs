using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.LabServer.Options;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Options;

namespace Ctf4e.LabServer.Services;

public interface IDockerService
{
    Task InitUserAsync(int userId, string userName, string password, CancellationToken cancellationToken);
    Task<(bool passed, string stderr)> GradeAsync(string containerName, string gradingScriptPath, int userId, int exerciseId, string input, CancellationToken cancellationToken);
}

public class DockerService : IDockerService, IDisposable
{
    /// <summary>
    /// We don't use <see cref="IOptionsMonitor{TOptions}"/> here, as we cannot safely support configuration changes during runtime.
    /// </summary>
    private readonly IOptions<LabOptions> _options;
        
    private readonly DockerClient _dockerClient;
    private readonly SemaphoreSlim _gradingLock;

    public DockerService(IOptions<LabOptions> options)
    {
        _options = options;

        int concurrencyCount = _options.Value.DockerContainerGradingConcurrencyCount ?? 100;
        _gradingLock = new SemaphoreSlim(concurrencyCount, concurrencyCount);

        // Only initialize this if container support is enabled
        if(_options.Value.EnableDocker)
        {
            _dockerClient = new DockerClientConfiguration(new Uri("unix:///docker/docker.sock")).CreateClient();
        }
    }

    public async Task InitUserAsync(int userId, string userName, string password, CancellationToken cancellationToken)
    {
        if(_dockerClient == null)
            throw new InvalidOperationException("Docker support is not initialized.");

        if(string.IsNullOrWhiteSpace(_options.Value.DockerContainerInitUserScriptPath))
            throw new NotSupportedException("User initialization script is not specified.");
            
        // Prepare command
        var execCreateResponse = await _dockerClient.Exec.ExecCreateContainerAsync(_options.Value.DockerContainerName, new ContainerExecCreateParameters
        {
            AttachStdin = false,
            AttachStderr = true,
            AttachStdout = true,
            Tty = false,
            Cmd = new List<string>
            {
                _options.Value.DockerContainerInitUserScriptPath,
                userId.ToString(),
                userName,
                password
            },
            Detach = false
        }, cancellationToken);
                                                            
        // Run command and wait for completion
        var execStream = await _dockerClient.Exec.StartAndAttachContainerExecAsync(execCreateResponse.ID, false, cancellationToken);
        await execStream.ReadOutputToEndAsync(cancellationToken);
    }

    public async Task<(bool passed, string stderr)> GradeAsync(string containerName, string gradingScriptPath, int userId, int exerciseId, string input, CancellationToken cancellationToken)
    {
        if(_dockerClient == null)
            throw new InvalidOperationException("Docker support is not initialized.");
            
        if(string.IsNullOrWhiteSpace(gradingScriptPath))
            throw new NotSupportedException("Grading script is not specified.");
        
        // Wait for turn
        await _gradingLock.WaitAsync(cancellationToken);
        try
        {
            // Prepare command
            bool stringInputPresent = input != null;
            var execCreateResponse = await _dockerClient.Exec.ExecCreateContainerAsync(containerName, new ContainerExecCreateParameters
            {
                AttachStdin = stringInputPresent,
                AttachStderr = true,
                AttachStdout = true,
                Tty = false,
                Cmd = new List<string>
                {
                    gradingScriptPath,
                    userId.ToString(),
                    exerciseId.ToString()
                },
                Detach = false
            }, cancellationToken);

            // Run command
            var execStream = await _dockerClient.Exec.StartAndAttachContainerExecAsync(execCreateResponse.ID, false, cancellationToken);

            // Transmit input, if present
            if(stringInputPresent)
            {
                var inputBytes = Encoding.UTF8.GetBytes(input);
                await execStream.WriteAsync(inputBytes, 0, inputBytes.Length, cancellationToken);
                execStream.CloseWrite();
            }

            // Wait for completion
            var (stdout, stderr) = await execStream.ReadOutputToEndAsync(cancellationToken);

            // Passed?
            return (stdout.Trim() == "1", stderr);
        }
        finally
        {
            _gradingLock.Release();
        }
    }

    public void Dispose()
    {
        _dockerClient?.Dispose();
    }
}