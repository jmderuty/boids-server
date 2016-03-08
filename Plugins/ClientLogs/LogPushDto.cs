using MsgPack.Serialization;
using Newtonsoft.Json.Linq;
using Stormancer;
using Stormancer.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Plugins.ClientLogs
{
    public class LogPushDto
    {
        [MessagePackMember(0)]
        public string Category { get; set; }

        [MessagePackMember(1)]
        public string Data { get; set; }

        [MessagePackMember(2)]
        public LogLevel Level { get; set; }

        [MessagePackMember(3)]
        public string Message { get; set; }
    }
}
