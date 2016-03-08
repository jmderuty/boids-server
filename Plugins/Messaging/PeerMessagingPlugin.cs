using Server.Plugins.Nat;
using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Plugins.Messaging
{
    
    class PeerMessagingPlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "stormancer.messaging";
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<MessagingController>().InstancePerRequest();
            };
            ctx.SceneCreated += (ISceneHost scene) =>
              {
                  if (scene.Metadata.ContainsKey(METADATA_KEY))
                  {
                      scene.AddController<MessagingController>();
                  }
              };
        }
    }
}
