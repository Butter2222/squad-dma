using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;

namespace squad_dma
{
    public class RegistredActors
    {
        private readonly ulong _persistentLevel;
        private ulong _actorsArray;
        private readonly Stopwatch _regSw = new();
        public readonly ConcurrentDictionary<ulong, UActor> _actors = new();
        public IEnumerable<uint> GetActorNameIds()
        {
            return _actors.Values.Select(actor => actor.NameId).Where(id => id != 0);
        }

        #region Getters
        public ReadOnlyDictionary<ulong, UActor> Actors { get; }

        public int ActorCount
        {
            get
            {
                const int maxAttempts = 5;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    try
                    {
                        var count = Memory.ReadValue<int>(_persistentLevel + Offsets.Level.MaxPacket);

                        if (count < 1)
                        {
                            this._actors.Clear();
                            return -1;
                        }

                        return count;
                    }
                    catch (DMAShutdown)
                    {
                        throw;
                    }
                    catch (Exception ex) when (attempt < maxAttempts - 1)
                    {
                        Program.Log($"ERROR - PlayerCount attempt {attempt + 1} failed: {ex}");
                        Thread.Sleep(1000);
                    }
                }
                return -1;
            }
        }
        #endregion

        /// <summary>
        /// RegisteredPlayers List Constructor.
        /// </summary>
        public RegistredActors(ulong persistentLevelAddr)
        {
            this._persistentLevel = persistentLevelAddr;
            this.Actors = new ReadOnlyDictionary<ulong, UActor>(_actors);
            this._actorsArray = Memory.ReadPtr(_persistentLevel + Offsets.Level.Actors);
            this._regSw.Start();
        }

        #region Update List/Player Functions
        public Dictionary<ulong, uint> GetActorBaseWithName()
        {
            var count = this.ActorCount;
            if (count < 1)
                return new Dictionary<ulong, uint>();

            var initialActorScatterMap = new ScatterReadMap(count);

            var actorRound = initialActorScatterMap.AddRound();
            var playerObjectIdRound = initialActorScatterMap.AddRound();

            for (int i = 0; i < count; i++)
            {
                var actorAddr = actorRound.AddEntry<ulong>(i, 0, _actorsArray + (uint)(i * 0x8));
                var playerObjectId = playerObjectIdRound.AddEntry<uint>(i, 1, actorAddr, null, Offsets.Actor.ID);
            }

            initialActorScatterMap.Execute();

            var actorBaseWithName = new Dictionary<ulong, uint>();
            for (int i = 0; i < count; i++)
            {
                if (!initialActorScatterMap.Results[i][0].TryGetResult<ulong>(out var actorAddr) || actorAddr == 0)
                    continue;
                if (!initialActorScatterMap.Results[i][1].TryGetResult<uint>(out var actorNameId) || actorNameId == 0)
                    continue;
                actorBaseWithName[actorAddr] = actorNameId;
            }

            return actorBaseWithName;
        }


        public void UpdateList()
        {
            if (this._regSw.ElapsedMilliseconds < 500)
                return;

            try
            {
                var count = this.ActorCount;

                if (count < 1) // todo
                    throw new GameEnded();

                var initialActorScatterMap = new ScatterReadMap(count);

                var actorRound = initialActorScatterMap.AddRound();
                var playerObjectIdRound = initialActorScatterMap.AddRound();

                for (int i = 0; i < count; i++)
                {
                    var actorAddr = actorRound.AddEntry<ulong>(i, 0, _actorsArray + (uint)(i * 0x8));
                    var playerObjectId = playerObjectIdRound.AddEntry<uint>(i, 1, actorAddr, null, Offsets.Actor.ID);
                }

                initialActorScatterMap.Execute();

                var actorBaseWithName = new Dictionary<ulong, uint>();
                for (int i = 0; i < count; i++) {
                    if (!initialActorScatterMap.Results[i][0].TryGetResult<ulong>(out var actorAddr) || actorAddr == 0)
                        continue;
                    if (!initialActorScatterMap.Results[i][1].TryGetResult<uint>(out var actorNameId) || actorNameId == 0)
                        continue;
                    actorBaseWithName[actorAddr] = actorNameId;
                }

                var notUpdated = new HashSet<ulong>(_actors.Keys);
                foreach (var item in actorBaseWithName) {
                    if (_actors.ContainsKey(item.Key) && _actors[item.Key].NameId == item.Value) {
                        notUpdated.Remove(item.Key);
                        actorBaseWithName.Remove(item.Key);
                    }
                }
                var names = Memory.GetNamesById([.. actorBaseWithName.Values.Distinct()]);
                foreach (var item in names)
                {
                    if (item.Value.StartsWith("BP_UAF"))
                    {
                        names[item.Key] = item.Value.Replace("BP_UAF", "BP_Soldier_UAF");
                    }
                }
                var playersNameIDs = names.Where(x => x.Value.StartsWith("BP_Soldier") || Names.TechNames.ContainsKey(x.Value)).ToDictionary();
                var filteredActors = actorBaseWithName.Where(actor => playersNameIDs.ContainsKey(actor.Value)).Select(actor => actor.Key).ToList();
                count = filteredActors.Count;
                for (int i = 0; i < count; i++) {
                    var actorAddr = filteredActors[i];
                    var nameId = actorBaseWithName[actorAddr];
                    var actorName = playersNameIDs[nameId];
                    var team = Team.Unknown;
                    var actorType = Names.TechNames.GetValueOrDefault(actorName, ActorType.Player);
                    if (actorType == ActorType.Player) {
                        team = Names.Teams.GetValueOrDefault(actorName[..14], Team.Unknown);
                    }
                    if (_actors.TryGetValue(actorAddr, out var actor)) {
                        if (actor.ErrorCount > 50) {
                            Program.Log($"Existing player '{actor.Base}' being reallocated due to excessive errors...");
                            reallocateActor(actorAddr, team, actorType, nameId);
                        } else if (actor.Base != actorAddr) {
                            Program.Log($"Existing player '{actor.Base}' being reallocated due to new base address...");
                            reallocateActor(actorAddr, team, actorType, nameId);
                        }
                    } else {
                        reallocateActor(actorAddr, team, actorType, nameId);
                    }
                    _actors[actorAddr].Name = actorName;
                    notUpdated.Remove(actorAddr);
                }

                foreach (var actorIdToRemove in notUpdated) {
                    _actors.TryRemove(actorIdToRemove, out var _);
                }
            }
            catch (DMAShutdown)
            {
                throw;
            }
            catch (GameEnded)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Log($"CRITICAL ERROR - RegisteredActors Loop FAILED: {ex}");
            }
            finally
            {
                this._regSw.Restart();
            }

