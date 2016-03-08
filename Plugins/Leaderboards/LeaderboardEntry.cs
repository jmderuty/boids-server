using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Leaderboards
{
    public class ScoreRecord
    {
        public string Id { get; set; }
        public string leaderboard { get; set; }

        public string Username { get; set; }

        public int score { get; set; }
        public string UserId { get; internal set; }
    }
    
    public class LeaderboardRecord
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public string Description { get; set; }
    }
}
