using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Plugins.ClientLogs
{
    public class ClientLogsPlugin : IHostPlugin
    {
        public const string METADATA_KEY = "stormancer.clientLogs";
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<LogsController>().InstancePerRequest();
            };
            ctx.SceneCreated+= (ISceneHost scene) =>
               {
                   if(scene.Metadata.ContainsKey(METADATA_KEY))
                   {
                       scene.AddController<LogsController>();
                   }
               };
        }
    }
}
