using Stormancer.Core;
using Stormancer.Plugins.ClientLogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    public static class ClientLogsExtensions
    {
        public static void AddLogsServer(this ISceneHost scene)
        {
            scene.Metadata[ClientLogsPlugin.METADATA_KEY] = "enabled";
        }
    }
}
