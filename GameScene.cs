using Stormancer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer.Core;
using System.Collections.Concurrent;
using Stormancer.Diagnostics;
using System.Diagnostics;
using System.Threading;
using Stormancer.Plugins;
using System.IO;
using System.Reactive.Concurrency;
using Stormancer.Server.Components;

namespace Server
{
    static class GameSceneExtensions
    {
        public static void AddGameScene(this IAppBuilder builder)
        {
            builder.SceneTemplate("game", scene => new GameScene(scene));
        }
    }

    class GameScene
    {
        private const float X_MIN = -100;
        private const float X_MAX = 100;
        private const float Y_MIN = -100;
        private const float Y_MAX = 100;

        private readonly ISceneHost _scene;
        private ushort _currentId = 0;
        private ConcurrentDictionary<long, Player> _players = new ConcurrentDictionary<long, Player>();
        private ConcurrentDictionary<ushort, Ship> _ships = new ConcurrentDictionary<ushort, Ship>();
        private ConcurrentQueue<UsedSkillMsg> _skills = new ConcurrentQueue<UsedSkillMsg>();

        private bool isRunning = false;

        private long interval = 50;
        private long _deathCount = 0L;

        public GameScene(ISceneHost scene)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
            }
            _scene = scene;

            _scene.Connected.Add(OnConnected);
            _scene.Disconnected.Add(OnDisconnected);
            _scene.AddRoute("position.update", OnPositionUpdate);
            _scene.AddProcedure("getShipInfos", OnGetShipInfos);
            _scene.AddProcedure("ship.killCount", OnGetShipKillCount);
            _scene.AddProcedure("skill", UseSkill);
            _scene.Starting.Add(OnStarting);
            _scene.Shuttingdown.Add(OnShutdown);
        }

        private Task OnGetShipKillCount(RequestContext<IScenePeerClient> request)
        {
            request.SendValue(s =>
            {
                s.Write(BitConverter.GetBytes(this._deathCount), 0, 8);
                s.Write(BitConverter.GetBytes(this._scene.GetComponent<IEnvironment>().Clock), 0, 8);
            });

            return Task.FromResult(true);
        }

        private Task OnGetShipInfos(RequestContext<IScenePeerClient> arg)
        {

            var shipIds = arg.ReadObject<ushort[]>();

            var ships = new List<ShipCreatedDto>(shipIds.Length);
            foreach (var id in shipIds)
            {
                Ship ship;
                if (_ships.TryGetValue(id, out ship))
                {
                    ships.Add(new ShipCreatedDto { timestamp = _scene.GetComponent<IEnvironment>().Clock, id = ship.id, team = ship.team, x = ship.x, y = ship.y, rot = ship.rot, weapons = ship.weapons, status = ship.Status });
                }
            }

            arg.SendValue(ships);
            return Task.FromResult(true);

        }
        private async Task UseSkill(RequestContext<IScenePeerClient> args)
        {
            string errorMsg = null;
            if (!await UseSkillImpl(args))
            {
                args.SendValue(new UseSkillResponse { error = true, errorMsg = errorMsg });
            }
        }
        private async Task<bool> UseSkillImpl(RequestContext<IScenePeerClient> arg)
        {

            var env = _scene.GetComponent<IEnvironment>();
            var p = arg.ReadObject<UserSkillRequest>();
            var ship = _ships[_players[arg.RemotePeer.Id].ShipId];

            if (ship.Status != ShipStatus.InGame || ship.currentPv <= 0)
            {
                return false;
                //throw new ClientException("You can only use skills during games.");
            }

            var timestamp = _scene.GetComponent<IEnvironment>().Clock;
            var weapon = ship.weapons.FirstOrDefault(w => w.id == p.skillId);
            if (weapon == null)
            {
                return false;
                //throw new ClientException(string.Format("Skill '{0}' not available.", p.skillId));
            }

            if (weapon.fireTimestamp + weapon.coolDown > timestamp)
            {
                return false;
                //throw new ClientException("Skill in cooldown.");
            }
            if (!_ships.ContainsKey(p.target))
            {
                return false;
            }

            var target = _ships[p.target];
            if (target.Status != ShipStatus.InGame)
            {
                return false;
                //throw new ClientException("Can only use skills on ships that are in game.");
            }
            var dx = ship.x - target.x;
            var dy = ship.y - target.y;
            if (weapon.range * weapon.range < dx * dx + dy * dy)
            {
                return false;
                //throw new ClientException("Target out of range.");
            }

            weapon.fireTimestamp = timestamp;
            var success = _rand.Next(100) < weapon.precision * 100;
            if (success)
            {
                if (target.currentPv > 0)
                {
                    target.ChangePv(-weapon.damage);
                }
            }

            this.RegisterSkill(ship.id, target.id, success, weapon.id, timestamp);

            arg.SendValue(new UseSkillResponse { error = false, errorMsg = null, skillUpTimestamp = weapon.fireTimestamp + weapon.coolDown, success = success });
            return true;
        }

        private void RegisterSkill(ushort originId, ushort targetId, bool success, string weaponId, long timestamp)
        {
            this._skills.Enqueue(new UsedSkillMsg { origin = originId, shipId = targetId, success = success, weaponId = weaponId, timestamp = timestamp });
        }

        private Task OnStarting(dynamic arg)
        {
            StartUpdateLoop();
            return Task.FromResult(true);
        }

        private void StartUpdateLoop()
        {
            if (!isRunning)
            {
                isRunning = true;
                RunUpdate();
            }
        }

        private IDisposable _periodicUpdateTask;

        private IEnumerable<UsedSkillMsg> GetUsedSkills()
        {
            UsedSkillMsg result;
            while (this._skills.TryDequeue(out result))
            {
                yield return result;
            }
        }

        private void RunUpdate()
        {
            var env = _scene.GetComponent<IEnvironment>();
            long lastRun = 0;
            _scene.GetComponent<ILogger>().Info("gameScene", "Starting update loop");
            long lastLog = 0;
            var metrics = new ConcurrentDictionary<int, uint>();
            _periodicUpdateTask = DefaultScheduler.Instance.SchedulePeriodic(TimeSpan.FromMilliseconds(interval), () =>
            {
                try
                {
                    var current = env.Clock;

                    if (_scene.RemotePeers.Any())
                    {
                        if (_ships.Any(s => s.Value.PositionUpdatedOn > lastRun))
                        {
                            _scene.Broadcast("position.update", s =>
                            {
                                //var nb = 0;
                                foreach (var ship in _ships.Values.ToArray())
                                {
                                    if (ship.PositionUpdatedOn > lastRun && ship.Status == ShipStatus.InGame)
                                    {
                                        using (var writer = new BinaryWriter(s, Encoding.UTF8, true))
                                        {
                                            writer.Write(ship.id);
                                            writer.Write(ship.x);
                                            writer.Write(ship.y);
                                            writer.Write(ship.rot);
                                            writer.Write(ship.PositionUpdatedOn);
                                        }
                                        //nb++;
                                    }
                                }

                                //metrics.AddOrUpdate(nb, 1, (i, old) => old + 1);
                            }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.UNRELIABLE);

                            _scene.BrodcastUsedSkill(this.GetUsedSkills());
                        }
                        //else
                        //{
                        //    metrics.AddOrUpdate(0, 1, (i, old) => old + 1);
                        //}

                        lastRun = current;
                        if (current > lastLog + 1000 * 60)
                        {
                            lastLog = current;

                            _scene.GetComponent<ILogger>().Log(LogLevel.Info, "gameloop", "running", new
                            {
                                //sends = metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                                //received = ComputeMetrics()
                            });
                            metrics.Clear();
                        }
                        var execution = env.Clock - current;
                        if (execution > this._longestExecution)
                        {
                            this._longestExecution = execution;
                        }
                    }

                    RunGameplayLoop();
                }
                catch (Exception ex)
                {
                    _scene.GetComponent<ILogger>().Error("update.loop", "{0}", ex.Message);
                }
            });
        }

        private void RunGameplayLoop()
        {
            var clock = _scene.GetComponent<IEnvironment>().Clock;


            foreach (var ship in _ships.Values.ToArray())
            {

                if (ship.Status == ShipStatus.Dead && ship.lastStatusUpdate + 2000 < clock)
                {
                     ReviveShip(ship);
                }
            }
        }

        private void ReviveShip(Ship ship)
        {
            var clock = _scene.GetComponent<IEnvironment>().Clock;

            ship.x = X_MIN + (float)(_rand.NextDouble() * (X_MAX - X_MIN));
            ship.y = Y_MIN + (float)(_rand.NextDouble() * (Y_MAX - Y_MIN));
            ship.PositionUpdatedOn = clock;
            _scene.GetComponent<ILogger>().Info("logic.respawn", "Trying to find peer for {0}", ship.player.Id);
            var peer = _scene.RemotePeers.FirstOrDefault(p => p.Id == ship.player.Id);
            if (peer != null)
            {
                _scene.GetComponent<ILogger>().Info("logic.respawn", "Sending respawn position to {0}", peer.Id);
                _scene.Send(new MatchPeerFilter(peer), "ship.forcePositionUpdate", (s) =>
                {
                    using (var writer = new BinaryWriter(s, Encoding.UTF8, true))
                    {
                        writer.Write(ship.id);
                        writer.Write(ship.x);
                        writer.Write(ship.y);
                        writer.Write(ship.rot);
                        writer.Write(ship.PositionUpdatedOn);
                    }
                }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
            }
            
            ship.ChangePv(ship.maxPv - ship.currentPv);
        }

        public class ReceivedDataMetrics
        {
            public double Avg;
            public int NbSamples;
            public int[] Percentiles = new int[11];
            public int Percentile99;
            public int LostPackets;
            public double LongestExecution;
        }

        public ReceivedDataMetrics ComputeMetrics()
        {
            var intervals = new List<int>();
            foreach (var boid in _boidsTimes)
            {
                var values = boid.Value.ToArray();
                boid.Value.Clear();
                for (int i = 0; i < values.Length; i++)
                {
                    intervals.Add((int)values[i]);
                    //intervals.Add((int)(values[i] - values[i - 1]));
                }
            }

            var result = new ReceivedDataMetrics();
            if (intervals.Any())
            {
                intervals.Sort();

                result.Avg = intervals.Average();
                result.NbSamples = intervals.Count;
                for (int i = 0; i < 11; i++)
                {
                    result.Percentiles[i] = intervals[(i * (result.NbSamples - 1)) / 10];
                }
                result.Percentile99 = intervals[99 * (result.NbSamples - 1) / 100];
                result.LostPackets = this._lostPackets;
                this._lostPackets = 0;
                result.LongestExecution = this._longestExecution;
                this._longestExecution = 0;
            }
            return result;
        }

        private ConcurrentDictionary<ushort, List<long>> _boidsTimes = new ConcurrentDictionary<ushort, List<long>>();
        private ConcurrentDictionary<ushort, uint> _boidsLastIndex = new ConcurrentDictionary<ushort, uint>();
        private const int positionUpdateLength = 2 + 3 * 4 + 4 + 4;
        private int _lostPackets = 0;
        private long _longestExecution = 0;
        private void OnPositionUpdate(Packet<IScenePeerClient> packet)
        {
            unchecked
            {
                using (var reader = new BinaryReader(packet.Stream))
                {
                    
                    var shipId = reader.ReadUInt16();
                    var x = reader.ReadSingle();
                    var y = reader.ReadSingle();
                    var rot = reader.ReadSingle();
                    //var timestamp = reader.ReadInt64();
                    var timestamp = _scene.GetComponent<IEnvironment>().Clock;
                    var ls = _scene.GetComponent<Server.Leaderboards.ILeaderboardsService>();

                    Ship ship;
                    if (_ships.TryGetValue(shipId, out ship))
                    {
                        ship.PositionUpdatedOn = timestamp;
                        ship.x = x;
                        ship.y = y;
                        ship.rot = rot;

                    }

                    //this._boidsLastIndex.AddOrUpdate(shipId, packetIndex, (_, previousIndex) =>
                    //{
                    //    if (previousIndex < (packetIndex - 1))
                    //    {
                    //        Interlocked.Add(ref this._lostPackets, (int)(packetIndex - previousIndex - 1));
                    //    }
                    //    return packetIndex;
                    //});
                }

                //packet.Connection.Send("position.update", s =>
                //_scene.Broadcast("position.update", s =>
                //{
                //    using (var binWriter = new BinaryWriter(s, Encoding.UTF8, true))
                //    {
                //        binWriter.Write((byte)0xc0);
                //        binWriter.Write((uint)time);
                //        s.Write(bytes, 0, bytes.Length);
                //    }
                //}, PacketPriority.MEDIUM_PRIORITY, PacketReliability.UNRELIABLE_SEQUENCED);
            }
        }


        private async Task OnDisconnected(DisconnectedArgs arg)
        {
            Player player;
            if (_players.TryRemove(arg.Peer.Id, out player))
            {
                Ship ship;
                if (_ships.TryRemove(player.ShipId, out ship))
                {
                    List<long> _;
                    _boidsTimes.TryRemove(player.ShipId, out _);
                    _scene.GetComponent<ILogger>().Info("gameScene", "removed ship");
                    _scene.Broadcast("ship.remove", ship.id, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
                }
            }
        }

        private async Task OnConnected(IScenePeerClient client)
        {
            var pInfos = PlayerInfos.FromPeer(client);

            var player = new Player(pInfos, client.Id);

            _players.AddOrUpdate(client.Id, player, (id, old) => player);

            Ship ship = null;
            if (!player.IsObserver)
            {
                ship = CreateShip(player);
                player.ShipId = ship.id;
                _ships.AddOrUpdate(ship.id, ship, (id, old) => ship);

                var dto = new ShipCreatedDto { timestamp = _scene.GetComponent<IEnvironment>().Clock, id = ship.id, team = ship.team, x = ship.x, y = ship.y, rot = ship.rot, weapons = ship.weapons, status = ship.Status };
                var data = new[] { dto };

                client.Send("ship.me", s => client.Serializer().Serialize(data, s), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);

                _scene.Broadcast("ship.add", data, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);

            }

            // Send ships to new client
            var shipsToSend = new List<ShipCreatedDto>();
            foreach (var s in _ships.Values.ToArray())
            {
                var dto = new ShipCreatedDto { timestamp = _scene.GetComponent<IEnvironment>().Clock, id = s.id, team = s.team, x = s.x, y = s.y, rot = s.rot, weapons = s.weapons, status = s.Status };
                if (ship == null || ship.id != s.id)
                {
                    shipsToSend.Add(dto);
                }
            }
            client.Send("ship.add", stream =>
            {
                client.Serializer().Serialize(shipsToSend, stream);
            }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);

            if (ship != null)
            {
                await Task.Delay(1000);
                ship.UpdateStatus(ShipStatus.InGame);
            }

            _scene.GetComponent<ILogger>().Info("gameScene", "Added ship");

            StartUpdateLoop();
        }

        private Random _rand = new Random();

        public Task OnShutdown(ShutdownArgs args)
        {
            if (_periodicUpdateTask != null)
            {
                _periodicUpdateTask.Dispose();
            }
            return Task.FromResult(true);
        }

        private Ship CreateShip(Player player)
        {
            ushort id = 0;
            lock (this)
            {
                id = _currentId++;
            }
            player.ShipId = id;
            var ship = new Ship(this)
            {
                Status = ShipStatus.Waiting,
                team = id,//Deathmatch
                id = id,
                player = player,
                rot = (float)(_rand.NextDouble() * 2 * Math.PI),
                x = X_MIN + (float)(_rand.NextDouble() * (X_MAX - X_MIN)),
                y = Y_MIN + (float)(_rand.NextDouble() * (Y_MAX - Y_MIN)),
                currentPv = 15,
                maxPv = 15,
                weapons = new Weapon[] { new Weapon { id = "canon", damage = 10, precision = 0.6f, coolDown = 1500, range = 200 }/*, new Weapon { id = "missile", damage = 40, precision = 0.6f, coolDown = 3 }*/ }
            };
            return ship;
        }

        internal void BroadcastPvUpdate(ushort shipId, int diff)
        {
            this._scene.BroadcastPvUpdate(shipId, diff);
        }

        internal void BroadcastStatusChanged(ushort shipId, ShipStatus shipStatus)
        {
            if (shipStatus == ShipStatus.Dead)
            {
                Interlocked.Increment(ref this._deathCount);
            }
            this._scene.BroadcastStatusChanged(shipId, shipStatus);
        }
    }


    public static class SceneExtensions
    {
        public static void BroadcastStatusChanged(this ISceneHost scene, ushort shipId, ShipStatus status)
        {
            
            scene.Broadcast("ship.statusChanged", new StatusChangedMsg { shipId = shipId, status = status });
        }

        //public static void BroadcastUsedSkill(this ISceneHost scene, ushort shipId, ushort target, bool success, string weaponId, long timestamp)
        //{
        //    scene.Broadcast("ship.usedSkill", new UsedSkillMsg { shipId = target, origin = shipId, success = success, weaponId = weaponId, timestamp = timestamp });
        //}

        public static void BrodcastUsedSkill(this ISceneHost scene, IEnumerable<UsedSkillMsg> skills)
        {
            scene.Broadcast("ship.usedSkill", skills.ToArray());
        }


        public static void BroadcastPvUpdate(this ISceneHost scene, ushort shipId, int diff)
        {
            scene.Broadcast("ship.pv", s =>
            {
                using (var writer = new BinaryWriter(s, Encoding.UTF8, true))
                {
                    writer.Write(shipId);
                    writer.Write(diff);
                }
            });
        }
    }
}
