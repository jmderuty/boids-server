using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Server.Users;

namespace Stormancer.Matchmaking
{
    class MatchReadyCheck : IDisposable
    {
        public MatchReadyCheck(int timeout, Action closeReadyCheck, Match match)
        {
            TimeoutTask = Task.Delay(timeout);
            _startDate = DateTime.UtcNow;
            _timeout = timeout;
            _closeReadyCheck = closeReadyCheck;
            PlayersReadyState = new Dictionary<string, Readiness>();
            foreach (var player in match.AllPlayers)
            {
                PlayersReadyState[player.UserId] = Readiness.Unknown;
            }
        }
        private int _timeout;
        private DateTime _startDate;
        private Action _closeReadyCheck;

        public Match Match { get; set; }

        public Dictionary<string, Readiness> PlayersReadyState
        {
            get; private set;
        }

        public Task TimeoutTask { get; private set; }

        public bool IsSuccess { get; private set; }

        public bool RanToCompletion { get; private set; }

        private TaskCompletionSource<MatchReadyCheckResult> _tcs = new TaskCompletionSource<MatchReadyCheckResult>();
        public Task<MatchReadyCheckResult> WhenCompleteAsync()
        {
            return _tcs.Task;

        }

        internal bool ContainsPlayer(string uId)
        {
            return PlayersReadyState.ContainsKey(uId);
        }

        internal void ResolvePlayer(string id, bool accepts)
        {
            if (!PlayersReadyState.ContainsKey(id))
            {
                return;
            }

            PlayersReadyState[id] = accepts ? Readiness.Ready : Readiness.NotReady;

            RaiseStateChanged();


            if (GlobalState != Readiness.Unknown)
            {
                ResolveReadyPhase();
            }
        }

        private void ResolveReadyPhase()
        {
            var globalState = GlobalState;
            if (globalState == Readiness.Unknown)
            {
                return;
            }
            else if (globalState == Readiness.Ready)
            {
                _tcs.TrySetResult(new MatchReadyCheckResult(true, Enumerable.Empty<Group>(), Match.AllGroups));
            }
            else
            {
                var unReadyGroupList = new List<Group>();
                var readyGroupList = new List<Group>();
                foreach(var group in Match.AllGroups)
                {
                    if(group.Players.Any(p=>PlayersReadyState[p.UserId] == Readiness.NotReady))
                    {
                        unReadyGroupList.Add(group);
                    }
                    else
                    {
                        readyGroupList.Add(group);
                    }
                }
                _tcs.TrySetResult(new MatchReadyCheckResult(false, unReadyGroupList, readyGroupList));
            }
        }

        public Readiness GlobalState
        {
            get
            {
                var checkState = Readiness.Unknown;
                if (PlayersReadyState.Values.Any(r => r == Readiness.NotReady))
                {
                    checkState = Readiness.NotReady;
                }
                if (PlayersReadyState.Values.All(r => r == Readiness.Ready))
                {
                    checkState = Readiness.Ready;
                }
                return checkState;
            }
        }
        private void RaiseStateChanged()
        {
            var state = new ReadyVerificationRequest()
            {
                matchId = Match.Id,
                members = PlayersReadyState,
                timeout = _timeout - (int)(DateTime.UtcNow - _startDate).TotalMilliseconds
            };
            var e = StateChanged;
            if (e != null)
            {
                e(state);
            }

        }

        public void Dispose()
        {
            _closeReadyCheck();
        }

        internal Action<ReadyVerificationRequest> StateChanged { get; set; }

        internal void Cancel(string id)
        {
            ResolvePlayer(id, false);
        }
    }

    class MatchReadyCheckResult
    {
        public MatchReadyCheckResult(bool success, IEnumerable<Group> timeoutedGroups, IEnumerable<Group> readyGroups)
        {
            Success = success;
            UnreadyGroups = timeoutedGroups;
            ReadyGroups = readyGroups;
        }
        public bool Success { get; private set; }

        public IEnumerable<Group> UnreadyGroups { get; private set; }
        public IEnumerable<Group> ReadyGroups { get; private set; }

    }
}
