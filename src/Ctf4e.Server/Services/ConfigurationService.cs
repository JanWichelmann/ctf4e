using System;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Server.Data;
using Ctf4e.Server.Data.Entities;
using LtiAdvantageTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace Ctf4e.Server.Services;

public interface IConfigurationService
{
    ConfigurationEntry<int> FlagMinimumPointsDivisor { get; }
    ConfigurationEntry<int> FlagHalfPointsSubmissionCount { get; }
    ConfigurationEntry<int> ScoreboardEntryCount { get; }
    ConfigurationEntry<int> ScoreboardCachedSeconds { get; }
    ConfigurationEntry<bool> PassAsGroup { get; }
    ConfigurationEntry<bool> ShowGroupMemberSubmissions { get; }
    ConfigurationEntry<bool> EnableScoreboard { get; }
    ConfigurationEntry<bool> EnableFlags { get; }
    ConfigurationEntry<string> NavbarTitle { get; }
    ConfigurationEntry<string> PageTitle { get; }
    ConfigurationEntry<int> GroupSizeMin { get; }
    ConfigurationEntry<int> GroupSizeMax { get; }
    ConfigurationEntry<string> GroupSelectionPageText { get; }
    ConfigurationEntry<string> LtiAdvantageJsonWebKey { get; }
}

public class ConfigurationService(CtfDbContext dbContext, IMemoryCache cache) : IConfigurationService
{
    public ConfigurationEntry<int> FlagMinimumPointsDivisor { get; } = new(dbContext, cache, "FlagMinimumPointsDivisor", s => s == null ? 1 : int.Parse(s));
    public ConfigurationEntry<int> FlagHalfPointsSubmissionCount { get; } = new(dbContext, cache, "FlagHalfPointsSubmissionCount", s => s == null ? 1 : int.Parse(s));
    public ConfigurationEntry<int> ScoreboardEntryCount { get; } = new(dbContext, cache, "ScoreboardEntryCount", s => s == null ? 3 : int.Parse(s));
    public ConfigurationEntry<int> ScoreboardCachedSeconds { get; } = new(dbContext, cache, "ScoreboardCachedSeconds", s => s == null ? 10 : int.Parse(s));
    public ConfigurationEntry<bool> PassAsGroup { get; } = new(dbContext, cache, "PassAsGroup", s => s != null && bool.Parse(s));
    public ConfigurationEntry<bool> ShowGroupMemberSubmissions { get; } = new(dbContext, cache, "ShowGroupMemberSubmissions", s => s == null || bool.Parse(s));
    public ConfigurationEntry<bool> EnableScoreboard { get; } = new(dbContext, cache, "EnableScoreboard", s => s == null || bool.Parse(s));
    public ConfigurationEntry<bool> EnableFlags { get; } = new(dbContext, cache, "EnableFlags", s => s == null || bool.Parse(s));
    public ConfigurationEntry<string> NavbarTitle { get; } = new(dbContext, cache, "NavbarTitle", s => s ?? "CTF4E");
    public ConfigurationEntry<string> PageTitle { get; } = new(dbContext, cache, "PageTitle", s => s ?? "CTF4E");
    public ConfigurationEntry<int> GroupSizeMin { get; } = new(dbContext, cache, "GroupSizeMin", s => s == null ? 1 : int.Parse(s));
    public ConfigurationEntry<int> GroupSizeMax { get; } = new(dbContext, cache, "GroupSizeMax", s => s == null ? 2 : int.Parse(s));
    public ConfigurationEntry<string> GroupSelectionPageText { get; } = new(dbContext, cache, "GroupSelectionPageText", s => s ?? string.Empty);
    public ConfigurationEntry<string> LtiAdvantageJsonWebKey { get; } = new(dbContext, cache, "LtiAdvantageJsonWebKey", s => s ?? string.Empty); // (De-)Serialization is done elsewhere
}

public class ConfigurationEntry<TValue>(CtfDbContext dbContext, IMemoryCache cache, string key, Func<string, TValue> deserializer)
{
    public async Task<TValue> GetAsync(CancellationToken cancellationToken)
    {
        if(!cache.TryGetValue(key, out string valueStr))
        {
            // Retrieve from database
            var config = await dbContext.ConfigurationItems.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Key == key, cancellationToken);
            valueStr = config?.Value;

            cache.Set(key, valueStr);
        }

        return deserializer(valueStr);
    }

    public async Task SetAsync(TValue value, CancellationToken cancellationToken)
    {
        string valueStr = value.ToString();

        // Write updated config to database
        var config = await dbContext.ConfigurationItems.FindAsync([key], cancellationToken);
        if(config == null)
            await dbContext.ConfigurationItems.AddAsync(new ConfigurationItemEntity { Key = key, Value = valueStr }, cancellationToken);
        else
            config.Value = valueStr;
        await dbContext.SaveChangesAsync(cancellationToken);

        // Update cache
        cache.Set(key, valueStr);
    }
}

public class LtiConfigurationStore(IConfigurationService configurationService) : ILtiConfigurationStore
{
    public Task<string> GetSerializedPublicJsonWebKeyAsync(CancellationToken cancellationToken) 
        => configurationService.LtiAdvantageJsonWebKey.GetAsync(cancellationToken);

    public Task SetSerializedPublicJsonWebKeyAsync(string key, CancellationToken cancellationToken) 
        => configurationService.LtiAdvantageJsonWebKey.SetAsync(key, cancellationToken);
}