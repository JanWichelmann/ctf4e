using System;
using System.IO;
using System.Threading;
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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    Task ReadConfigurationAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Loads and parses the lab configuration file.
/// </summary>
public class LabConfigurationService(IOptions<LabOptions> options) : ILabConfigurationService
{
    public LabConfiguration CurrentConfiguration { get; private set; }

    public async Task ReadConfigurationAsync(CancellationToken cancellationToken)
    {
        try
        {                 
            // Read and parse configuration
            var labConfigData = await File.ReadAllTextAsync(options.Value.LabConfigurationFile, cancellationToken);
            CurrentConfiguration = JsonConvert.DeserializeObject<LabConfiguration>(labConfigData);
        }
        catch(Exception ex)
        {
            throw new Exception("Could not load lab configuration", ex);
        }
    }
}