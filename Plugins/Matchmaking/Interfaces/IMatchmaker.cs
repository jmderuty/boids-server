using Stormancer.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Matchmaking
{
    public interface IMatchmaker : IConfigurationRefresh
    {
        Task<MatchmakingResult> FindMatches(IEnumerable<Group> candidates);
    }
}