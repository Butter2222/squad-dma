using squad_dma.Source.Misc;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;

namespace squad_dma
{
    public class RegisteredActors
    {
        private readonly ulong _gameWorld;
        private readonly ulong _persistentLevel;
        private ulong _gameState;
        private ulong _playerArray;
        private readonly Stopwatch _regSw = new();
        private readonly Stopwatch _vehicleUpdateSw = new();
        private readonly ConcurrentDictionary<ulong, UActor> _actors = new();
        private Dictionary<ulong, int> _squadCache = new();
        private DateTime _lastSquadUpdate = DateTime.MinValue;

        private const int SquadUpdateInterval = 2000; // Increased from 1000ms to 2000ms
        private const int MaxPlayers = 120;
        private const int MaxActors = 20000;
        private const int ListUpdateInterval = 500; // Increased from 300ms to 500ms

        private static readonly int[] _boneIds = { 7, 6, 5, 3, 2, 65, 66, 67, 68, 92, 93, 94, 95, 130, 131, 132, 125, 126, 127 };

        private static readonly HashSet<string> _vehicleNameBlacklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "Spawner", "GameMode", "Weapon", "Construction", "Ammo", "Servo", "CaptureZone", "Main", "Passenger"
        };

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
                        var actorCount = Memory.ReadValue<int>(_persistentLevel + Offsets.Level.Actors + 0x8);

                        if (_gameState == 0)
                            _gameState = Memory.ReadPtr(_gameWorld + Offsets.World.GameState);

                        int playerCount = 0;
                        if (_gameState != 0)
                        {
                            var playerArrayTArray = _gameState + Offsets.AGameStateBase.PlayerArray;
                            var playerArrayPtr = Memory.ReadPtr(playerArrayTArray);
                            if (playerArrayPtr != 0)
                            {
                                playerCount = Memory.ReadValue<int>(playerArrayTArray + 0x8);
                                if (playerCount < 0 || playerCount > MaxPlayers) playerCount = 0;
                            }
                        }

                        var total = actorCount + playerCount;
                        if (total < 1)
                        {
                            _actors.Clear();
                            return -1;
                        }

