using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Matchmaking
{
    public class MatchmakingResult
    {
        public MatchmakingResult(){}

        public MatchmakingResult(IEnumerable<Match> matches)
        {
            Matches.AddRange(matches);
        }


        public List<Match> Matches { get; } = new List<Match>();
    }
}
