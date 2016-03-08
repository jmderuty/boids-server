//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Stormancer.Matchmaking.Default
//{
//    public class DefaultMatchmaker : IMatchmaker
//    { 
//        private int _playersPerMatch;

//        #region IConfigurationRefresh
//        public void Init(dynamic config)
//        {
//            ConfigChanged(config); 
//        }

//        public void ConfigChanged(dynamic newConfig)
//        {
//            _playersPerMatch = (int)(newConfig.matchmaker.playerspermatch);
//        }
//        #endregion

//        public Task FindMatches(IEnumerable<IMatchmakingContext> candidates)
//        {
//            var candidatesQueue = new Queue<IMatchmakingContext>(candidates);

//            while(candidatesQueue.Count >= _playersPerMatch)
//            {
//                var group = new List<IMatchmakingContext>();

//                for(var i=0; i<_playersPerMatch; i++)
//                {
//                    group.Add(candidatesQueue.Dequeue());
//                }

//                var indices = group.Select(candidate => candidate.Group).ToList();

//                foreach(var candidate in group)
//                {
//                    candidate.Success(indices);
//                }
//            }

//            return Task.FromResult(true);
//        }
//    }
//}
