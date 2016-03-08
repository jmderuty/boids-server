using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Server.Database;

namespace Server.Leaderboards
{
    class LeaderboardsService : ILeaderboardsService
    {
        private IESClientFactory _factory;
        private string index = Constants.INDEX;
        public LeaderboardsService(IESClientFactory esFactory)
        {
            _factory = esFactory;
        }

        public async Task<IEnumerable<Leaderboard>> ListLeaderboards()
        {
            var client = await _factory.CreateClient(index);
            var results = await client.SearchAsync<LeaderboardRecord>(sd => sd);

            if (!results.IsValid)
            {
                throw new InvalidOperationException(string.Format("Failed to perform ListLeaderboards query : '{0}'", results.ServerError));
            }

            return results.Hits.Select(h => new Leaderboard(h.Source, client));
        }

        public async Task<Leaderboard> GetLeaderboard(string leaderboard)
        {
            var client = await _factory.CreateClient(index);
            var result = await client.GetAsync<LeaderboardRecord>(sd => sd.Id(leaderboard));

            if (!result.IsValid)
            {
                throw new InvalidOperationException(string.Format("Failed to perform GetLeaderboard query : '{0}'", result.ServerError));
            }
            else
            {
                return new Leaderboard(result.Source, client);
            }
        }

        public async Task<Leaderboard> CreateOrUpdateLeaderboard(string leaderboard, string description)
        {
            var client = await _factory.CreateClient(index);
            var result = await client.IndexAsync(new LeaderboardRecord { Id = leaderboard, Description = description, Name = leaderboard });

            if (!result.IsValid)
            {
                throw new InvalidOperationException(string.Format("Failed to perform CreateOrUpdateLeaderboard query : '{0}'", result.ServerError));
            }
            else
            {
                return await GetLeaderboard(leaderboard);
            }
        }

        public async Task DeleteLeaderboard(string leaderboard)
        {
            var client = await _factory.CreateClient(index);
            var result = await client.DeleteByQueryAsync<ScoreRecord>(ds => ds.Query(qd => qd.Term(s => s.leaderboard, leaderboard)));

            if(!result.IsValid)
            {
                throw new InvalidOperationException(string.Format("Failed to perform DeleteLeaderboard query : '{0}'", result.ServerError));
            }

            result = await client.DeleteAsync<LeaderboardRecord>(ds => ds.Id(leaderboard));

            if (!result.IsValid)
            {
                throw new InvalidOperationException(string.Format("Failed to perform DeleteLeaderboard query : '{0}'", result.ServerError));
            }
        }
    }
}
