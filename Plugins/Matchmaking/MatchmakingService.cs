using Server.Users;
using Stormancer.Configuration;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Components;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Stormancer.Matchmaking
{
    public class MatchmakingService : IMatchmakingService
    {
        private const string UPDATE_NOTIFICATION_ROUTE = "match.update";
        private const string UPDATE_READYCHECK_ROUTE = "match.ready.update";
        private const string UPDATE_FINDMATCH_REQUEST_PARAMS_ROUTE = "match.parameters.update";

        private ISceneHost _matchmakingScene;

        private readonly IEnumerable<IMatchmakingDataExtractor> _extractors;
        private readonly IMatchmaker _matchmaker;
        private readonly IMatchmakingResolver _resolver;
        private readonly ILogger _logger;
        // private readonly MatchmakingPeerService _peerService;

        private readonly IUserSessions _sessions;
        //private ApplicationClient _applicationManagementClient;

        //        private readonly ConcurrentDictionary<IMatchmakingContext, System.Reactive.Unit> _waitingClients = new ConcurrentDictionary<IMatchmakingContext, System.Reactive.Unit>();
        private readonly ConcurrentDictionary<Group, MatchmakingRequestState> _waitingGroups = new ConcurrentDictionary<Group, MatchmakingRequestState>();
        private readonly ConcurrentDictionary<string, Group> _usersToGroup = new ConcurrentDictionary<string, Group>();
        private Task _runningMatch;
        private bool _isRunning = false;
        private readonly ISceneHost _scene;

        //private ulong _currentIndex = 0;

        private TimeSpan _period;

        public MatchmakingService(IEnumerable<IMatchmakingDataExtractor> extractors,
            IMatchmaker matchmaker,
            IMatchmakingResolver resolver,
                    IUserSessions sessions,
            //   MatchmakingPeerService peerService,
            ILogger logger, ISceneHost scene)
        {
            this._extractors = extractors;
            this._matchmaker = matchmaker;
            this._resolver = resolver;
            this._sessions = sessions;
            _logger = logger;
            this._scene = scene;
            //     _peerService = peerService;
        }

        #region IConfigurationRefresh
        void IConfigurationRefresh.Init(dynamic config)
        {
            ApplyConfig(config);
        }


        void IConfigurationRefresh.ConfigChanged(dynamic newConfig)
        {
            ApplyConfig(newConfig);
        }


        private void ApplyConfig(dynamic config)
        {
            _period = TimeSpan.FromSeconds((double)(config.matchmaking.interval));
        }

        #endregion



        public void Init(ISceneHost matchmakingScene)
        {
            _logger.Log(LogLevel.Trace, "matchmaker", "Initializing the MatchmakingService.", new { extractors = _extractors.Select(e => e.GetType().ToString()) });

            if (this._matchmakingScene != null)
            {
                throw new InvalidOperationException("The matchmaking service may only be initialized once.");
            }

            this._matchmakingScene = matchmakingScene;

            try
            {
                var configService = this._matchmakingScene.GetComponent<ConfigurationService>();

                configService.RegisterComponent(this);
                configService.RegisterComponent(_matchmaker);
                configService.RegisterComponent(_resolver);


                this._isRunning = true;
                this._runningMatch = this.Match();
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "matchmaker", "An error occured while initializing the MatchmakingService.", ex);
                throw;
            }
        }

        public async Task FindMatch(RequestContext<IScenePeerClient> request)
        {
            var group = new Group();
            var provider = request.ReadObject<string>();

            var currentUser = await _sessions.GetUser(request.RemotePeer);
            foreach (var extractor in _extractors)
            {
                if (await extractor.ExtractData(provider, request, group))
                {
                    break;
                }
            }
            foreach(var p in group.Players)
            {
                if(_usersToGroup.ContainsKey(p.UserId))
                {
                    throw new ClientException($"'{p.UserId} is already waiting for a match.");
                }
            }
            var state = new MatchmakingRequestState(group);

            _waitingGroups[group] = state;
            foreach(var user in group.Players)
            {
                _usersToGroup[user.UserId] = group;
            }
            request.CancellationToken.Register(() =>
            {


                state.Tcs.TrySetCanceled();
            });
            await BroadcastToPlayers(group, UPDATE_FINDMATCH_REQUEST_PARAMS_ROUTE, (s, sz) =>
             {
                 request.InputStream.Seek(0, System.IO.SeekOrigin.Begin);
                 request.InputStream.CopyTo(s);
             });
            await BroadcastToPlayers(group, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                        {
                            s.WriteByte((byte)MatchmakingStatusUpdate.SearchStart);
                            
                        });
            state.State = RequestState.Ready;

            IMatchResolverContext resolutionContext;
            try
            {
                resolutionContext = await state.Tcs.Task;
            }
            catch (TaskCanceledException)
            {
                await BroadcastToPlayers(group, UPDATE_NOTIFICATION_ROUTE, (s, sz) => s.WriteByte((byte)MatchmakingStatusUpdate.Cancelled));

            }
            finally //Always remove group from list.
            {
                MatchmakingRequestState _;
                foreach(var player in group.Players)
                {
                    Group grp1;
                    _usersToGroup.TryRemove(player.UserId, out grp1);
                }
                _waitingGroups.TryRemove(group, out _);
                if(_.Candidate != null)
                {
                    MatchReadyCheck rc;

                    if(_pendingReadyChecks.TryGetValue(_.Candidate.Id, out rc))
                    {
                        if (!rc.RanToCompletion)
                        {
                            rc.Cancel(currentUser.Id);
                        }
                    }
                }
            }


        }


        private async Task Match()
        {
            _logger.Log(LogLevel.Trace, "matchmaker", "Running matchmaking loop", new { period = _period });
            while (this._isRunning)
            {
                try
                {
                    await this.MatchOnce();
                }
                catch (Exception e)
                {
                    _logger.Log(LogLevel.Error, "matchmaker", "An error occurred while running a matchmaking.", e);
                }
                await Task.Delay(this._period);
            }

            _logger.Log(LogLevel.Trace, "matchmaker", "Exiting matchmaking loop", new { period = _period });
        }

        private async Task MatchOnce()
        {
            var waitingClients = _waitingGroups.Where(kvp => kvp.Value.State == RequestState.Ready).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            foreach (var value in waitingClients.Values)
            {
                value.State = RequestState.Searching;
                value.Candidate = null;
            }

            var matches = await this._matchmaker.FindMatches(waitingClients.Keys);


            if (matches.Matches.Any())
            {
                await _resolver.PrepareMatchResolution(matches);
            }


            foreach (var match in matches.Matches)
            {
                foreach (var group in match.Teams.SelectMany(t => t.Groups)) //Set match found to prevent players from being matched again
                {
                    var state = waitingClients[group];
                    state.State = RequestState.Found;
                    state.Candidate = match;
                }


                var _ = ResolveMatchFound(match, waitingClients);//Resolve match, but don't wait for completion.


            }

            foreach (var value in waitingClients.Values.Where(v => v.State == RequestState.Searching))
            {
                value.State = RequestState.Ready;
            }
        }

        public async Task Stop()
        {
            this._isRunning = false;
            try
            {
                await this._runningMatch;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "matchmaker", "Matchmaker encountered an exception.", ex);
                throw;
            }
        }
        private bool IsReadyCheckEnabled
        {
            get
            {
                return true;
            }
        }
        private int ReadyCheckTimeout
        {
            get
            {
                return 10 * 1000;//10 secondes
            }
        }
        private async Task ResolveMatchFound(Match match, Dictionary<Group, MatchmakingRequestState> waitingClients)
        {
            var resolverCtx = new MatchResolverContext(match);
            await _resolver.ResolveMatch(resolverCtx);


            if (IsReadyCheckEnabled)
            {
                await BroadcastToPlayers(match, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                {
                    var writerContext = new MatchmakingResolutionWriterContext(sz, s);
                    s.WriteByte((byte)MatchmakingStatusUpdate.WaitingPlayersReady);

                });

                using (var matchReadyCheckState = CreateReadyCheck(match))
                {
                    matchReadyCheckState.StateChanged += update =>
                    {
                        BroadcastToPlayers(match, UPDATE_READYCHECK_ROUTE, (s, sz) =>
                         {
                             sz.Serialize(update, s);
                         });
                    };
                    var result = await matchReadyCheckState.WhenCompleteAsync();

                    if (!result.Success)
                    {
                        foreach (var group in result.UnreadyGroups)//Cancel matchmaking for timeouted groups
                        {
                            MatchmakingRequestState mrs;
                            if (_waitingGroups.TryGetValue(group, out mrs))
                            {
                                mrs.Tcs.TrySetCanceled();
                            }
                        }
                        foreach (var group in result.ReadyGroups)//Put ready groups back in queue.
                        {
                            MatchmakingRequestState mrs;
                            if (_waitingGroups.TryGetValue(group, out mrs))
                            {
                                mrs.State = RequestState.Ready;
                                await BroadcastToPlayers(group, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                                {
                                    s.WriteByte((byte)MatchmakingStatusUpdate.SearchStart);
                                });

                            }
                        }
                        return;//stop here
                    }
                    else
                    {

                    }
                }
            }

            await BroadcastToPlayers(match, UPDATE_NOTIFICATION_ROUTE, (s, sz) =>
                {
                    var writerContext = new MatchmakingResolutionWriterContext(sz, s);
                    s.WriteByte((byte)MatchmakingStatusUpdate.Success);
                    if (resolverCtx.ResolutionAction != null)
                    {
                        resolverCtx.ResolutionAction(writerContext);
                    }
                });

            foreach (var group in match.Teams.SelectMany(t => t.Groups))//Complete requests
            {


                var state = waitingClients[group];
                state.Tcs.TrySetResult(resolverCtx);
            }
        }

        private ConcurrentDictionary<string, MatchReadyCheck> _pendingReadyChecks = new ConcurrentDictionary<string, MatchReadyCheck>();

        private MatchReadyCheck CreateReadyCheck(Match match)
        {
            var readyCheck = new MatchReadyCheck(ReadyCheckTimeout, () => CloseReadyCheck(match.Id), match);
            
            _pendingReadyChecks.TryAdd(match.Id, readyCheck);
            return readyCheck;
        }
        private void CloseReadyCheck(string id)
        {
            MatchReadyCheck _;
            _pendingReadyChecks.TryRemove(id, out _);
        }
        private MatchReadyCheck GetReadyCheck(User user)
        {
            Group g;
            if(_usersToGroup.TryGetValue(user.Id,out g))
            {
                var matchMakingRq = _waitingGroups[g];
                var matchCandidate = _waitingGroups[g].Candidate;
                if(matchCandidate == null)
                {
                    return null;
                }
                return GetReadyCheck(matchCandidate.Id);
            }
            return null;
        }
        private MatchReadyCheck GetReadyCheck(string matchId)
        {
            MatchReadyCheck check;
            if (_pendingReadyChecks.TryGetValue(matchId, out check))
            {
                return check;
            }
            else
            {
                return null;
            }
        }
        public async void ResolveReadyRequest(Packet<IScenePeerClient> packet)
        {

            var user = await _sessions.GetUser(packet.Connection);
            if (user == null)//User not authenticated
            {
                return;
            }

            var accepts = packet.ReadObject<bool>();
           

            var check = GetReadyCheck(user);
            if (check == null)
            {
                return;
            }
            if (!check.ContainsPlayer(user.Id))
            {
                return;
            }

            check.ResolvePlayer(user.Id, accepts);
        }

        public async void CancelMatch(Packet<IScenePeerClient> packet)
        {
            await CancelMatch(packet.Connection);

        }

        public async Task CancelMatch(IScenePeerClient peer)
        {
            var user = await _sessions.GetUser(peer);
            if (user == null)//User not authenticated
            {
                return;
            }
            Group group;
            if (!_usersToGroup.TryGetValue(user.Id, out group))
            {
                return;
            }

            MatchmakingRequestState mmrs;
            if (!_waitingGroups.TryGetValue(group, out mmrs))
            {
                return;
            }

            mmrs.Tcs.TrySetCanceled();
        }

        private Task<IScenePeerClient> GetPlayer(Player member)
        {
            return _sessions.GetPeer(member.UserId);
        }

        private async Task<IEnumerable<IScenePeerClient>> GetPlayers(Group group)
        {
            return await Task.WhenAll(group.Players.Select(GetPlayer));
        }

        private async Task<IEnumerable<IScenePeerClient>> GetPlayers(params Group[] groups)
        {
            return await Task.WhenAll(groups.SelectMany(g => g.Players).Select(GetPlayer));
        }

        private Task BroadcastToPlayers(Match match, string route, Action<System.IO.Stream, ISerializer> writer)
        {
            return BroadcastToPlayers(match.Teams.SelectMany(t => t.Groups), route, writer);
        }
        private Task BroadcastToPlayers(Group group, string route, Action<System.IO.Stream, ISerializer> writer)
        {
            return BroadcastToPlayers(new Group[] { group }, route, writer);
        }

        private async Task BroadcastToPlayers(IEnumerable<Group> groups, string route, Action<System.IO.Stream, ISerializer> writer)
        {
            var peers = await GetPlayers(groups.ToArray());
            foreach (var group in peers.Where(p => p != null).GroupBy(p => p.Serializer()))
            {
                _scene.Send(new MatchArrayFilter(group), route, s => writer(s, group.Key), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
            }
        }


        //private class MatchmakingContext : IMatchmakingContext
        //{
        //    private TaskCompletionSource<bool> _tcs;

        //    public MatchmakingContext(RequestContext<IScenePeerClient> request, TaskCompletionSource<bool> tcs, Group data)
        //    {
        //        _tcs = tcs;
        //        Request = request;
        //        Group = data;
        //        CreationTimeUTC = DateTime.UtcNow;
        //    }

        //    public DateTime CreationTimeUTC { get; }

        //    public Group Group { get; set; }


        //    public bool Rejected { get; private set; }
        //    public object RejectionData { get; private set; }

        //    /// <summary>
        //    /// Write the data sent to all the player when matchmaking completes (success or failure)
        //    /// </summary>
        //    public Action<System.IO.Stream, ISerializer> ResolutionWriter { get; set; }
        //    public bool MatchFound { get; private set; }
        //    public object MatchFoundData { get; private set; }


        //    public RequestContext<IScenePeerClient> Request { get; }

        //    public void Fail(object failureData)
        //    {

        //        if (IsResolved)
        //        {
        //            throw new InvalidOperationException("This matchmaking context has already been resolved.");
        //        }
        //        Rejected = true;
        //        RejectionData = failureData;
        //        _tcs.SetResult(false);
        //    }

        //    public void Success(object successData)
        //    {
        //        if (IsResolved)
        //        {
        //            throw new InvalidOperationException("This matchmaking context has already been resolved.");
        //        }
        //        MatchFound = true;
        //        MatchFoundData = successData;
        //        _tcs.SetResult(true);
        //    }

        //    public bool IsResolved
        //    {
        //        get
        //        {
        //            return MatchFound || Rejected;
        //        }
        //    }
        //}

        private class MatchResolverContext : IMatchResolverContext
        {
            public MatchResolverContext(Match match)
            {
                Match = match;
            }

            public Match Match { get; }

            public Action<IMatchmakingResolutionWriterContext> ResolutionAction { get; set; }
        }

        private class MatchmakingResolutionWriterContext : IMatchmakingResolutionWriterContext
        {
            private readonly Stream _stream;

            public MatchmakingResolutionWriterContext(ISerializer serializer, Stream stream)
            {

                Serializer = serializer;
                _stream = stream;
            }



            public ISerializer Serializer { get; }

            public void WriteObjectToStream<T>(T data)
            {
                Serializer.Serialize(data, _stream);
            }

            public void WriteToStream(Action<Stream> writer)
            {
                writer(_stream);
            }
        }

        private class MatchmakingRequestState
        {
            public MatchmakingRequestState(Group group)
            {
                Group = group;
            }

            public TaskCompletionSource<IMatchResolverContext> Tcs { get; } = new TaskCompletionSource<IMatchResolverContext>();

            public RequestState State { get; set; } = RequestState.NotStarted;

            public Group Group { get; }

            public Match Candidate { get; set; }
        }

        private enum RequestState
        {
            NotStarted,
            Ready,
            Searching,
            Found,
            Validated,
            Rejected
        }
    }
}



