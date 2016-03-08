using Stormancer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class Player
    {
        public Player(PlayerInfos infos, long id)
        {
            IsObserver = infos.isObserver;
            Id = id;
        }
        public bool IsObserver { get; private set; }
        public ushort ShipId { get; internal set; }

        public long Id { get; private set; }
    }

    public class PlayerInfos
    {
        internal static PlayerInfos FromPeer(IScenePeerClient peer)
        {
            return peer.GetUserData<PlayerInfos>();
          //  return peer.Serializer().Deserialize<PlayerInfos>(new MemoryStream(peer.UserData));
        }
        public bool isObserver;

        public string userId { get; set; }

    }
  
}
