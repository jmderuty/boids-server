using Server.Plugins.API;
using Server.Plugins.Database;
using Server.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer;
using Stormancer.Plugins;
using Stormancer.Diagnostics;

namespace Server.Plugins.Nat
{
    internal class NatIndex : InMemoryIndex<NatPunchthroughData> { }

    internal class NatUserSessionEventHandler : IUserSessionEventHandler
    {
        private readonly NatIndex _index;
        public NatUserSessionEventHandler(NatIndex index)
        {
            _index = index;
        }

        public void OnLoggedIn(IScenePeerClient client, User user)
        {

        }

        public void OnLoggedOut(IScenePeerClient client, User user)
        {
            _index.TryRemove(client.Id.ToString());
        }
    }

    class NatController : ControllerBase
    {
        private readonly NatIndex _natIndex;
        private readonly IUserSessions _userSessions;
        private readonly ILogger _logger;

        public NatController(NatIndex natIndex, IUserSessions userSessions, ILogger logger)
        {
            _logger = logger;
            _natIndex = natIndex;
            _userSessions = userSessions;
        }

        public async Task UpdateP2PData(RequestContext<IScenePeerClient> ctx)
        {
            var user = await _userSessions.GetUser(ctx.RemotePeer);

            if (user == null)
            {
                _logger.Trace("natTraversal", $"Failed to update nat traversal data: the peer '{ctx.RemotePeer.Id}' is not logged in.");
                throw new ClientException($"The peer '{ctx.RemotePeer.Id}' is not logged in.");
            }
            var p2p = ctx.ReadObject<Dictionary<string, string>>();
            var r = await _natIndex.AddOrUpdateWithRetries(ctx.RemotePeer.Id.ToString(), new NatPunchthroughData { P2PTransports = p2p }, d => d.SetP2P(p2p));
           

        }

        public async Task GetP2PData(RequestContext<IScenePeerClient> ctx)
        {
            var provider = ctx.ReadObject<string>();
            switch (provider)
            {
                case "userId":
                    {
                        var userId = ctx.ReadObject<string>();
                        var peer = await _userSessions.GetPeer(userId);
                        
                        if (peer == null)
                        {
                            throw new ClientException($"The user '{userId}' is not connected.");
                          
                        }
                      
                        var r = await _natIndex.TryGet(peer.Id.ToString());
                        if (!r.Success)
                        {
                           
                            throw new ClientException($"The user '{userId}' (peer : '{peer.Id}') is not available for p2p (no p2p data set).");
                        }
                        else
                        {
                            ctx.SendValue(r.Value.P2PTransports);
                        }
                        break;
                    }
                case "peerId":
                    {
                        var peerId = ctx.ReadObject<long>();
                        var r = await _natIndex.TryGet(peerId.ToString());
                        if (!r.Success)
                        {
                            ctx.SendValue(new Dictionary<string, string>());
                        }
                        else
                        {
                            ctx.SendValue(r.Value.P2PTransports);
                        }
                        break;
                    }
                default:
                    throw new ClientException($"Unknown provider '{provider}'.");
            }


        }

    }
    class NatPunchthroughData
    {
        public NatPunchthroughData SetP2P(Dictionary<string, string> p2p)
        {
            return new NatPunchthroughData { P2PTransports = p2p };
        }
        public Dictionary<string, string> P2PTransports;
    }

}
