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
        Task<string> GetGroupSelectionPageTextAsync(CancellationToken cancellationToken = default);
        Task SetGroupSelectionPageTextAsync(string value, CancellationToken cancellationToken = default);
    }

    public class ConfigurationService : IConfigurationService
    {
        private readonly CtfDbContext _dbContext;
        private readonly IMemoryCache _cache;

        private const string _configKeyFlagMinimumPointsDivisor = "FlagMinimumPointsDivisor";
        private const string _configKeyFlagHalfPointsSubmissionCount = "FlagHalfPointsSubmissionCount";
        private const string _configKeyScoreboardEntryCount = "ScoreboardEntryCount";
        private const string _configKeyScoreboardCachedSeconds = "ScoreboardCachedSeconds";
        private const string _configKeyPassAsGroup = "PassAsGroup";
        private const string _configKeyNavbarTitle = "NavbarTitle";
        private const string _configKeyPageTitle = "PageTitle";
        private const string _configKeyGroupSizeMin = "GroupSizeMin";
        private const string _configKeyGroupSizeMax = "GroupSizeMax";
        private const string _configKeyGroupSelectionPageText = "GroupSelectionPageText";

        public ConfigurationService(CtfDbContext dbContext, IMemoryCache cache)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public Task<int> GetFlagMinimumPointsDivisorAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(_configKeyFlagMinimumPointsDivisor, s => s == null ? 1 : int.Parse(s), cancellationToken);

        public Task SetFlagMinimumPointsDivisorAsync(int value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(_configKeyFlagMinimumPointsDivisor, value, cancellationToken);

        public Task<int> GetFlagHalfPointsSubmissionCountAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(_configKeyFlagHalfPointsSubmissionCount, s => s == null ? 1 : int.Parse(s), cancellationToken);

        public Task SetFlagHalfPointsSubmissionCountAsync(int value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(_configKeyFlagHalfPointsSubmissionCount, value, cancellationToken);

        public Task<int> GetScoreboardEntryCountAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(_configKeyScoreboardEntryCount, s => s == null ? 3 : int.Parse(s), cancellationToken);

        public Task SetScoreboardEntryCountAsync(int value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(_configKeyScoreboardEntryCount, value, cancellationToken);

        public Task<int> GetScoreboardCachedSecondsAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(_configKeyScoreboardCachedSeconds, s => s == null ? 10 : int.Parse(s), cancellationToken);

        public Task<bool> GetPassAsGroupAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(_configKeyPassAsGroup, s => s != null && bool.Parse(s), cancellationToken);

        public Task SetPassAsGroupAsync(bool value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(_configKeyPassAsGroup, value, cancellationToken);

        public Task SetScoreboardCachedSecondsAsync(int value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(_configKeyScoreboardCachedSeconds, value, cancellationToken);

        public Task<string> GetNavbarTitleAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(_configKeyNavbarTitle, s => s ?? "CTF4E", cancellationToken);

        public Task SetNavbarTitleAsync(string value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(_configKeyNavbarTitle, value, cancellationToken);

        public Task<string> GetPageTitleAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(_configKeyPageTitle, s => s ?? "CTF4E", cancellationToken);

        public Task SetPageTitleAsync(string value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(_configKeyPageTitle, value, cancellationToken);

        public Task SetGroupSizeMinAsync(int value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(_configKeyGroupSizeMin, value, cancellationToken);

        public Task<int> GetGroupSizeMinAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(_configKeyGroupSizeMin, s => s == null ? 1 : int.Parse(s), cancellationToken);

        public Task SetGroupSizeMaxAsync(int value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(_configKeyGroupSizeMax, value, cancellationToken);

        public Task<int> GetGroupSizeMaxAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(_configKeyGroupSizeMax, s => s == null ? 2 : int.Parse(s), cancellationToken);
        
        public Task<string> GetGroupSelectionPageTextAsync(CancellationToken cancellationToken = default)
            => GetConfigItemAsync(_configKeyGroupSelectionPageText, s => s ?? string.Empty, cancellationToken);

        public Task SetGroupSelectionPageTextAsync(string value, CancellationToken cancellationToken = default)
            => AddOrUpdateConfigItem(_configKeyGroupSelectionPageText, value, cancellationToken);

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