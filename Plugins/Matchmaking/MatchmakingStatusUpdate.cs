using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Matchmaking
{
    public enum MatchmakingStatusUpdate : byte
    {
        SearchStart = 0,
        CandidateFound = 1,
        WaitingPlayersReady = 2,
        Success = 3,
        Failed = 4,
        Cancelled = 5
    }
}
