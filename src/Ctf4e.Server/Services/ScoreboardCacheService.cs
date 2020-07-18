using System;
using System.Collections.Concurrent;
using Ctf4e.Server.ViewModels;

namespace Ctf4e.Server.Services
{
    public interface IScoreboardCacheService
    {
        bool TryGetValidLabScoreboard(int labId, out Scoreboard cachedScoreboard);
        bool TryGetValidFullScoreboard(out Scoreboard cachedScoreboard);
        void SetLabScoreboard(int labId, Scoreboard labScoreboard);
        void SetFullScoreboard(Scoreboard scoreboard);
        void InvalidateAll();
    }

    public class ScoreboardCacheService : IScoreboardCacheService
    {
        private readonly ConcurrentDictionary<int, Scoreboard> _labScoreboards = new ConcurrentDictionary<int, Scoreboard>();

        private Scoreboard _fullScoreboard;


        public bool TryGetValidLabScoreboard(int labId, out Scoreboard cachedScoreboard)
        {
            return _labScoreboards.TryGetValue(labId, out cachedScoreboard) && DateTime.Now <= cachedScoreboard.ValidUntil;
        }

        public bool TryGetValidFullScoreboard(out Scoreboard cachedScoreboard)
        {
            cachedScoreboard = _fullScoreboard;
            return _fullScoreboard != null && DateTime.Now <= _fullScoreboard.ValidUntil;
        }

        public void SetLabScoreboard(int labId, Scoreboard labScoreboard)
        {
            _labScoreboards[labId] = labScoreboard;
        }

        public void SetFullScoreboard(Scoreboard scoreboard)
        {
            _fullScoreboard = scoreboard;
        }

        public void InvalidateAll()
        {
            _labScoreboards.Clear();
            _fullScoreboard = null;
        }
    }
}