            UActor reallocateActor(ulong actorBase, Team team, ActorType actorType, uint nameId)
            {
                try
                {
                    _actors[actorBase] = new UActor(actorBase)
                    {
                        Team = team,
                        ActorType = actorType,
                        NameId = nameId,
                    };
                    return _actors[actorBase];
                }
                catch (Exception ex)
                {
                    throw new Exception($"ERROR re-allocating player: ", ex);
                }
            }
        }

        /// <summary>
        /// Updates all 'Player' values (Position,health,direction,etc.)
        /// </summary>
        public void UpdateAllPlayers()
        {
            try
            {
                var count = _actors.Count;
                if (count < 10) // todo | this causes seeding servers not to render
                    throw new GameEnded();
                var actorBases = _actors.Values.Select(actor => actor.Base).Order().ToArray();

                var playerInfoScatterMap = new ScatterReadMap(count);
                var playerInstanceInfoRound = playerInfoScatterMap.AddRound();
                var instigatorAndRootRound = playerInfoScatterMap.AddRound();

                for (int i = 0; i < count; i++)
                {
                    var actorAddr = actorBases[i];
                    var actorType = _actors[actorAddr].ActorType;

                    var rootComponent = playerInstanceInfoRound.AddEntry<ulong>(i, 1, actorAddr + Offsets.Actor.RootComponent);
                    if (actorType == ActorType.Player)
                    {
                        var health = playerInstanceInfoRound.AddEntry<float>(i, 2, actorAddr + Offsets.ASQSoldier.Health);
                    }
                    else if (Names.Deployables.Contains(actorType))
                    {
                        var health = playerInstanceInfoRound.AddEntry<float>(i, 2, actorAddr + Offsets.SQDeployable.Health);
                        var maxHealth = playerInstanceInfoRound.AddEntry<float>(i, 3, actorAddr + Offsets.SQDeployable.MaxHealth);
                    }
                    else
                    {
                        var health = playerInstanceInfoRound.AddEntry<float>(i, 2, actorAddr + Offsets.SQVehicle.Health);
                        var maxHealth = playerInstanceInfoRound.AddEntry<float>(i, 3, actorAddr + Offsets.SQVehicle.MaxHealth);
                    }

                    var origin = instigatorAndRootRound.AddEntry<Vector3>(i, 4, rootComponent, null, Offsets.USceneComponent.RelativeLocation);
                    var rotation = instigatorAndRootRound.AddEntry<Vector3>(i, 5, rootComponent, null, Offsets.USceneComponent.RelativeRotation);
                }

                playerInfoScatterMap.Execute();

                for (int i = 0; i < count; i++)
                {
                    var actor = _actors[actorBases[i]];

                    if (playerInfoScatterMap.Results[i][2].TryGetResult<float>(out var hp))
                    {
                        if (actor.Health > 0 && hp <= 0)
                        {
                            actor.DeathPosition = actor.Position;
                            actor.TimeOfDeath = DateTime.Now;
                        }
                        actor.Health = hp;
                    }
                    if (playerInfoScatterMap.Results[i].ContainsKey(3) && playerInfoScatterMap.Results[i][3].TryGetResult<float>(out var maxHp))
                    {
                        actor.Health /= maxHp;
                        actor.Health *= 100;
                    }

                    if (playerInfoScatterMap.Results[i][4].TryGetResult<Vector3>(out var location))
                    {
                        actor.Position = location;
                    }

                    if (playerInfoScatterMap.Results[i][5].TryGetResult<Vector3>(out var rotation))
                    {
                        actor.Rotation = new Vector2(rotation.Y, rotation.X);
                        actor.Rotation3D = rotation;
                    }
                }
            }
            catch (GameEnded)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Log($"CRITICAL ERROR - UpdateAllPlayers Loop FAILED: {ex}");
            }
        }
        #endregion
    }
}