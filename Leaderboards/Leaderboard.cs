using System;
using System.Collections.Generic;
using Nest;
using Server.Database;
using System.Linq;
using System.Threading.Tasks;

namespace Server.Leaderboards
{
    /// <summary>
    /// A leaderboard object
    /// </summary>
    public class Leaderboard
    {
        private IElasticClient _client;
        internal Leaderboard(LeaderboardRecord record, IElasticClient client)
        {
            _client = client;
            Name = record.Name;
            Description = record.Description;
        }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public async Task SetScore(string userId, string username, int value)
        {
            var result = await _client.IndexAsync(new ScoreRecord
            {
                Id = this.Name + "-" + userId,
                leaderboard = this.Name,
                score = value,
                UserId = userId,
                Username = username
            });

            if (!result.IsValid)
            {
                throw new InvalidOperationException(string.Format("Failed to perform SetScore query : '{0}'", result.ServerError));
            }
        }

        public async Task IncrementScore(string userId,string username, int diff)
        {
            var result = await _client.UpdateAsync<ScoreRecord>(ud => ud.Script("ctx._source.score+=" + diff).Id(this.Name + "-" + userId));
            if (!result.IsValid)
            {
                await SetScore(userId, username, diff);
                
            }
          
        }

        public async Task<ScoreDto> GetScore(string userId)
        {
            var result = await _client.GetAsync<ScoreRecord>(this.Name + "-" + userId);

            if (!result.IsValid)
            {
                throw new InvalidOperationException(string.Format("Failed to perform GetScore query : '{0}'", result.ServerError));
            }
            else
            {
                return new ScoreDto(result.Source);
            }
        }

        public async Task<IEnumerable<ScoreDto>> GetScores(int skip, int take)
        {
            var results = await _client.SearchAsync<ScoreRecord>(sd => sd
                .Filter(f => f.Term(s => s.leaderboard, this.Name))
                .SortDescending(s => s.score)
                .Skip(skip)
                .Take(take)
                );
            if (!results.IsValid)
            {
                throw new InvalidOperationException(string.Format("Failed to perform GetScores query : '{0}'", results.ServerError));
            }
            else
            {
                return results.Hits.Select(r => new ScoreDto(r.Source));
            }
        }

        public async Task DeleteScore(string userId)
        {
            var result = await _client.DeleteAsync(this.Name + "-" + userId);
            if (!result.IsValid)
            {
                throw new InvalidOperationException(string.Format("Failed to perform DeleteScore query : '{0}'", result.ServerError));
            }
        }
    }

    public class ScoreDto
    {
        internal ScoreDto(ScoreRecord record)
        {
            Leaderboard = record.leaderboard;
            Username = record.Username;
            UserId = record.UserId;
            Value = record.score;
        }
        public string Leaderboard { get; private set; }
        public string UserId { get; private set; }
        public string Username { get; private set; }

        public int Value { get; private set; }
    }
}