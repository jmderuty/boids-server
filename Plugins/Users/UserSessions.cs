using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer;
using Server.Plugins.Database;
using Stormancer.Core;
using Stormancer.Diagnostics;

namespace Server.Users
{
    public interface IUserPeerIndex : IIndex<long> { }
    internal class UserPeerIndex : InMemoryIndex<long>, IUserPeerIndex { }

    public interface IPeerUserIndex : IIndex<User> { }
    internal class PeerUserIndex : InMemoryIndex<User>, IPeerUserIndex { }

    public class UserSessions : IUserSessions
    {
        private readonly IUserPeerIndex _userPeerIndex;
        private readonly IUserService _userService;
        private readonly IPeerUserIndex _peerUserIndex;
        private readonly IEnumerable<IUserSessionEventHandler> _eventHandlers;
        private readonly ISceneHost _scene;
        private readonly ILogger logger;

        private void RunEventHandler<T>(IEnumerable<T> eh, Action<T> action)
        {
            foreach (var h in eh)
            {
                action(h);
            }
        }
        public UserSessions(IUserService userService,
            IPeerUserIndex peerUserIndex,
            IUserPeerIndex userPeerIndex,
            IEnumerable<IUserSessionEventHandler> eventHandlers, ISceneHost scene, ILogger logger)
        {
            _userService = userService;
            _peerUserIndex = peerUserIndex;
            _userPeerIndex = userPeerIndex;
            _eventHandlers = eventHandlers;
            _scene = scene;
            this.logger = logger;
        }

        public async Task<User> GetUser(IScenePeerClient peer)
        {
            var result = await _peerUserIndex.TryGet(peer.Id.ToString());
            if (result.Success)
            {

                return result.Value;
            }
            else
            {
                return null;
            }
        }

        public async Task<bool> IsAuthenticated(IScenePeerClient peer)
        {
            return (await GetUser(peer)) != null;
        }

        public async Task LogOut(IScenePeerClient peer)
        {
            var result = await _peerUserIndex.TryRemove(peer.Id.ToString());
            if (result.Success)
            {
                await _userPeerIndex.TryRemove(result.Value.Id);
                RunEventHandler(_eventHandlers, h => h.OnLoggedOut(peer, result.Value));
                logger.Trace("usersessions", $"removed '{result.Value.Id}' (peer : '{peer.Id}') in scene '{_scene.Id}'.");
            }
          

        }

        public async Task SetUser(IScenePeerClient peer, User user)
        {
            if (peer == null)
            {
                throw new ArgumentNullException("peer");
            }
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            await _userPeerIndex.TryAdd(user.Id, peer.Id);
            await _peerUserIndex.TryAdd(peer.Id.ToString(), user);
            RunEventHandler(_eventHandlers, h => h.OnLoggedIn(peer, user));
            logger.Trace("usersessions", $"Added '{user.Id}' (peer : '{peer.Id}') in scene '{_scene.Id}'.");
        }


        public async Task UpdateUserData<T>(IScenePeerClient peer, T data)
        {
            var user = await GetUser(peer);
            if (user == null)
            {
                throw new InvalidOperationException("User not found.");

            }
            else
            {
                user.UserData = Newtonsoft.Json.Linq.JObject.FromObject(data);
                await _userService.UpdateUserData(user.Id, data);
            }
        }

        public async Task<IScenePeerClient> GetPeer(string userId)
        {
            var result = await _userPeerIndex.TryGet(userId);

            if (result.Success)
            {

                var peer = _scene.RemotePeers.FirstOrDefault(p => p.Id == result.Value);
                //logger.Trace("usersessions", $"found '{userId}' (peer : '{result.Value}', '{peer.Id}') in scene '{_scene.Id}'.");
                if (peer == null)
                {
                    logger.Trace("usersessions", $"didn't found '{userId}' (peer : '{result.Value}') in scene '{_scene.Id}'.");
                }
                return peer;
            }
            else
            {
                logger.Trace("usersessions", $"didn't found '{userId}' in userpeer index.");
                return null;
            }
        }
    }
}
