using Server.Management;
using Stormancer.Plugins;
using Stormancer.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer;
namespace Server
{
    class GamePlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostStarting += host=>host.AddGameScene();
         
            ctx.HostStarted += OnHostStarted;
        }

        private void OnHostStarted(IHost host)
        {

            host.DependencyResolver.Resolve<ManagementClientAccessor>().GetApplicationClient().
                ContinueWith(t => t.Result.CreatePersistentIfNotExists("game", "main"));
        }

    }
}
