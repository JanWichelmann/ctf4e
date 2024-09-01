using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.LabServer.Configuration;
using Ctf4e.LabServer.Options;
using Microsoft.Extensions.Options;

namespace Ctf4e.LabServer.Services;

public interface ILabConfigurationService
{
    LabConfiguration CurrentConfiguration { get; }

    /// <summary>
    /// Reloads the current configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    Task ReadConfigurationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates the current configuration and reloads it.
    /// </summary>
    /// <param name="configuration">New configuration to be stored on disk.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    Task WriteConfigurationAsync(LabConfiguration configuration, CancellationToken cancellationToken);
    
    /// <summary>
    /// Deserializes the given configuration string and checks it for validity.
    /// </summary>
    /// <param name="configuration">Configuration string to be checked.</param>
    /// <param name="issues">Reference to a variable to store a list of issues found during the check.</param>
    /// <param name="checkedConfiguration">Reference to a variable to store the new configuration.</param>
    /// <returns></returns>
    bool DeserializeAndCheckConfiguration(string configuration, out List<string> issues, out LabConfiguration checkedConfiguration);
}

/// <summary>
/// Loads and parses the lab configuration file.
/// </summary>
public class LabConfigurationService : ILabConfigurationService
{
    public LabConfiguration CurrentConfiguration { get; private set; }
    
    private readonly JsonSerializerOptions _writeOptions = new();
    private readonly IOptions<LabOptions> _options;

    /// <summary>
    /// Loads and parses the lab configuration file.
    /// </summary>
    public LabConfigurationService(IOptions<LabOptions> options)
    {
        _options = options;
        
        // Configure JSON serializers
        _writeOptions.WriteIndented = true;
    }

    public async Task ReadConfigurationAsync(CancellationToken cancellationToken)
    {
        try
        {                 
            // Read and parse configuration
            var labConfigData = await File.ReadAllTextAsync(_options.Value.LabConfigurationFile, cancellationToken);
            CurrentConfiguration = JsonSerializer.Deserialize<LabConfiguration>(labConfigData);
        }
        catch(Exception ex)
        {
            throw new Exception("Could not load lab configuration", ex);
        }
    }
    
    public async Task WriteConfigurationAsync(LabConfiguration configuration, CancellationToken cancellationToken)
    {
        try
        {
            // Serialize configuration
            // Use default converters here
            var labConfigData = JsonSerializer.Serialize(configuration, _writeOptions);
            
            // Write configuration
            await File.WriteAllTextAsync(_options.Value.LabConfigurationFile, labConfigData, cancellationToken);
            
            // Remember new configuration
            CurrentConfiguration = configuration;
        }
        catch(Exception ex)
        {
            throw new Exception("Could not write lab configuration", ex);
        }
    }

    public bool DeserializeAndCheckConfiguration(string configuration, out List<string> issues, out LabConfiguration checkedConfiguration)
    {
        var parsedConfiguration = JsonSerializer.Deserialize<LabConfiguration>(configuration);

        var exerciseIds = new HashSet<int>();
        issues = [];
        foreach(var exercise in parsedConfiguration.Exercises)
        {
            // Make sure that exercise IDs are unique
            if(exerciseIds.Contains(exercise.Id)) 
                issues.Add($"Duplicate exercise ID: #{exercise.Id}");

            exerciseIds.Add(exercise.Id);

            // Run exercise self-check
            if(!exercise.Validate(out string errorMessage)) 
                issues.Add($"Validation of exercise #{exercise.Id} failed: {errorMessage}");
        } 
        
        if(issues.Count > 0)
        {
            checkedConfiguration = null;
            return false;
        }

        checkedConfiguration = parsedConfiguration;
        return true;
    }
}