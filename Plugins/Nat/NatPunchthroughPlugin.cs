using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Server.Plugins.API;
using Stormancer.Server;
using Server.Users;

namespace Server.Plugins.Nat
{
    class NatPunchthroughPlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "stormancer.natPunchthrough";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
              {
                  builder.Register<Plugins.Nat.NatUserSessionEventHandler>().As<IUserSessionEventHandler>();
                  builder.Register<NatController>().InstancePerRequest();
                  builder.Register<NatIndex>().SingleInstance();
              };

            ctx.SceneCreated += (ISceneHost scene) =>
             {
                 if (scene.Metadata.ContainsKey(METADATA_KEY))
                 {
                     scene.AddController<NatController>();
                 }
             };
        }
    }
}
