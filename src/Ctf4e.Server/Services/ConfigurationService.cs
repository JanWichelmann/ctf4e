using System;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Server.Data;
using Ctf4e.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Ctf4e.Server.Services;

public interface IConfigurationService
{
    Task<int> GetFlagMinimumPointsDivisorAsync(CancellationToken cancellationToken);
    Task SetFlagMinimumPointsDivisorAsync(int value, CancellationToken cancellationToken);
    Task<int> GetFlagHalfPointsSubmissionCountAsync(CancellationToken cancellationToken);
    Task SetFlagHalfPointsSubmissionCountAsync(int value, CancellationToken cancellationToken);
    Task<int> GetScoreboardEntryCountAsync(CancellationToken cancellationToken);
    Task SetScoreboardEntryCountAsync(int value, CancellationToken cancellationToken);
    Task<int> GetScoreboardCachedSecondsAsync(CancellationToken cancellationToken);
    Task<bool> GetPassAsGroupAsync(CancellationToken cancellationToken);
    Task SetPassAsGroupAsync(bool value, CancellationToken cancellationToken);
    Task SetScoreboardCachedSecondsAsync(int value, CancellationToken cancellationToken);
    Task<string> GetNavbarTitleAsync(CancellationToken cancellationToken);
    Task SetNavbarTitleAsync(string value, CancellationToken cancellationToken);
    Task<string> GetPageTitleAsync(CancellationToken cancellationToken);
    Task SetPageTitleAsync(string value, CancellationToken cancellationToken);
    Task SetGroupSizeMinAsync(int value, CancellationToken cancellationToken);
    Task<int> GetGroupSizeMinAsync(CancellationToken cancellationToken);
    Task SetGroupSizeMaxAsync(int value, CancellationToken cancellationToken);
    Task<int> GetGroupSizeMaxAsync(CancellationToken cancellationToken);
    Task<string> GetGroupSelectionPageTextAsync(CancellationToken cancellationToken);
    Task SetGroupSelectionPageTextAsync(string value, CancellationToken cancellationToken);
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

    public Task<int> GetFlagMinimumPointsDivisorAsync(CancellationToken cancellationToken)
        => GetConfigItemAsync(_configKeyFlagMinimumPointsDivisor, s => s == null ? 1 : int.Parse(s), cancellationToken);

    public Task SetFlagMinimumPointsDivisorAsync(int value, CancellationToken cancellationToken)
        => AddOrUpdateConfigItem(_configKeyFlagMinimumPointsDivisor, value, cancellationToken);

    public Task<int> GetFlagHalfPointsSubmissionCountAsync(CancellationToken cancellationToken)
        => GetConfigItemAsync(_configKeyFlagHalfPointsSubmissionCount, s => s == null ? 1 : int.Parse(s), cancellationToken);

    public Task SetFlagHalfPointsSubmissionCountAsync(int value, CancellationToken cancellationToken)
        => AddOrUpdateConfigItem(_configKeyFlagHalfPointsSubmissionCount, value, cancellationToken);

    public Task<int> GetScoreboardEntryCountAsync(CancellationToken cancellationToken)
        => GetConfigItemAsync(_configKeyScoreboardEntryCount, s => s == null ? 3 : int.Parse(s), cancellationToken);

    public Task SetScoreboardEntryCountAsync(int value, CancellationToken cancellationToken)
        => AddOrUpdateConfigItem(_configKeyScoreboardEntryCount, value, cancellationToken);

    public Task<int> GetScoreboardCachedSecondsAsync(CancellationToken cancellationToken)
        => GetConfigItemAsync(_configKeyScoreboardCachedSeconds, s => s == null ? 10 : int.Parse(s), cancellationToken);

    public Task<bool> GetPassAsGroupAsync(CancellationToken cancellationToken)
        => GetConfigItemAsync(_configKeyPassAsGroup, s => s != null && bool.Parse(s), cancellationToken);

    public Task SetPassAsGroupAsync(bool value, CancellationToken cancellationToken)
        => AddOrUpdateConfigItem(_configKeyPassAsGroup, value, cancellationToken);

    public Task SetScoreboardCachedSecondsAsync(int value, CancellationToken cancellationToken)
        => AddOrUpdateConfigItem(_configKeyScoreboardCachedSeconds, value, cancellationToken);

    public Task<string> GetNavbarTitleAsync(CancellationToken cancellationToken)
        => GetConfigItemAsync(_configKeyNavbarTitle, s => s ?? "CTF4E", cancellationToken);

    public Task SetNavbarTitleAsync(string value, CancellationToken cancellationToken)
        => AddOrUpdateConfigItem(_configKeyNavbarTitle, value, cancellationToken);

    public Task<string> GetPageTitleAsync(CancellationToken cancellationToken)
        => GetConfigItemAsync(_configKeyPageTitle, s => s ?? "CTF4E", cancellationToken);

    public Task SetPageTitleAsync(string value, CancellationToken cancellationToken)
        => AddOrUpdateConfigItem(_configKeyPageTitle, value, cancellationToken);

    public Task SetGroupSizeMinAsync(int value, CancellationToken cancellationToken)
        => AddOrUpdateConfigItem(_configKeyGroupSizeMin, value, cancellationToken);

    public Task<int> GetGroupSizeMinAsync(CancellationToken cancellationToken)
        => GetConfigItemAsync(_configKeyGroupSizeMin, s => s == null ? 1 : int.Parse(s), cancellationToken);

    public Task SetGroupSizeMaxAsync(int value, CancellationToken cancellationToken)
        => AddOrUpdateConfigItem(_configKeyGroupSizeMax, value, cancellationToken);

    public Task<int> GetGroupSizeMaxAsync(CancellationToken cancellationToken)
        => GetConfigItemAsync(_configKeyGroupSizeMax, s => s == null ? 2 : int.Parse(s), cancellationToken);
        
    public Task<string> GetGroupSelectionPageTextAsync(CancellationToken cancellationToken)
        => GetConfigItemAsync(_configKeyGroupSelectionPageText, s => s ?? string.Empty, cancellationToken);

    public Task SetGroupSelectionPageTextAsync(string value, CancellationToken cancellationToken)
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