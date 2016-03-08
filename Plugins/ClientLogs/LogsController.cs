using Newtonsoft.Json.Linq;
using Server;
using Server.Plugins.API;
using Stormancer;
using Stormancer.Core;
using Stormancer.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Plugins.ClientLogs
{
   
    public class LogsController : ControllerBase
    {
        private readonly ILogger _logger;
        public LogsController(ILogger logger)
        {
            _logger = logger;
        }

        public void Push(Packet<IScenePeerClient> packet)
        {
            
            var log = packet.ReadObject<LogPushDto>();
            var data = JToken.Parse(log.Data);
            JObject jData = data as JObject;
            if (jData == null)
            {
                jData = new JObject();
                jData["data"] = data;
            }
            _logger.Log(log.Level, log.Category, log.Message, jData);
        }
    }
}
