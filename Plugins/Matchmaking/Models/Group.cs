using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Matchmaking
{
    public class Group
    {
        public Group() { }

        public List<Player> Players { get; } = new List<Player>();

        public object GroupData { get; set; }

        public DateTime CreationTimeUtc { get; } = DateTime.UtcNow;
    }
}
