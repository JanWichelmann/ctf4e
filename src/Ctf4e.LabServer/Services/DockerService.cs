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
    Task<(bool passed, string stderr)> GradeAsync(int userId, int exerciseId, string input, CancellationToken cancellationToken);
}

public class DockerService : IDockerService, IDisposable
{
    /// <summary>
    /// We don't use <see cref="IOptionsMonitor{TOptions}"/> here, as we cannot safely support configuration changes during runtime.
    /// </summary>
    private readonly IOptions<LabOptions> _options;
        
    private readonly DockerClient _dockerClient;

    public DockerService(IOptions<LabOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Only initialize this if container support is enabled
        if(!string.IsNullOrWhiteSpace(_options.Value.DockerContainerName))
        {
            _dockerClient = new DockerClientConfiguration(new Uri("unix:///docker/docker.sock")).CreateClient();
        }
    }

    public async Task InitUserAsync(int userId, string userName, string password, CancellationToken cancellationToken)
    {
        if(_dockerClient == null)
            throw new NotSupportedException("Docker support is not initialized.");

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

    public async Task<(bool passed, string stderr)> GradeAsync(int userId, int exerciseId, string input, CancellationToken cancellationToken)
    {
        if(_dockerClient == null)
            throw new NotSupportedException("Docker support is not initialized.");
            
        if(string.IsNullOrWhiteSpace(_options.Value.DockerContainerGradeScriptPath))
            throw new NotSupportedException("Grading script is not specified.");
            
        // Prepare command
        bool stringInputPresent = input != null;
        var execCreateResponse = await _dockerClient.Exec.ExecCreateContainerAsync(_options.Value.DockerContainerName, new ContainerExecCreateParameters
        {
            AttachStdin = stringInputPresent,
            AttachStderr = true,
            AttachStdout = true,
            Tty = false,
            Cmd = new List<string>
            {
                _options.Value.DockerContainerGradeScriptPath,
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

    public void Dispose()
    {
        _dockerClient?.Dispose();
    }
}