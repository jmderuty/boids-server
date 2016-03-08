using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Matchmaking
{
    public interface IMatchmakingConfig
    {
        string MatchmakingSceneId { get; set; }

        string Kind { get; set; }

        void RegisterDependencies(IDependencyBuilder dependencyResolver);
    }
}
