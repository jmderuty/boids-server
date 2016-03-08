//using Server.Users;
//using Stormancer;
//using Stormancer.Core;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Stormancer.Server;

//namespace Stormancer.Matchmaking
//{
//    public class MatchmakingPeerService
//    {
//        private readonly IUserSessions _sessions;
//        private readonly ISceneHost _scene;

//        public MatchmakingPeerService(IUserSessions sessions, ISceneHost scene)
//        {
//            _scene = scene;
//            _sessions = sessions;
//        }

//        public Task<IScenePeerClient> GetPlayer(Player member)
//        {
//            return _sessions.GetPeer(member.UserId);
//        }

//        private async Task<IEnumerable<IScenePeerClient>> GetPlayers(Group group)
//        {
//            return await Task.WhenAll(group.Players.Select(GetPlayer));
//        }

//        public async Task<IEnumerable<IScenePeerClient>> GetPlayers(params Group[] groups)
//        {
//            return await Task.WhenAll(groups.SelectMany(g => g.Players).Select(GetPlayer));
//        }

//        public Task BroadcastToPlayers(Group group, string route, Action<System.IO.Stream, ISerializer> writer)
//        {
//            return BroadcastToPlayers(new Group[] { group }, route, writer);
//        }

//        public async Task BroadcastToPlayers(IEnumerable<Group> groups, string route, Action<System.IO.Stream, ISerializer> writer)
//        {
//            var peers = await GetPlayers(groups.ToArray());
//            foreach (var group in peers.Where(p => p != null).GroupBy(p => p.Serializer()))
//            {
//                _scene.Send(new MatchArrayFilter(group), route, s => writer(s, group.Key), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
//            }
//        }
//    }
//}

