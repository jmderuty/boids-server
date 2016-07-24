using Newtonsoft.Json.Linq;
using Stormancer.Management.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    static class ManagementClientExtensions
    {
        public static async Task CreatePersistentIfNotExists(this ApplicationClient client, string templateName, string sceneId, bool isPublic = true, JObject metadata = null )
        {
            var scene = await client.GetScene(sceneId);

            if(scene == null)
            {
                await client.CreateScene(sceneId, templateName, isPublic, metadata, true);
            }
        }
    }
}
