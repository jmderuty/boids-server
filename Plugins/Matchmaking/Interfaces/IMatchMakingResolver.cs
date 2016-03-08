using Stormancer.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Matchmaking
{
    public interface IMatchmakingResolver : IConfigurationRefresh
    {
        Task PrepareMatchResolution(MatchmakingResult matchmakingResult);

        Task ResolveMatch(IMatchResolverContext matchCtx);
    }
}