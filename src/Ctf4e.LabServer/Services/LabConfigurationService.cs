using System;
using System.IO;
using System.Threading.Tasks;
using Ctf4e.LabServer.Configuration;
using Ctf4e.LabServer.Options;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Ctf4e.LabServer.Services;

public interface ILabConfigurationService
{
    LabConfiguration CurrentConfiguration { get; }

    /// <summary>
    /// Reloads the current configuration.
    /// </summary>
    /// <returns></returns>
    Task ReadConfigurationAsync();
}

/// <summary>
/// Loads and parses the lab configuration file.
/// </summary>
public class LabConfigurationService : ILabConfigurationService
{
    private readonly IOptionsMonitor<LabOptions> _options;
    public LabConfiguration CurrentConfiguration { get; private set; }

    public LabConfigurationService(IOptionsMonitor<LabOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Reloads the current configuration.
    /// </summary>
    /// <returns></returns>
    public async Task ReadConfigurationAsync()
    {
        try
        {
            // Read and parse configuration
            var labConfigData = await File.ReadAllTextAsync(_options.CurrentValue.LabConfigurationFile);
            CurrentConfiguration = JsonConvert.DeserializeObject<LabConfiguration>(labConfigData);
        }
        catch(Exception ex)
        {
            throw new Exception("Could not load lab configuration", ex);
        }
    }
}