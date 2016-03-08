using System.Collections.Generic;
using System.Linq;

namespace Stormancer.Matchmaking
{
    public class Team
    {
        public Team()
        {
        }

        public Team(params Group[] groups) : this(groups, null)
        {
        }

        public Team(IEnumerable<Group> groups, object customData = null)
        {
            CustomData = customData;
            Groups.AddRange(groups);
        }

        public object CustomData { get; set; }

        public List<Group> Groups { get; set; } = new List<Group>();

        public IEnumerable<Player> AllPlayers => Groups.SelectMany(g => g.Players);
    }
}