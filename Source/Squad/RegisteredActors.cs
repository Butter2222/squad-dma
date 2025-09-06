using squad_dma.Source.Misc;
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
        private readonly ConcurrentDictionary<ulong, UActor> _actors = new();
        private Dictionary<ulong, int> _squadCache = new();
        private DateTime _lastSquadUpdate = DateTime.MinValue;
        private const int SquadUpdateInterval = 1000; // Update every 1 second

        // Bone IDs for ESP
        private static readonly int[] _boneIds = { 7, 6, 5, 3, 2, 65, 66, 67, 68, 92, 93, 94, 95, 130, 131, 132, 125, 126, 127 };

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
                        var actorsTArray = _persistentLevel + Offsets.Level.Actors;
                        var count = Memory.ReadValue<int>(actorsTArray + 0x8);

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
                        Logger.Error($"PlayerCount attempt {attempt + 1} failed: {ex}");
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
            this.Actors = new(this._actors);
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
                            Logger.Error($"Existing player '{actor.Base}' being reallocated due to excessive errors...");
                            reallocateActor(actorAddr, team, actorType, nameId);
                        } else if (actor.Base != actorAddr) {
                            Logger.Error($"Existing player '{actor.Base}' being reallocated due to new base address...");
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
                Logger.Error($"CRITICAL ERROR - RegisteredActors Loop FAILED: {ex}");
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
                if (count < 10) 
                    throw new GameEnded();
                var actorBases = _actors.Values.Select(actor => actor.Base).Order().ToArray();

                var playerInfoScatterMap = new ScatterReadMap(count);
                var playerInstanceInfoRound = playerInfoScatterMap.AddRound();
                var instigatorAndRootRound = playerInfoScatterMap.AddRound();
                var teamInfoRound = playerInfoScatterMap.AddRound();
                var meshRound = playerInfoScatterMap.AddRound();
                var boneInfoRound = playerInfoScatterMap.AddRound();

                for (int i = 0; i < count; i++)
                {
                    var actorAddr = actorBases[i];
                    var actorType = _actors[actorAddr].ActorType;

                    var rootComponent = playerInstanceInfoRound.AddEntry<ulong>(i, 1, actorAddr + Offsets.Actor.RootComponent);

                    if (actorType == ActorType.Player)
                    {
                        playerInstanceInfoRound.AddEntry<float>(i, 2, actorAddr + Offsets.ASQSoldier.Health);

                        var pawnPlayerState = playerInstanceInfoRound.AddEntry<ulong>(i, 3, actorAddr + Offsets.Pawn.PlayerState);
                        var controller = playerInstanceInfoRound.AddEntry<ulong>(i, 4, actorAddr + Offsets.Pawn.Controller);
                        var controllerPlayerState = teamInfoRound.AddEntry<ulong>(i, 5, controller, null, Offsets.Controller.PlayerState);

                        teamInfoRound.AddEntry<int>(i, 6, pawnPlayerState, null, Offsets.ASQPlayerState.TeamID);
                        teamInfoRound.AddEntry<int>(i, 7, controllerPlayerState, null, Offsets.ASQPlayerState.TeamID);

                        // Add mesh and bone tracking for ESP
                       // var meshPtr = playerInstanceInfoRound.AddEntry<ulong>(i, 8, actorAddr + Offsets.ASQSoldier.Mesh);
                       // meshRound.AddEntry<FTransform>(i, 9, meshPtr, null, Offsets.USceneComponent.ComponentToWorld);
                        //var boneArrayPtr = meshRound.AddEntry<ulong>(i, 10, meshPtr, null, 0x5B8);

                       // for (int j = 0; j < _boneIds.Length; j++)
                       // {
                       //     boneInfoRound.AddEntry<FTransform>(i, 11 + j, boneArrayPtr, null, (uint)(_boneIds[j] * 0x30));
                       // }
                    }
                    else if (Names.Deployables.Contains(actorType))
                    {
                        playerInstanceInfoRound.AddEntry<float>(i, 2, actorAddr + Offsets.SQDeployable.Health);
                        playerInstanceInfoRound.AddEntry<float>(i, 3, actorAddr + Offsets.SQDeployable.MaxHealth);
                        playerInstanceInfoRound.AddEntry<int>(i, 4, actorAddr + Offsets.SQDeployable.Team);
                    }
                    else
                    {
                        playerInstanceInfoRound.AddEntry<float>(i, 2, actorAddr + Offsets.SQVehicle.Health);
                        playerInstanceInfoRound.AddEntry<float>(i, 3, actorAddr + Offsets.SQVehicle.MaxHealth);
                        // Add vehicle team ID reading
                        playerInstanceInfoRound.AddEntry<ulong>(i, 14, actorAddr + Offsets.SQVehicle.ClaimedBySquad);
                        // Read team ID directly from claimed squad
                        playerInstanceInfoRound.AddEntry<int>(i, 15, actorAddr + Offsets.SQVehicle.ClaimedBySquad);
                    }

                    instigatorAndRootRound.AddEntry<double>(i, 8, rootComponent, null, Offsets.USceneComponent.RelativeLocation);
                    instigatorAndRootRound.AddEntry<double>(i, 9, rootComponent, null, Offsets.USceneComponent.RelativeLocation + 0x8);
                    instigatorAndRootRound.AddEntry<double>(i, 10, rootComponent, null, Offsets.USceneComponent.RelativeLocation + 0x10);
                    
                    instigatorAndRootRound.AddEntry<double>(i, 11, rootComponent, null, Offsets.USceneComponent.RelativeRotation);
                    instigatorAndRootRound.AddEntry<double>(i, 12, rootComponent, null, Offsets.USceneComponent.RelativeRotation + 0x8);
                    instigatorAndRootRound.AddEntry<double>(i, 13, rootComponent, null, Offsets.USceneComponent.RelativeRotation + 0x10);
                }

                playerInfoScatterMap.Execute();

                bool updateSquads = (DateTime.Now - _lastSquadUpdate).TotalMilliseconds > SquadUpdateInterval;

                for (int i = 0; i < count; i++)
                {
                    var actor = _actors[actorBases[i]];
                    var results = playerInfoScatterMap.Results[i];
                    float hp = 0;

                    if (results.TryGetValue(2, out var healthResult) && healthResult.TryGetResult<float>(out hp))
                    {
                        if (actor.ActorType == ActorType.Player && actor.Health > 0 && hp <= 0)
                        {
                            actor.DeathPosition = actor.Position;
                            actor.TimeOfDeath = DateTime.Now;
                        }
                        actor.Health = hp;
                    }

                    if (results.TryGetValue(3, out var maxHpResult) &&
                       maxHpResult.TryGetResult<float>(out var maxHp) &&
                       maxHp > 0)
                    {
                        actor.Health = (hp / maxHp) * 100;
                    }

                    if (actor.ActorType == ActorType.Player)
                    {
                        bool teamIdFound = false;

                        if (results.TryGetValue(6, out var pawnTeamResult) &&
                            pawnTeamResult.TryGetResult<int>(out var pawnTeamId))
                        {
                            actor.TeamID = pawnTeamId;
                            teamIdFound = true;
                        }

                        if (!teamIdFound && results.TryGetValue(7, out var controllerTeamResult) &&
                            controllerTeamResult.TryGetResult<int>(out var controllerTeamId))
                        {
                            actor.TeamID = controllerTeamId;
                            teamIdFound = true;
                        }

                        if (!teamIdFound && results.TryGetValue(4, out var controllerResult) &&
                            controllerResult.TryGetResult<ulong>(out var controllerAddr) &&
                            controllerAddr != 0)
                        {
                            try
                            {
                                var playerStateAddr = Memory.ReadPtr(controllerAddr + Offsets.Controller.PlayerState);
                                if (playerStateAddr != 0)
                                {
                                    actor.TeamID = Memory.ReadValue<int>(playerStateAddr + Offsets.ASQPlayerState.TeamID);
                                    teamIdFound = true;
                                }
                            }
                            catch { /* Silently fail */ }
                        }

                        if (actor.IsFriendly())
                        {
                            if (_squadCache.TryGetValue(actor.Base, out var cachedSquadId))
                            {
                                actor.SquadID = cachedSquadId;
                            }
                            else
                            {
                                actor.SquadID = -1;
                            }

                            if (updateSquads)
                            {
                                try
                                {
                                    ulong playerState = 0;
                                    if (results.TryGetValue(3, out var psResult))
                                        psResult.TryGetResult<ulong>(out playerState);

                                    if (playerState != 0)
                                    {
                                        var squadState = Memory.ReadPtr(playerState + Offsets.ASQPlayerState.SquadState);
                                        if (squadState != 0)
                                        {
                                            var squadId = Memory.ReadValue<int>(squadState + Offsets.ASQSquadState.SquadId);

                                            if (squadId > 0 && squadId < 1000)
                                            {
                                                actor.SquadID = squadId;
                                                _squadCache[actor.Base] = squadId; 
                                            }
                                        }
                                    }
                                }
                                catch { /* Silently fail */ }
                            }
                        }

                        // Update mesh and bone information for ESP
                        if (results.TryGetValue(8, out var meshResult) && meshResult.TryGetResult<ulong>(out var meshAddr))
                        {
                            actor.Mesh = meshAddr;
                            if (meshAddr == 0)
                            {
                                actor.BoneScreenPositions = new Vector2[_boneIds.Length];
                                Array.Clear(actor.BoneScreenPositions, 0, actor.BoneScreenPositions.Length);
                                continue;
                            }

                            // ComponentToWorld is commented out, so skip it for now
                            actor.ComponentToWorld = new FTransform(); // Default transform

                            if (results.TryGetValue(9, out var boneArrayResult) && boneArrayResult.TryGetResult<ulong>(out var boneArrayPtr))
                            {
                                if (boneArrayPtr == 0)
                                {
                                    actor.BoneScreenPositions = new Vector2[_boneIds.Length];
                                    Array.Clear(actor.BoneScreenPositions, 0, actor.BoneScreenPositions.Length);
                                    continue;
                                }

                                actor.BoneTransforms.Clear();
                                var viewInfo = new MinimalViewInfo
                                {
                                    Location = Memory._game.LocalPlayer.Position,
                                    Rotation = Memory._game.LocalPlayer.Rotation3D,
                                    FOV = Memory._game.CurrentFOV
                                };
                                actor.BoneScreenPositions = new Vector2[_boneIds.Length];

                                bool anyBoneSuccess = false;
                                for (int j = 0; j < _boneIds.Length; j++)
                                {
                                    if (results.TryGetValue(10 + j, out var boneResult) &&
                                        boneResult.TryGetResult<FTransform>(out var boneTransform))
                                    {
                                        actor.BoneTransforms[_boneIds[j]] = boneTransform;
                                        Vector3 boneWorldPos = TransformToWorld(boneTransform, actor.ComponentToWorld);
                                        Vector3D boneWorldPos3D = boneWorldPos.ToVector3D();
                                        actor.BoneScreenPositions[j] = Camera.WorldToScreen(viewInfo, boneWorldPos3D);
                                        if (actor.BoneScreenPositions[j] != Vector2.Zero)
                                        {
                                            anyBoneSuccess = true;
                                        }
                                    }
                                    else
                                    {
                                        actor.BoneScreenPositions[j] = Vector2.Zero;
                                    }
                                }

                                if (!anyBoneSuccess)
                                {
                                    Array.Clear(actor.BoneScreenPositions, 0, actor.BoneScreenPositions.Length);
                                }
                            }
                            else
                            {
                                actor.BoneScreenPositions = new Vector2[_boneIds.Length];
                                Array.Clear(actor.BoneScreenPositions, 0, actor.BoneScreenPositions.Length);
                            }
                        }
                        else
                        {
                            actor.Mesh = 0;
                            actor.BoneScreenPositions = new Vector2[_boneIds.Length];
                            Array.Clear(actor.BoneScreenPositions, 0, actor.BoneScreenPositions.Length);
                        }
                    }
                    else if (Names.Deployables.Contains(actor.ActorType))
                    {
                        if (results.TryGetValue(14, out var teamResult) &&
                            teamResult.TryGetResult<int>(out var teamId))
                        {
                            actor.TeamID = (teamId == 1 || teamId == 2) ? teamId : -1;
                        }
                        else
                        {
                            actor.TeamID = -1;
                        }
                    }
                    else
                    {
                        if (results.TryGetValue(14, out var claimedBySquadResult) &&
                            claimedBySquadResult.TryGetResult<ulong>(out var claimedBySquad) &&
                            claimedBySquad != 0)
                        {
                            try
                            {
                                if (claimedBySquad > 0x10000 && claimedBySquad < 0x7FFFFFFFFFFF)
                                {
                                    var teamId = Memory.ReadValue<int>(claimedBySquad + Offsets.ASQSquadState.TeamId);
                                    actor.TeamID = (teamId == 1 || teamId == 2) ? teamId : -1;
                                }
                                else
                                {
                                    actor.TeamID = -1;
                                }
                            }
                            catch
                            {
                                actor.TeamID = -1;
                            }
                        }
                        else
                        {
                            actor.TeamID = -1;
                        }
                    }

                    if (results.TryGetValue(8, out var xResult) && results.TryGetValue(9, out var yResult) &&
                        results.TryGetValue(10, out var zResult) &&
                        xResult.TryGetResult<double>(out var x) && yResult.TryGetResult<double>(out var y) &&
                        zResult.TryGetResult<double>(out var z))
                    {
                        actor.Position = new Vector3D(x, y, z);
                    }

                    if (results.TryGetValue(11, out var rotXResult) && results.TryGetValue(12, out var rotYResult) &&
                        results.TryGetValue(13, out var rotZResult) &&
                        rotXResult.TryGetResult<double>(out var rotX) && rotYResult.TryGetResult<double>(out var rotY) &&
                        rotZResult.TryGetResult<double>(out var rotZ))
                    {
                        var rotation = new Vector3D(rotX, rotY, rotZ);
                        actor.Rotation = new Vector2D(rotation.Y, rotation.X);
                        actor.Rotation3D = rotation;
                    }
                }

                if (updateSquads)
                {
                    _lastSquadUpdate = DateTime.Now;
                    _squadCache = _squadCache.Where(kv => _actors.ContainsKey(kv.Key))
                                           .ToDictionary(kv => kv.Key, kv => kv.Value);
                }
            }
            catch (GameEnded)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"CRITICAL ERROR - RegisteredActors Loop FAILED: {ex}");
            }
        }
        #endregion

        private Vector3 TransformToWorld(FTransform boneTransform, FTransform componentToWorld)
        {
            boneTransform.Scale3D = new Vector3(1, 1, 1);
            componentToWorld.Scale3D = new Vector3(1, 1, 1);
            Matrix4x4 boneMatrix = boneTransform.ToMatrix();
            Matrix4x4 worldMatrix = componentToWorld.ToMatrix();
            Matrix4x4 finalMatrix = boneMatrix * worldMatrix;
            return new Vector3(finalMatrix.M41, finalMatrix.M42, finalMatrix.M43);
        }
    }
}