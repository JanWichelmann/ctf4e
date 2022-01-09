using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Ctf4e.Server.Services;

public interface ILoginRateLimiter
{
    /// <summary>
    /// Records a new attempt and checks whether the rate limit is hit (true) or not (false).
    /// </summary>
    /// <param name="userId">Affected user ID.</param>
    /// <returns></returns>
    bool CheckRateLimitHit(int userId);

    /// <summary>
    /// Resets the rate limit state for the given user.
    /// </summary>
    /// <param name="userId">Affected user ID.</param>
    void ResetRateLimit(int userId);
}

public class LoginRateLimiter : ILoginRateLimiter
{
    private readonly TimeSpan _maxTriesPeriod = TimeSpan.FromMinutes(30);
    private const int MaxTries = 10;

    private readonly ConcurrentDictionary<int, RateLimitData> _rateLimitData = new();

    /// <summary>
    /// Records a new attempt and checks whether the rate limit is hit (true) or not (false).
    /// </summary>
    /// <param name="userId">Affected user ID.</param>
    /// <returns></returns>
    public bool CheckRateLimitHit(int userId)
    {
        var rateLimitData = _rateLimitData.GetOrAdd(userId, _ => new RateLimitData());
        lock(rateLimitData.Lock)
        {
            var now = DateTime.UtcNow;

            // Remove attempts that are older than _maxTriesPeriod minutes
            while(rateLimitData.LoginAttempts.TryPeek(out var oldestAttempt) && now - oldestAttempt > _maxTriesPeriod)
                rateLimitData.LoginAttempts.Dequeue();

            // Now the queue only contains items that are less than _maxTriesPeriod minutes old
            // If the queue is still full, hit the rate limit
            if(rateLimitData.LoginAttempts.Count >= MaxTries)
            {
                return true;
            }

            // No rate limit hit; remember this attempt
            rateLimitData.LoginAttempts.Enqueue(now);
            return false;
        }
    }

    /// <summary>
    /// Resets the rate limit state for the given user.
    /// </summary>
    /// <param name="userId">Affected user ID.</param>
    public void ResetRateLimit(int userId)
    {
        var rateLimitData = _rateLimitData.GetOrAdd(userId, _ => new RateLimitData());
        lock(rateLimitData.Lock)
        {
            rateLimitData.LoginAttempts.Clear();
        }
    }
}

internal class RateLimitData
{
    public Queue<DateTime> LoginAttempts { get; } = new();
    public object Lock { get; } = new();
}