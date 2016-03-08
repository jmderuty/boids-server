using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Matchmaking
{
    public class Match
    {
        #region constructors
        public Match(object customData = null)
            : this(0, customData)
        {
        }

        public Match(params Team[] teams) : this(teams, null)
        {
        }

        public Match(int teamCount, object customData = null) 
            : this(teamCount, () => new Team(), customData)
        {
        }

        public Match(int teamCount, Func<Team> teamFactory, object customData = null)
        {
            CustomData = customData;
            for(var i = 0; i< teamCount; i++)
            {
                Teams.Add(teamFactory());
            }
            Id = Guid.NewGuid().ToString();
        }

        public Match(IEnumerable<Team> teams, object customData = null)
        {
            CustomData = customData;
            Teams.AddRange(teams);
            Id = Guid.NewGuid().ToString();
        }
        #endregion
        public string Id { get; private set; }

        public object CustomData { get; set; }

        public object CommonCustomData { get; set; }

        public List<Team> Teams { get; set; } = new List<Team>();

        public IEnumerable<Group> AllGroups => Teams.SelectMany(team => team.Groups);

        public IEnumerable<Player> AllPlayers => Teams.SelectMany(team => team.AllPlayers);
    }
}