                        return total;
                    }
                    catch (DMAShutdown) { throw; }
                    catch (Exception ex) when (attempt < maxAttempts - 1)
                    {
                        Logger.Error($"ActorCount attempt {attempt + 1} failed: {ex}");
                        Thread.Sleep(1000);
                    }
                }
                return -1;
            }
        }

        public RegisteredActors(ulong gameWorldAddr)
        {
            _gameWorld = gameWorldAddr;
            _persistentLevel = Memory.ReadPtr(_gameWorld + Offsets.World.PersistentLevel);
            Actors = new(_actors);
            _regSw.Start();
            _vehicleUpdateSw.Start();
        }

        public Dictionary<ulong, uint> GetActorBaseWithName()
        {
            return _actors.Values
                .Where(a => a.NameId != 0)
                .ToDictionary(a => a.Base, a => a.NameId);
        }

        public void UpdateList()
        {
            if (_regSw.ElapsedMilliseconds < ListUpdateInterval)
                return;

            try
            {
                UpdatePlayersFromPlayerArray();
                UpdateVehiclesFromActorList();
            }
            catch (DMAShutdown) { throw; }
            catch (GameEnded) { throw; }
            catch (Exception ex)
            {
                Logger.Error($"CRITICAL ERROR - RegisteredActors Loop FAILED: {ex}");
            }
            finally
            {
                _regSw.Restart();
            }
        }

        private void UpdatePlayersFromPlayerArray()
        {
            try
            {
                if (_gameState == 0)
                {
                    _gameState = Memory.ReadPtr(_gameWorld + Offsets.World.GameState);
                    if (_gameState == 0) return;
                }

                var playerArrayTArray = _gameState + Offsets.AGameStateBase.PlayerArray;
                _playerArray = Memory.ReadPtr(playerArrayTArray);
                var playerCount = Memory.ReadValue<int>(playerArrayTArray + 0x8);

                if (_playerArray == 0 || playerCount <= 0 || playerCount > MaxPlayers)
                    return;

                var scatterMap = new ScatterReadMap(playerCount);
                var stateRound = scatterMap.AddRound();
                var pawnRound = scatterMap.AddRound();
                var idRound = scatterMap.AddRound();

                for (int i = 0; i < playerCount; i++)
                {
                    var stateAddr = stateRound.AddEntry<ulong>(i, 0, _playerArray + (uint)(i * 0x8));
                    var pawnAddr = pawnRound.AddEntry<ulong>(i, 1, stateAddr, null, Offsets.APlayerState.PawnPrivate);
                    idRound.AddEntry<uint>(i, 2, pawnAddr, null, Offsets.Actor.ID);
                }

                scatterMap.Execute();

                var playerBaseWithName = new Dictionary<ulong, uint>(playerCount);
                var playerStateMap = new Dictionary<ulong, ulong>(playerCount);

                for (int i = 0; i < playerCount; i++)
                {
                    var r = scatterMap.Results[i];
                    if (!r.TryGetValue(0, out var sr) || !sr.TryGetResult<ulong>(out var stateAddr) || stateAddr == 0) continue;
                    if (!r.TryGetValue(1, out var pr) || !pr.TryGetResult<ulong>(out var pawnAddr) || pawnAddr == 0) continue;
                    if (!r.TryGetValue(2, out var ir) || !ir.TryGetResult<uint>(out var nameId) || nameId == 0) continue;

                    playerBaseWithName[pawnAddr] = nameId;
                    playerStateMap[pawnAddr] = stateAddr;
                }

                ProcessPlayerEntities(playerBaseWithName, playerStateMap);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating players from PlayerArray: {ex}");
            }
        }

        private void UpdateVehiclesFromActorList()
        {
            try
            {
                var actorsTArray = _persistentLevel + Offsets.Level.Actors;
                var actorCount = Memory.ReadValue<int>(actorsTArray + 0x8);

                if (actorCount < 1 || actorCount > MaxActors) return;

                var actorsArrayPtr = Memory.ReadPtr(actorsTArray);
                if (actorsArrayPtr == 0) return;

                var scatterMap = new ScatterReadMap(actorCount);
                var actorRound = scatterMap.AddRound();
                var idRound = scatterMap.AddRound();
                var validationRound = scatterMap.AddRound();

                for (int i = 0; i < actorCount; i++)
                {
                    var actorAddr = actorRound.AddEntry<ulong>(i, 0, actorsArrayPtr + (uint)(i * 0x8));
                    var nameIdEntry = idRound.AddEntry<uint>(i, 1, actorAddr, null, Offsets.Actor.ID);
                    
                    // Consolidate validation reads into single round
                    validationRound.AddEntry<byte>(i, 2, actorAddr, null, Offsets.ASQVehicle.VehicleType);
                    validationRound.AddEntry<float>(i, 3, actorAddr, null, Offsets.ASQVehicle.MaxHealth);
                    validationRound.AddEntry<int>(i, 4, actorAddr, null, Offsets.ASQVehicle.VehicleSeats + 0x8);
                }

                scatterMap.Execute();

                var actorBaseWithName = new Dictionary<ulong, uint>();
                var vehicleValidationData = new Dictionary<ulong, (uint nameId, byte vehicleType, float maxHealth, int seatCount)>();
                
                for (int i = 0; i < actorCount; i++)
                {
                    var r = scatterMap.Results[i];
                    if (!r.TryGetValue(0, out var ar) || !ar.TryGetResult<ulong>(out var actorAddr) || actorAddr == 0) continue;
                    if (!r.TryGetValue(1, out var ir) || !ir.TryGetResult<uint>(out var nameId) || nameId == 0) continue;
                    
                    actorBaseWithName[actorAddr] = nameId;
                    
                    // Store validation data for later processing
                    if (r.TryGetValue(2, out var vtResult) && vtResult.TryGetResult<byte>(out var vtByte) &&
                        r.TryGetValue(3, out var mhResult) && mhResult.TryGetResult<float>(out var maxHealth) &&
                        r.TryGetValue(4, out var scResult) && scResult.TryGetResult<int>(out var seatCount))
                    {
                        vehicleValidationData[actorAddr] = (nameId, vtByte, maxHealth, seatCount);
                    }
                }

                ProcessVehicleEntities(actorBaseWithName, vehicleValidationData);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating vehicles from ActorList: {ex}");
            }
        }

        private void ProcessPlayerEntities(Dictionary<ulong, uint> playerBaseWithName, Dictionary<ulong, ulong> playerStateMap)
        {
            var existingPlayers = _actors
                .Where(kv => kv.Value.ActorType == ActorType.Player)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var toRemove = new HashSet<ulong>(existingPlayers.Keys);

            if (playerBaseWithName.Count == 0)
            {
                foreach (var id in toRemove)
                    _actors.TryRemove(id, out _);
                return;
            }

            var newEntities = playerBaseWithName
                .Where(kv => !existingPlayers.ContainsKey(kv.Key) || existingPlayers[kv.Key].NameId != kv.Value)
                .ToDictionary();

            if (newEntities.Count > 0)
            {
                var names = Memory.GetNamesById([.. newEntities.Values.Distinct()]);

                foreach (var (nameId, name) in names.Where(x => x.Value.StartsWith("BP_UAF")).ToList())
                    names[nameId] = name.Replace("BP_UAF", "BP_Soldier_UAF");

                var soldierNames = names.Where(x => x.Value.StartsWith("BP_Soldier")).ToDictionary();

                foreach (var (pawnAddr, nameId) in newEntities.Where(kv => soldierNames.ContainsKey(kv.Value)))
                    ReallocatePlayerActor(pawnAddr, playerStateMap[pawnAddr], Team.Unknown, nameId, soldierNames[nameId]);
            }

            foreach (var pawnAddr in playerBaseWithName.Keys.Where(existingPlayers.ContainsKey))
                toRemove.Remove(pawnAddr);

            foreach (var id in toRemove)
                _actors.TryRemove(id, out _);
        }

        private void ProcessVehicleEntities(Dictionary<ulong, uint> actorBaseWithName, Dictionary<ulong, (uint nameId, byte vehicleType, float maxHealth, int seatCount)> vehicleValidationData)
        {
            var notUpdated = new HashSet<ulong>(_actors
                .Where(kv => kv.Value.ActorType != ActorType.Player)
                .Select(kv => kv.Key));

            foreach (var item in actorBaseWithName.ToList())
            {
                if (_actors.TryGetValue(item.Key, out var existing) && existing.NameId == item.Value)
                {
                    notUpdated.Remove(item.Key);
                    actorBaseWithName.Remove(item.Key);
                }
            }

            var names = Memory.GetNamesById([.. actorBaseWithName.Values.Distinct()]);

            var deployables = names.Where(x => Names.TechNames.ContainsKey(x.Value)).ToDictionary();
            foreach (var entry in actorBaseWithName.Where(a => deployables.ContainsKey(a.Value)).ToList())
            {
                var actorName = deployables[entry.Value];
                var actorType = Names.TechNames[actorName];

                if (_actors.TryGetValue(entry.Key, out var actor) && (actor.ErrorCount > 50 || actor.Base != entry.Key))
                {
                    Logger.Error($"Existing actor '{actor.Base}' being reallocated...");
                    ReallocateVehicleActor(entry.Key, Team.Unknown, actorType, entry.Value, actorName);
                }
                else if (!_actors.ContainsKey(entry.Key))
                {
                    ReallocateVehicleActor(entry.Key, Team.Unknown, actorType, entry.Value, actorName);
                }

                notUpdated.Remove(entry.Key);
            }

            // Process potential vehicles using pre-fetched validation data
            var potentialVehicles = actorBaseWithName
                .Where(a => !deployables.ContainsKey(a.Value))
                .ToList();

            foreach (var (actorAddr, nameId) in potentialVehicles)
            {
                if (!vehicleValidationData.TryGetValue(actorAddr, out var validationData)) continue;

                var actorName = names.TryGetValue(nameId, out var n) ? n : "Unknown";
                var vtByte = validationData.vehicleType;
                var maxHealth = validationData.maxHealth;
                var seatCount = validationData.seatCount;

                var vehicleType = (ESQVehicleType)vtByte;
                if (vehicleType == ESQVehicleType.None || vehicleType == ESQVehicleType.MAX || vtByte == 0 || vtByte > 18) continue;
                if (_vehicleNameBlacklist.Any(b => actorName.Contains(b, StringComparison.OrdinalIgnoreCase))) continue;
                if (seatCount < 1) continue;

                bool isHelicopter = vehicleType is ESQVehicleType.HelicopterTransport or ESQVehicleType.HelicopterAttack;
                if (maxHealth <= 1.0f && !isHelicopter) continue;

                if (!Names.VehicleTypeMap.TryGetValue(vehicleType, out var actorType)) continue;

                if (_actors.TryGetValue(actorAddr, out var actor) && (actor.ErrorCount > 50 || actor.Base != actorAddr))
                {
                    Logger.Error($"Existing vehicle '{actor.Base}' being reallocated...");
                    ReallocateVehicleActor(actorAddr, Team.Unknown, actorType, nameId, actorName);
                }
                else if (!_actors.ContainsKey(actorAddr))
                {
                    ReallocateVehicleActor(actorAddr, Team.Unknown, actorType, nameId, actorName);
                }

                notUpdated.Remove(actorAddr);
            }

            foreach (var id in notUpdated)
                _actors.TryRemove(id, out _);
        }

        private void ReallocatePlayerActor(ulong pawnBase, ulong playerStateBase, Team team, uint nameId, string actorName)
        {
            _actors[pawnBase] = new UActor(pawnBase)
            {
                Team = team,
                ActorType = ActorType.Player,
                NameId = nameId,
                Name = actorName,
                PlayerStateAddress = playerStateBase
            };
        }

        private void ReallocateVehicleActor(ulong actorBase, Team team, ActorType actorType, uint nameId, string actorName)
        {
            _actors[actorBase] = new UActor(actorBase)
            {
                Team = team,
                ActorType = actorType,
                NameId = nameId,
                Name = actorName,
                PlayerStateAddress = 0
            };
        }

        public void UpdateAllPlayers()
        {
            try
            {
                var count = _actors.Count;
                if (count < 1) throw new GameEnded();

                var actorBases = _actors.Values.Select(a => a.Base).Order().ToArray();

                var scatterMap = new ScatterReadMap(count);
                var instanceRound = scatterMap.AddRound();
                var instigatorRound = scatterMap.AddRound();
                var teamRound = scatterMap.AddRound();
                var vehicleTeamRound = scatterMap.AddRound();

                for (int i = 0; i < count; i++)
                {
                    var actorAddr = actorBases[i];
                    var actor = _actors[actorAddr];

                    var rootComponent = instanceRound.AddEntry<ulong>(i, 1, actorAddr + Offsets.Actor.RootComponent);

                    if (actor.ActorType == ActorType.Player)
                    {
                        instanceRound.AddEntry<float>(i, 2, actorAddr + Offsets.ASQSoldier.Health);

                        if (actor.PlayerStateAddress != 0)
                        {
                            teamRound.AddEntry<int>(i, 6, actor.PlayerStateAddress, null, Offsets.ASQPlayerState.TeamID);
                            teamRound.AddEntry<ulong>(i, 7, actor.PlayerStateAddress, null, Offsets.ASQPlayerState.SquadState);
                        }
                        else
                        {
                            var pawnPlayerState = instanceRound.AddEntry<ulong>(i, 3, actorAddr + Offsets.Pawn.PlayerState);
                            teamRound.AddEntry<int>(i, 6, pawnPlayerState, null, Offsets.ASQPlayerState.TeamID);
                            teamRound.AddEntry<ulong>(i, 7, pawnPlayerState, null, Offsets.ASQPlayerState.SquadState);
                        }

                        // ESP bone processing disabled for performance
                        // SetupESPEntries(instanceRound, meshRound, boneRound, i, actorAddr);
                    }
                    else if (Names.Deployables.Contains(actor.ActorType))
                    {
                        instanceRound.AddEntry<float>(i, 2, actorAddr + Offsets.SQDeployable.Health);
                        instanceRound.AddEntry<float>(i, 3, actorAddr + Offsets.SQDeployable.MaxHealth);
                        instanceRound.AddEntry<int>(i, 4, actorAddr + Offsets.SQDeployable.Team);
                    }
                    else
                    {
                        instanceRound.AddEntry<float>(i, 2, actorAddr + Offsets.ASQVehicle.Health);
                        instanceRound.AddEntry<float>(i, 3, actorAddr + Offsets.ASQVehicle.MaxHealth);
                        instanceRound.AddEntry<ulong>(i, 14, actorAddr + Offsets.ASQVehicle.ClaimedBySquad);
                        
                        // Add vehicle team detection to scatter reads
                        var seatsPtr = instanceRound.AddEntry<ulong>(i, 15, actorAddr + Offsets.ASQVehicle.VehicleSeats);
                        var firstSeat = instigatorRound.AddEntry<ulong>(i, 16, seatsPtr, null, 0x0);
                        var seatPawn = teamRound.AddEntry<ulong>(i, 17, firstSeat, null, Offsets.USQVehicleSeatComponent.SeatPawn);
                        vehicleTeamRound.AddEntry<byte>(i, 18, seatPawn, null, Offsets.ASQPawn.Team);
                    }

                    instigatorRound.AddEntry<double>(i, 8, rootComponent, null, Offsets.USceneComponent.RelativeLocation);
                    instigatorRound.AddEntry<double>(i, 9, rootComponent, null, Offsets.USceneComponent.RelativeLocation + 0x8);
                    instigatorRound.AddEntry<double>(i, 10, rootComponent, null, Offsets.USceneComponent.RelativeLocation + 0x10);
                    instigatorRound.AddEntry<double>(i, 11, rootComponent, null, Offsets.USceneComponent.RelativeRotation);
                    instigatorRound.AddEntry<double>(i, 12, rootComponent, null, Offsets.USceneComponent.RelativeRotation + 0x8);
                    instigatorRound.AddEntry<double>(i, 13, rootComponent, null, Offsets.USceneComponent.RelativeRotation + 0x10);
                }

                scatterMap.Execute();

                bool updateSquads = (DateTime.Now - _lastSquadUpdate).TotalMilliseconds > SquadUpdateInterval;

                for (int i = 0; i < count; i++)
                {
                    var actor = _actors[actorBases[i]];
                    var r = scatterMap.Results[i];

                    float hp = 0;
                    if (r.TryGetValue(2, out var hpResult) && hpResult.TryGetResult<float>(out hp))
                    {
                        if (actor.ActorType == ActorType.Player && actor.Health > 0 && hp <= 0)
                        {
                            actor.DeathPosition = actor.Position;
                            actor.TimeOfDeath = DateTime.Now;
                        }
                        actor.Health = hp;
                    }

                    if (r.TryGetValue(3, out var maxHpResult) && maxHpResult.TryGetResult<float>(out var maxHp) && maxHp > 0)
                        actor.Health = hp / maxHp * 100;

                    if (actor.ActorType == ActorType.Player)
                    {
                        if (r.TryGetValue(6, out var teamResult) && teamResult.TryGetResult<int>(out var teamId))
                            actor.TeamID = teamId;

                        if (actor.IsFriendly())
                        {
                            actor.SquadID = _squadCache.TryGetValue(actor.Base, out var cachedSquadId) ? cachedSquadId : -1;

                            if (updateSquads &&
                                r.TryGetValue(7, out var squadResult) &&
                                squadResult.TryGetResult<ulong>(out var squadStateAddr) &&
                                squadStateAddr != 0)
                            {
                                try
                                {
                                    var squadId = Memory.ReadValue<int>(squadStateAddr + Offsets.ASQSquadState.SquadId);
                                    if (squadId > 0 && squadId < 1000)
                                    {
                                        actor.SquadID = squadId;
                                        _squadCache[actor.Base] = squadId;
                                    }
                                }
                                catch { }
                            }
                        }

                        // ESP data processing disabled for performance
                        // UpdatePlayerESPData(actor, r);
                        // InitializeEmptyESPData(actor);
                    }
                    else if (Names.Deployables.Contains(actor.ActorType))
                    {
                        actor.TeamID = r.TryGetValue(4, out var dTeamResult) && dTeamResult.TryGetResult<int>(out var dTeamId)
                            ? (dTeamId is 1 or 2 ? dTeamId : -1)
                            : -1;
                    }
                    else
                    {
                        // Try to get vehicle team from scatter reads first
                        actor.TeamID = -1;
                        if (r.TryGetValue(18, out var teamByteResult) && teamByteResult.TryGetResult<byte>(out var teamByte))
                        {
                            var team = (ESQTeam)teamByte;
                            actor.TeamID = team switch
                            {
                                ESQTeam.Team_One => 1,
                                ESQTeam.Team_Two => 2,
                                ESQTeam.Team_Neutral => 0,
                                _ => -1
                            };
                        }

                        // Fallback to claimed squad if no team from seats
                        if (actor.TeamID == -1 &&
                            r.TryGetValue(14, out var claimedResult) &&
                            claimedResult.TryGetResult<ulong>(out var claimedBySquad) &&
                            claimedBySquad is > 0x10000 and < 0x7FFFFFFFFFFF)
                        {
                            try
                            {
                                var tid = Memory.ReadValue<int>(claimedBySquad + Offsets.ASQSquadState.TeamId);
                                actor.TeamID = tid is 1 or 2 ? tid : -1;
                            }
                            catch { }
                        }

                        if (r.TryGetValue(14, out var claimResult) && claimResult.TryGetResult<ulong>(out var claimPtr) && claimPtr != 0)
                        {
                            try
                            {
                                actor.IsClaimed = true;
                                actor.ClaimingSquadID = Memory.ReadValue<int>(claimPtr + Offsets.ASQSquadState.SquadId);
                            }
                            catch
                            {
                                actor.IsClaimed = false;
                                actor.ClaimingSquadID = -1;
                            }
                        }
                        else
                        {
                            actor.IsClaimed = false;
                            actor.ClaimingSquadID = -1;
                        }
                    }

                    if (r.TryGetValue(8, out var xr) && r.TryGetValue(9, out var yr) && r.TryGetValue(10, out var zr) &&
                        xr.TryGetResult<double>(out var x) && yr.TryGetResult<double>(out var y) && zr.TryGetResult<double>(out var z))
                    {
                        actor.Position = new Vector3D(x, y, z);
                    }

                    if (r.TryGetValue(11, out var rxr) && r.TryGetValue(12, out var ryr) && r.TryGetValue(13, out var rzr) &&
                        rxr.TryGetResult<double>(out var rotX) && ryr.TryGetResult<double>(out var rotY) && rzr.TryGetResult<double>(out var rotZ))
                    {
                        var rot = new Vector3D(rotX, rotY, rotZ);
                        actor.Rotation = new Vector2D(rot.Y, rot.X);
                        actor.Rotation3D = rot;
                    }
                }

                if (updateSquads)
                {
                    _lastSquadUpdate = DateTime.Now;
                    _squadCache = _squadCache
                        .Where(kv => _actors.ContainsKey(kv.Key))
                        .ToDictionary(kv => kv.Key, kv => kv.Value);
                }
            }
            catch (GameEnded) { throw; }
            catch (Exception ex)
            {
                Logger.Error($"CRITICAL ERROR - RegisteredActors Loop FAILED: {ex}");
            }
        }

        #region ESP Functionality (Ready to Enable)
        /// <summary>
        /// Sets up ESP scatter read entries for mesh and bone data.
        /// Call this from SetupPlayerEntries() to enable ESP.
        /// </summary>
        private void SetupESPEntries(ScatterReadRound playerInstanceInfoRound, ScatterReadRound meshRound, 
            ScatterReadRound boneInfoRound, int index, ulong actorAddr)
        {
            // Read mesh pointer - use key 20 to avoid collision with position/rotation keys (8-13)
            var meshPtr = playerInstanceInfoRound.AddEntry<ulong>(index, 20, actorAddr + Offsets.ACharacter.Mesh);
            
            // Read component to world transform - use key 21
            meshRound.AddEntry<FTransform>(index, 21, meshPtr, null, Offsets.USceneComponent.ComponentToWorld);
            var boneArrayPtr = meshRound.AddEntry<ulong>(index, 22, meshPtr, null, 0x5B8);

            for (int j = 0; j < _boneIds.Length; j++)
                boneInfoRound.AddEntry<FTransform>(index, 50 + j, boneArrayPtr, null, (uint)(_boneIds[j] * 0x30));
        }

        private void UpdatePlayerESPData(UActor actor, Dictionary<int, IScatterEntry> results)
        {
            if (actor.BoneScreenPositions == null || actor.BoneScreenPositions.Length != _boneIds.Length)
                actor.BoneScreenPositions = new Vector2[_boneIds.Length];

            if (!results.TryGetValue(20, out var meshResult) || !meshResult.TryGetResult<ulong>(out var meshAddr) || meshAddr == 0)
            {
                ClearESPData(actor);
                return;
            }

            actor.Mesh = meshAddr;

            actor.ComponentToWorld = results.TryGetValue(21, out var ctResult) && ctResult.TryGetResult<FTransform>(out var ct)
                ? ct
                : new FTransform();

            if (!results.TryGetValue(22, out var boneArrayResult) || !boneArrayResult.TryGetResult<ulong>(out var boneArrayPtr) || boneArrayPtr == 0)
            {
                ClearESPData(actor);
                return;
            }

            ProcessBoneTransforms(actor, results);
        }

        private void ProcessBoneTransforms(UActor actor, Dictionary<int, IScatterEntry> results)
        {
            var localPlayer = Memory._game?.LocalPlayer;
            if (localPlayer == null)
            {
                ClearESPData(actor);
                return;
            }
            
            // Setup view info for world-to-screen conversion
            var viewInfo = new MinimalViewInfo
            {
                Location = localPlayer.Position,
                Rotation = localPlayer.Rotation3D,
                FOV = Memory._game?.CurrentFOV ?? 90f
            };
            
            actor.BoneTransforms.Clear();
            bool anyBoneSuccess = false;

            for (int j = 0; j < _boneIds.Length; j++)
            {
                if (results.TryGetValue(50 + j, out var boneResult) && boneResult.TryGetResult<FTransform>(out var boneTransform))
                {
                    actor.BoneTransforms[_boneIds[j]] = boneTransform;
                    
                    // Transform to world space
                    Vector3 boneWorldPos = TransformToWorld(boneTransform, actor.ComponentToWorld);
                    Vector3D boneWorldPos3D = new Vector3D(boneWorldPos.X, boneWorldPos.Y, boneWorldPos.Z);
                    
                    // Convert to screen coordinates
                    Vector2 screenPos = Camera.WorldToScreen(viewInfo, boneWorldPos3D);
                    actor.BoneScreenPositions[j] = screenPos;

                    if (screenPos != Vector2.Zero)
                        anyBoneSuccess = true;
                }
                else
                {
                    actor.BoneScreenPositions[j] = Vector2.Zero;
                }
            }

            if (!anyBoneSuccess)
                ClearESPData(actor);
        }

        private void ClearESPData(UActor actor)
        {
            actor.Mesh = 0;
            if (actor.BoneScreenPositions != null)
                Array.Clear(actor.BoneScreenPositions, 0, actor.BoneScreenPositions.Length);
            actor.BoneTransforms.Clear();
        }

        private void InitializeEmptyESPData(UActor actor)
        {
            if (actor.BoneScreenPositions == null || actor.BoneScreenPositions.Length != _boneIds.Length)
                actor.BoneScreenPositions = new Vector2[_boneIds.Length];
            Array.Clear(actor.BoneScreenPositions, 0, actor.BoneScreenPositions.Length);
            actor.Mesh = 0;
            actor.BoneTransforms.Clear();
        }

        private Vector3 TransformToWorld(FTransform boneTransform, FTransform componentToWorld)
        {
            boneTransform.Scale3D = new Vector3(1, 1, 1);
            componentToWorld.Scale3D = new Vector3(1, 1, 1);
            var final = boneTransform.ToMatrix() * componentToWorld.ToMatrix();
            return new Vector3(final.M41, final.M42, final.M43);
        }
        #endregion
    }
}