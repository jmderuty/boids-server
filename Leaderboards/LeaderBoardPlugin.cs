using Stormancer;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Server.Database;
using Stormancer.Server.Admin;

namespace Server.Leaderboards
{
    public struct LeaderboardDto
    {
        public string id;
        public string name;
        public string description;
        public LeaderboardScoreDto[] scores;
    };

    public struct LeaderboardScoreDto
    {
        public string leaderboard;
        public string userid;
        public string username;
        public int score;
    };

    class LeaderBoardPlugin : IHostPlugin
    {
        private IAdminPluginConfig _apis;
        public LeaderBoardPlugin(IAppBuilder builder)
        {
            _apis = builder.AdminPlugin("leaderboard", Stormancer.Server.Admin.AdminPluginHostVersion.V0_1).Name("Leaderboard");
        }

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostStarting += h =>
            {
                h.DependencyResolver.Register<ILeaderboardsService, LeaderboardsService>();

                _apis.Get["/"] =  _ =>
                    {
                        var service = h.DependencyResolver.Resolve<ILeaderboardsService>();
                        return service.ListLeaderboards().Result.Select(l => new LeaderboardDto { id = l.Name, name = l.Name, description = l.Description });
                    };

                _apis.Get["/{id:string}/{skip:int}/{take:int}"] = parameters =>
                    {
                        var service = h.DependencyResolver.Resolve<ILeaderboardsService>();
                        var id = (string)parameters.id;
                        var skip = (int)parameters.skip;
                        var take = (int)parameters.take;
                        var leaderboard = service.GetLeaderboard(id).Result;
                        var scores = leaderboard.GetScores(skip, take).Result;
                        var scores2 = scores.Select(s => new LeaderboardScoreDto { userid = s.UserId, username = s.Username, score = s.Value, leaderboard = s.Leaderboard });
                        return new LeaderboardDto { id = leaderboard.Name, name = leaderboard.Name, description = leaderboard.Description, scores = scores2.ToArray() };
                    };
            };
        }
    }

    public class Startup
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new LeaderBoardPlugin(builder));

        }
    }
}
