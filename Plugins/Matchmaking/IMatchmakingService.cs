using System.Threading.Tasks;
using Stormancer.Plugins;
using Stormancer.Core;
using Stormancer.Configuration;

namespace Stormancer.Matchmaking
{
    public interface IMatchmakingService : IConfigurationRefresh
    {
      
        void Init(ISceneHost matchmakingScene);
        Task Stop();

        Task FindMatch(RequestContext<IScenePeerClient> request);
        void ResolveReadyRequest(Packet<IScenePeerClient> packet);
        void CancelMatch(Packet<IScenePeerClient> packet);
        Task CancelMatch(IScenePeerClient peer);
    }
}