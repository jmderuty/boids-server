using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Matchmaking
{
    public static class MatchmakingConfig
    {
        public static IMatchmakingConfig Create<TExtractor, TMatchmaker, TResolver>(
            string sceneId,
            string kind
            )
            where TExtractor : class, IMatchmakingDataExtractor
            where TMatchmaker : class, IMatchmaker
            where TResolver : class, IMatchmakingResolver
        {
            return new MatchMakingConfigImpl<TExtractor, TMatchmaker, TResolver>(sceneId, kind);
        }


        private class MatchMakingConfigImpl<TExtractor, TMatchmaker, TResolver> : IMatchmakingConfig
            where TExtractor : class, IMatchmakingDataExtractor
            where TMatchmaker : class, IMatchmaker
            where TResolver : class, IMatchmakingResolver

        {
            public MatchMakingConfigImpl(
                string matchmakingSceneId,
                string matchmakingKind)
            {
                this.MatchmakingSceneId = matchmakingSceneId;
                this.Kind = matchmakingKind;
            }

            public string MatchmakingSceneId { get; set; }

            public string Kind { get; set; }

            public void RegisterDependencies(IDependencyBuilder builder)
            {
                builder.Register<TExtractor>().As<IMatchmakingDataExtractor>();
                builder.Register<TMatchmaker>().As<IMatchmaker>();
                builder.Register<TResolver>().As<IMatchmakingResolver>();
            }
        }
    }


}
