using Stormancer.Core;
using Stormancer.Server.Components;
using System;

namespace Server
{
    internal class Ship
    {
        private GameScene _scene;
        public Ship(GameScene scene)
        {
            _scene = scene;
        }
        public Player player;
        public ushort id;

        public float x;
        public float y;

        public float rot;

        public int currentPv;

        public void ChangePv(int diff)
        {
            currentPv += diff;
            _scene.BroadcastPvUpdate(this.id, diff);
            if(currentPv <= 0)
            {
                UpdateStatus(ShipStatus.Dead);
            }
            else
            {
                UpdateStatus(ShipStatus.InGame);
            }
        }
        public int maxPv;

        public ushort team;

        public Weapon[] weapons { get; set; }

        public long PositionUpdatedOn { get; internal set; }

        public long lastStatusUpdate { get; set; }

        public void UpdateStatus(ShipStatus newStatus)
        {
            if (newStatus != Status)
            {
                Status = newStatus;

                _scene.BroadcastStatusChanged(this.id, this.Status);
            }
        }
        public ShipStatus Status { get; set; }
    }


    public enum ShipStatus
    {
        Waiting,
        InGame,
        Dead,
        GameComplete
    }
}