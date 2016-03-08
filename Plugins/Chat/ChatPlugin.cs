using Stormancer.Core;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Plugins.Chat
{
    class ChatPlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "stormancer.chat";
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.SceneCreated += (ISceneHost scene) =>
              {

                  if(scene.Metadata.ContainsKey(METADATA_KEY))
                  {
                      new ChatServer(scene);
                  }
              };
        }
    }
}
