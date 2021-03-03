using System;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Server.Data;
using Ctf4e.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Ctf4e.Server.Services
{
    public interface IConfigurationService
    {
        Task<int> GetFlagMinimumPointsDivisorAsync(CancellationToken cancellationToken = default);
        Task SetFlagMinimumPointsDivisorAsync(int value, CancellationToken cancellationToken = default);
        Task<int> GetFlagHalfPointsSubmissionCountAsync(CancellationToken cancellationToken = default);
        Task SetFlagHalfPointsSubmissionCountAsync(int value, CancellationToken cancellationToken = default);
        Task<int> GetScoreboardEntryCountAsync(CancellationToken cancellationToken = default);
        Task SetScoreboardEntryCountAsync(int value, CancellationToken cancellationToken = default);
        Task<int> GetScoreboardCachedSecondsAsync(CancellationToken cancellationToken = default);
        Task<bool> GetPassAsGroupAsync(CancellationToken cancellationToken = default);
        Task SetPassAsGroupAsync(bool value, CancellationToken cancellationToken = default);
        Task SetScoreboardCachedSecondsAsync(int value, CancellationToken cancellationToken = default);
        Task<string> GetNavbarTitleAsync(CancellationToken cancellationToken = default);
        Task SetNavbarTitleAsync(string value, CancellationToken cancellationToken = default);
        Task<string> GetPageTitleAsync(CancellationToken cancellationToken = default);
        Task SetPageTitleAsync(string value, CancellationToken cancellationToken = default);
        Task SetGroupSizeMinAsync(int value, CancellationToken cancellationToken = default);
        Task<int> GetGroupSizeMinAsync(CancellationToken cancellationToken = default);
        Task SetGroupSizeMaxAsync(int value, CancellationToken cancellationToken = default);
        Task<int> GetGroupSizeMaxAsync(CancellationToken cancellationToken = default);
    }

    public class ConfigurationService : IConfigurationService
    {
        private readonly CtfDbContext _dbContext;
        private readonly IMemoryCache _cache;

        // ReSharper disable InconsistentNaming
        private const string ConfigKeyFlagMinimumPointsDivisor = "FlagMinimumPointsDivisor";
        private const string ConfigKeyFlagHalfPointsSubmissionCount = "FlagHalfPointsSubmissionCount";
        private const string ConfigKeyScoreboardEntryCount = "ScoreboardEntryCount";
        private const string ConfigKeyScoreboardCachedSeconds = "ScoreboardCachedSeconds";
        private const string ConfigKeyPassAsGroup = "PassAsGroup";
        private const string ConfigKeyNavbarTitle = "NavbarTitle";
        private const string ConfigKeyPageTitle = "PageTitle";
        private const string ConfigKeyGroupSizeMin = "GroupSizeMin";
        private const string ConfigKeyGroupSizeMax = "GroupSizeMax";
        // ReSharper restore InconsistentNaming

        public ConfigurationService(CtfDbContext dbContext, IMemoryCache cache)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public Task<int> GetFlagMinimumPointsDivisorAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(ConfigKeyFlagMinimumPointsDivisor, s => s == null ? 1 : int.Parse(s), cancellationToken);

        public Task SetFlagMinimumPointsDivisorAsync(int value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(ConfigKeyFlagMinimumPointsDivisor, value, cancellationToken);

        public Task<int> GetFlagHalfPointsSubmissionCountAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(ConfigKeyFlagHalfPointsSubmissionCount, s => s == null ? 1 : int.Parse(s), cancellationToken);

        public Task SetFlagHalfPointsSubmissionCountAsync(int value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(ConfigKeyFlagHalfPointsSubmissionCount, value, cancellationToken);

        public Task<int> GetScoreboardEntryCountAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(ConfigKeyScoreboardEntryCount, s => s == null ? 3 : int.Parse(s), cancellationToken);

        public Task SetScoreboardEntryCountAsync(int value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(ConfigKeyScoreboardEntryCount, value, cancellationToken);

        public Task<int> GetScoreboardCachedSecondsAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(ConfigKeyScoreboardCachedSeconds, s => s == null ? 10 : int.Parse(s), cancellationToken);

        public Task<bool> GetPassAsGroupAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(ConfigKeyPassAsGroup, s => s != null && bool.Parse(s), cancellationToken);

        public Task SetPassAsGroupAsync(bool value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(ConfigKeyPassAsGroup, value, cancellationToken);

        public Task SetScoreboardCachedSecondsAsync(int value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(ConfigKeyScoreboardCachedSeconds, value, cancellationToken);

        public Task<string> GetNavbarTitleAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(ConfigKeyNavbarTitle, s => s ?? "CTF4E", cancellationToken);

        public Task SetNavbarTitleAsync(string value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(ConfigKeyNavbarTitle, value, cancellationToken);

        public Task<string> GetPageTitleAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(ConfigKeyPageTitle, s => s ?? "CTF4E", cancellationToken);

        public Task SetPageTitleAsync(string value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(ConfigKeyPageTitle, value, cancellationToken);

        public Task SetGroupSizeMinAsync(int value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(ConfigKeyGroupSizeMin, value, cancellationToken);

        public Task<int> GetGroupSizeMinAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(ConfigKeyGroupSizeMin, s => s == null ? 1 : int.Parse(s), cancellationToken);

        public Task SetGroupSizeMaxAsync(int value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(ConfigKeyGroupSizeMax, value, cancellationToken);

        public Task<int> GetGroupSizeMaxAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(ConfigKeyGroupSizeMax, s => s == null ? 2 : int.Parse(s), cancellationToken);

        private async Task<TValue> GetConfigItemAsync<TValue>(string key, Func<string, TValue> valueConverter, CancellationToken cancellationToken)
        {
            // Try to retrieve from config cache
            string cacheKey = "config-" + key;
            if(!_cache.TryGetValue(cacheKey, out string value))
            {
                // Retrieve from database
                var config = await _dbContext.ConfigurationItems.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Key == key, cancellationToken);
                value = config?.Value;

                // Update cache
                _cache.Set(cacheKey, value);
            }

            return valueConverter(value);
        }

        private async Task AddOrUpdateConfigItem<TValue>(string key, TValue value, CancellationToken cancellationToken)
        {
            // Write updated config to database
            string cacheKey = "config-" + key;
            var config = await _dbContext.ConfigurationItems.FindAsync(new object[] { key }, cancellationToken);
            if(config == null)
                await _dbContext.ConfigurationItems.AddAsync(new ConfigurationItemEntity { Key = key, Value = value.ToString() }, cancellationToken);
            else
                config.Value = value.ToString();
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Update cache
            _cache.Set(cacheKey, value.ToString());
        }
    }
}