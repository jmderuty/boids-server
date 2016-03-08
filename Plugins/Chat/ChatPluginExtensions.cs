using Stormancer.Core;
using Stormancer.Plugins.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    public static class ChatPluginExtensions
    {
        public static void AddChat(this ISceneHost scene )
        {
            scene.Metadata[ChatPlugin.METADATA_KEY] = "enabled";
        }
    }
}
