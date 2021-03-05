using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ctf4e.LessonServer.Configuration;
using Ctf4e.LessonServer.Configuration.Exercises;
using Ctf4e.LessonServer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Ctf4e.LessonServer.Services
{
    public interface ILessonConfigurationService
    {
        LessonConfiguration CurrentConfiguration { get; }

        /// <summary>
        /// Reloads the current configuration.
        /// </summary>
        /// <returns></returns>
        Task ReadConfigurationAsync();
    }

    /// <summary>
    /// Loads and parses the lesson configuration file.
    /// </summary>
    public class LessonConfigurationService : ILessonConfigurationService
    {
        private readonly IOptionsMonitor<LessonOptions> _options;
        public LessonConfiguration CurrentConfiguration { get; private set; }

        public LessonConfigurationService(IOptionsMonitor<LessonOptions> options)
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
                var lessonConfigData = await File.ReadAllTextAsync(_options.CurrentValue.LessonConfigurationFile);
                CurrentConfiguration = JsonConvert.DeserializeObject<LessonConfiguration>(lessonConfigData);
            }
            catch(Exception ex)
            {
                throw new Exception("Could not load lesson configuration", ex);
            }
        }
    }
}