using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class ShipCreatedDto
    {
        public float x;
        public float rot;
        public float y;

        public ShipStatus status { get; set; }

        public ushort team { get; set; }
        public ushort id { get; set; }

        public Weapon[] weapons { get; set; }

        public long timestamp { get; set; }
    }
}
