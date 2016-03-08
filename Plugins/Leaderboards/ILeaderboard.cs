using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Leaderboards
{
    public interface ILeaderboardsService
    {
        Task<IEnumerable<Leaderboard>> ListLeaderboards();
        Task<Leaderboard> GetLeaderboard(string leaderboard);
        Task<Leaderboard> CreateOrUpdateLeaderboard(string leaderboard, string description);
        Task DeleteLeaderboard(string leaderboard);
    }
}
