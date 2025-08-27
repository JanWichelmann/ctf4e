using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.LabServer.Configuration;

namespace Ctf4e.LabServer.Services;

public interface ILabConfigurationService
{
    LabConfiguration CurrentConfiguration { get; }

    /// <summary>
    /// Returns whether the given file is a configuration file.
    /// </summary>
    /// <param name="fileName">File name.</param>
    /// <returns></returns>
    public bool IsConfigurationFile(string fileName);

    /// <summary>
    /// Returns the paths of all configuration files.
    /// </summary>
    /// <returns></returns>
    ImmutableList<string> GetConfigurationFilePaths();
    
    /// <summary>
    /// Reloads the current configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    Task ReadConfigurationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates the given configuration file and reloads the entire lab configuration.
    /// </summary>
    /// <param name="fileToChange">The file to overwrite.</param>
    /// <param name="configuration">New configuration to be stored on disk.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    Task WriteConfigurationAsync(string fileToChange, LabConfiguration configuration, CancellationToken cancellationToken);

    /// <summary>
    /// Deserializes the lab configuration, while optionally replacing the content of a specific file.
    /// Issues found during deserialize are stored in the <see cref="issues"/> list.
    /// </summary>
    /// <param name="fileToChange">The configuration file which is to be replaced, or null.</param>
    /// <param name="newFileContent">The new content of the replaced configuration file, or null.</param>
    /// <param name="issues">Pointer to list for storing the detected issues.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized configuration, or null.</returns>
    public Task<LabConfiguration> DeserializeAndCheckConfigurationAsync(string fileToChange, string newFileContent, List<string> issues, CancellationToken cancellationToken);
}

/// <summary>
/// Loads and parses the lab configuration file.
/// </summary>
public class LabConfigurationService : ILabConfigurationService
{
    private readonly ImmutableList<string> _configurationFilePaths;

    public LabConfiguration CurrentConfiguration { get; private set; }


    private readonly JsonSerializerOptions _writeOptions = new();

    /// <summary>
    /// Loads and parses the lab configuration file.
    /// </summary>
    public LabConfigurationService(string mainFilePath, List<string> additionalFilePaths)
    {
        _configurationFilePaths = (additionalFilePaths ?? [])
            .Prepend(mainFilePath)
            .Where(f => f != null)
            .ToImmutableList();

        // Configure JSON serializers
        _writeOptions.WriteIndented = true;
    }

    public bool IsConfigurationFile(string fileName)
        => _configurationFilePaths.Contains(fileName);
    
    public ImmutableList<string> GetConfigurationFilePaths()
        => _configurationFilePaths;

    public async Task ReadConfigurationAsync(CancellationToken cancellationToken)
    {
        try
        {
            CurrentConfiguration = await DeserializeConfigurationAsync(cancellationToken);
        }
        catch(Exception ex)
        {
            throw new Exception("Could not load lab configuration", ex);
        }
    }

    public async Task WriteConfigurationAsync(string fileToChange, LabConfiguration configuration, CancellationToken cancellationToken)
    {
        if(!_configurationFilePaths.Contains(fileToChange))
            throw new Exception("Invalid configuration file");

        try
        {
            // Serialize configuration
            // Use default converters here
            var serializedConfig = JsonSerializer.Serialize(configuration, _writeOptions);

            // Write configuration
            await File.WriteAllTextAsync(fileToChange, serializedConfig, cancellationToken);

            // Reload full configuration
            CurrentConfiguration = await DeserializeConfigurationAsync(cancellationToken);
        }
        catch(Exception ex)
        {
            throw new Exception("Could not write lab configuration", ex);
        }
    }

    private async Task<LabConfiguration> DeserializeConfigurationAsync(CancellationToken cancellationToken)
    {
        List<string> issues = [];
        LabConfiguration configuration = await DeserializeAndCheckConfigurationAsync(null, null, issues, cancellationToken);
        if(issues.Count > 0)
            throw new Exception(issues[0]);

        return configuration;
    }

    public async Task<LabConfiguration> DeserializeAndCheckConfigurationAsync(string fileToChange, string newFileContent, List<string> issues, CancellationToken cancellationToken)
    {
        // Read, parse and merge all configuration files
        // Do some sanity checks
        LabConfiguration configuration = new() { Exercises = [] };
        HashSet<int> exerciseIds = [];
        foreach(var configurationFilePath in _configurationFilePaths)
        {
            string labConfigData;
            if(configurationFilePath == fileToChange && newFileContent != null)
                labConfigData = newFileContent;
            else
                labConfigData = await File.ReadAllTextAsync(configurationFilePath, cancellationToken);

            var parsedConfiguration = JsonSerializer.Deserialize<LabConfiguration>(labConfigData);

            // Merge exercises
            foreach(var exercise in parsedConfiguration.Exercises)
            {
                // Make sure that exercise IDs are unique
                if(!exerciseIds.Add(exercise.Id))
                    issues.Add($"Duplicate exercise ID: #{exercise.Id}");

                // Run exercise self-check
                if(!exercise.Validate(out string errorMessage))
                    issues.Add($"Validation of exercise #{exercise.Id} failed: {errorMessage}");
            }

            configuration.Exercises.AddRange(parsedConfiguration.Exercises);
        }

        return issues.Count == 0 ? configuration : null;
    }
}