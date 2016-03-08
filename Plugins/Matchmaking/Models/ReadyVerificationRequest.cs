using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Matchmaking
{
    public enum Readiness
    {
        Unknown = 0,
        Ready = 1,
        NotReady = 2
    }
    public class ReadyVerificationRequest
    {
        public Dictionary<string,Readiness> members;
        public string matchId;
        
        public int timeout;
    }

  
}
