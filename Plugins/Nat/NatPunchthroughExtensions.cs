using Server.Plugins.Nat;
using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    public static class NatPunchthroughExtensions
    {
        public static void AddNatPunchthrough(this ISceneHost scene)
        {
            scene.Metadata[NatPunchthroughPlugin.METADATA_KEY] = "enabled";
        }
    }
}
