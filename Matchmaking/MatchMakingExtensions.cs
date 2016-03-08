using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer;

namespace Server.Matchmaking
{
    internal static class MatchmakingExtensions
    {
        public static MatchmakingBuilder Matchmaker(this IAppBuilder builder, string templateId = "matchmaker")
        {
            var config = new MatchmakingBuilder();
            builder.SceneTemplate(templateId, scene => {
                

            }, new Dictionary<string, string> { { "description", "matchmaker" } });
            return config;
        }
    }
}
