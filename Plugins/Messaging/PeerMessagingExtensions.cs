using Stormancer.Core;
using Stormancer.Plugins.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    public static class PeerMessagingExtensions
    {
        public static void AddPeerMessaging(this ISceneHost host)
        {
            host.Metadata[PeerMessagingPlugin.METADATA_KEY] = "enabled";
        }
    }
}
