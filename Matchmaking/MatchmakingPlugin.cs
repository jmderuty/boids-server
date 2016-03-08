using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer;
using Stormancer.Plugins;
using Stormancer.Server;

namespace Server.Matchmaking
{
    public class Startup
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new MatchmakingPlugin());

        }
    }

    class MatchmakingPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostStarting += OnHostStarting;
        }

        private void OnHostStarting(IHost host)
        {
            host.AddSceneTemplate("matchmaker", scene => {

                scene.AddProcedure("match.find",async ctx => {
                    var managementClientFactory = scene.DependencyResolver.Resolve<Management.ManagementClientAccessor>();
                    var client = await managementClientFactory.GetApplicationClient();
                    var playersInfo = ctx.RemotePeer.GetUserData<PlayerInfos>();
                    //  var isPlayer = ctx.ReadObject<bool>();
                    var token = await client.CreateConnectionToken(Constants.GAME_NAME, playersInfo);

                    ctx.SendValue(new FindMatchResult { Token = token });
                });
            });
        }
    }
}
