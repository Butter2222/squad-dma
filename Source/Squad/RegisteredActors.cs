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
        private readonly Stopwatch _vehicleUpdateSw = new(); // Separate timer for vehicles
        private readonly ConcurrentDictionary<ulong, UActor> _actors = new();
        private Dictionary<ulong, int> _squadCache = new();
        private DateTime _lastSquadUpdate = DateTime.MinValue;
        private const int SquadUpdateInterval = 1000; // Update every 1 second

        // Bone IDs for ESP
        private static readonly int[] _boneIds = { 7, 6, 5, 3, 2, 65, 66, 67, 68, 92, 93, 94, 95, 130, 131, 132, 125, 126, 127 };

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
                        // Get actor count from the actor array (for vehicles/deployables)
                        var actorsTArray = _persistentLevel + Offsets.Level.Actors;
                        var actorCount = Memory.ReadValue<int>(actorsTArray + 0x8);

                        // Get player count from player array
                        if (_gameState == 0)
                        {
                            _gameState = Memory.ReadPtr(_gameWorld + Offsets.World.GameState);
                        }

                        int playerCount = 0;
                        if (_gameState != 0)
                        {
                            var playerArrayTArray = _gameState + Offsets.AGameStateBase.PlayerArray;
                            var playerArrayPtr = Memory.ReadPtr(playerArrayTArray);
                            if (playerArrayPtr != 0)
                            {
                                playerCount = Memory.ReadValue<int>(playerArrayTArray + 0x8);
                                if (playerCount < 0 || playerCount > 200) playerCount = 0; // Sanitize
                            }
                        }

                        // Return combined count for hybrid system
                        var totalCount = actorCount + playerCount;
                        if (totalCount < 1)
                        {
                            this._actors.Clear();
                            return -1;
                        }

                        return totalCount;
                    }
                    catch (DMAShutdown)
                    {
                        throw;
                    }
                    catch (Exception ex) when (attempt < maxAttempts - 1)
                    {
                        Logger.Error($"ActorCount attempt {attempt + 1} failed: {ex}");
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
        public RegisteredActors(ulong gameWorldAddr)
        {
            this._gameWorld = gameWorldAddr;
            this._persistentLevel = Memory.ReadPtr(_gameWorld + Offsets.World.PersistentLevel);
            this.Actors = new(this._actors);
            this._regSw.Start();
            this._vehicleUpdateSw.Start();
        }

        #region Update List/Player Functions
        public Dictionary<ulong, uint> GetActorBaseWithName()
        {
            return _actors.Values
                .Where(actor => actor.NameId != 0)
                .ToDictionary(actor => actor.Base, actor => actor.NameId);
        }


        public void UpdateList()
        {
            if (this._regSw.ElapsedMilliseconds < 300)
                return;

            try
            {
                UpdatePlayersFromPlayerArray();
                
                // Update vehicles less frequently (every 500ms) since they move slower
                if (_vehicleUpdateSw.ElapsedMilliseconds >= 300)
                {
                    UpdateVehiclesFromActorList();
                    _vehicleUpdateSw.Restart();
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
        }


        private void UpdatePlayersFromPlayerArray()
        {
            try
            {
                // Get GameState if not cached
                if (_gameState == 0)
                {
                    _gameState = Memory.ReadPtr(_gameWorld + Offsets.World.GameState);
                    if (_gameState == 0) return;
                }

                // Get PlayerArray TArray address and read data pointer + count
                var playerArrayTArray = _gameState + Offsets.AGameStateBase.PlayerArray;
                _playerArray = Memory.ReadPtr(playerArrayTArray);
                var playerCount = Memory.ReadValue<int>(playerArrayTArray + 0x8);
                
                if (_playerArray == 0 || playerCount <= 0 || playerCount > 200)
                    return;

                var playerScatterMap = new ScatterReadMap(playerCount);
                var playerStateRound = playerScatterMap.AddRound();
                var pawnRound = playerScatterMap.AddRound();
                var pawnIdRound = playerScatterMap.AddRound();

                for (int i = 0; i < playerCount; i++)
                {
                    var playerStateAddr = playerStateRound.AddEntry<ulong>(i, 0, _playerArray + (uint)(i * 0x8));
                    var pawnAddr = pawnRound.AddEntry<ulong>(i, 1, playerStateAddr, null, Offsets.APlayerState.PawnPrivate);
                    pawnIdRound.AddEntry<uint>(i, 2, pawnAddr, null, Offsets.Actor.ID);
                }

                playerScatterMap.Execute();

                // Pre-allocate dictionaries for better performance
                var playerBaseWithName = new Dictionary<ulong, uint>(playerCount);
                var playerStateMap = new Dictionary<ulong, ulong>(playerCount);
                
                // Process results efficiently
                for (int i = 0; i < playerCount; i++)
                {
                    var results = playerScatterMap.Results[i];
                    
                    if (!results.TryGetValue(0, out var playerStateResult) || 
                        !playerStateResult.TryGetResult<ulong>(out var playerStateAddr) || playerStateAddr == 0 ||
                        !results.TryGetValue(1, out var pawnResult) || 
                        !pawnResult.TryGetResult<ulong>(out var pawnAddr) || pawnAddr == 0 ||
                        !results.TryGetValue(2, out var pawnIdResult) || 
                        !pawnIdResult.TryGetResult<uint>(out var pawnNameId) || pawnNameId == 0)
                        continue;
                    
                    playerBaseWithName[pawnAddr] = pawnNameId;
                    playerStateMap[pawnAddr] = playerStateAddr;
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
                // Always use direct calculation - no caching, no cooldowns, no bullshit
                var actorsTArray = _persistentLevel + Offsets.Level.Actors;
                var actorCount = Memory.ReadValue<int>(actorsTArray + 0x8);
                
                // Simple bounds check
                if (actorCount < 1 || actorCount > 20000) 
                {
                    return; // Just skip silently if count is invalid
                }

                // Get actors array pointer directly
                var actorsArrayPtr = Memory.ReadPtr(actorsTArray);
                if (actorsArrayPtr == 0)
                {
                    return; // Skip silently if pointer is null
                }

                var actorScatterMap = new ScatterReadMap(actorCount);
                var actorRound = actorScatterMap.AddRound();
                var actorIdRound = actorScatterMap.AddRound();

                for (int i = 0; i < actorCount; i++)
                {
                    var actorAddr = actorRound.AddEntry<ulong>(i, 0, actorsArrayPtr + (uint)(i * 0x8));
                    var actorId = actorIdRound.AddEntry<uint>(i, 1, actorAddr, null, Offsets.Actor.ID);
                }

                actorScatterMap.Execute();

                var actorBaseWithName = new Dictionary<ulong, uint>();
                for (int i = 0; i < actorCount; i++)
                {
                    if (!actorScatterMap.Results[i].TryGetValue(0, out var actorResult) || 
                        !actorResult.TryGetResult<ulong>(out var actorAddr) || actorAddr == 0)
                        continue;
                    if (!actorScatterMap.Results[i].TryGetValue(1, out var actorIdResult) || 
                        !actorIdResult.TryGetResult<uint>(out var actorNameId) || actorNameId == 0)
                        continue;
                    actorBaseWithName[actorAddr] = actorNameId;
                }

                ProcessVehicleEntities(actorBaseWithName);
            }
            catch (Exception ex)
            {
                // Just log the error and continue - no complex recovery logic
                Logger.Error($"Error updating vehicles from ActorList: {ex}");
            }
        }


        private void ProcessPlayerEntities(Dictionary<ulong, uint> playerBaseWithName, Dictionary<ulong, ulong> playerStateMap)
        {
            var existingPlayers = _actors.Where(kv => kv.Value.ActorType == ActorType.Player).ToDictionary(kv => kv.Key, kv => kv.Value);
            var playersToRemove = new HashSet<ulong>(existingPlayers.Keys);
            
            if (playerBaseWithName.Count == 0)
            {
                // Remove all players that are no longer rendered
                foreach (var actorId in playersToRemove)
                    _actors.TryRemove(actorId, out _);
                return;
            }
            
            // Get names only for new/changed entities
            var newEntities = playerBaseWithName.Where(kv => !existingPlayers.ContainsKey(kv.Key) || existingPlayers[kv.Key].NameId != kv.Value).ToDictionary();
            
            if (newEntities.Count > 0)
            {
                var names = Memory.GetNamesById([.. newEntities.Values.Distinct()]);
                
                // Handle UAF naming convention
                foreach (var (nameId, name) in names.Where(x => x.Value.StartsWith("BP_UAF")).ToList())
                    names[nameId] = name.Replace("BP_UAF", "BP_Soldier_UAF");
                
                var soldierNames = names.Where(x => x.Value.StartsWith("BP_Soldier")).ToDictionary();
                
                foreach (var (pawnAddr, nameId) in newEntities.Where(kv => soldierNames.ContainsKey(kv.Value)))
                {
                    var actorName = soldierNames[nameId];
                    var team = Names.Teams.GetValueOrDefault(actorName[..14], Team.Unknown);
                    var playerStateAddr = playerStateMap[pawnAddr];
                    
                    ReallocatePlayerActor(pawnAddr, playerStateAddr, team, nameId, actorName);
                }
            }
            
            // Mark existing players as still active
            foreach (var pawnAddr in playerBaseWithName.Keys.Where(existingPlayers.ContainsKey))
                playersToRemove.Remove(pawnAddr);

            // Remove players that are no longer rendered
            foreach (var actorId in playersToRemove)
                _actors.TryRemove(actorId, out _);
        }

        private void ProcessVehicleEntities(Dictionary<ulong, uint> actorBaseWithName)
        {
            var notUpdated = new HashSet<ulong>(_actors.Where(kv => kv.Value.ActorType != ActorType.Player).Select(kv => kv.Key));
            
            foreach (var item in actorBaseWithName.ToList())
            {
                if (_actors.ContainsKey(item.Key) && _actors[item.Key].NameId == item.Value)
                {
                    notUpdated.Remove(item.Key);
                    actorBaseWithName.Remove(item.Key);
                }
            }
            
            var names = Memory.GetNamesById([.. actorBaseWithName.Values.Distinct()]);
            var vehiclesNameIDs = names.Where(x => Names.TechNames.ContainsKey(x.Value)).ToDictionary();
            var filteredVehicles = actorBaseWithName.Where(actor => vehiclesNameIDs.ContainsKey(actor.Value)).ToList();
            
            foreach (var vehicleEntry in filteredVehicles)
            {
                var actorAddr = vehicleEntry.Key;
                var nameId = vehicleEntry.Value;
                var actorName = vehiclesNameIDs[nameId];
                var actorType = Names.TechNames[actorName];
                var team = Team.Unknown;
                
                if (_actors.TryGetValue(actorAddr, out var actor))
                {
                    if (actor.ErrorCount > 50 || actor.Base != actorAddr)
                    {
                        Logger.Error($"Existing vehicle '{actor.Base}' being reallocated...");
                        ReallocateVehicleActor(actorAddr, team, actorType, nameId, actorName);
                    }
                }
                else
                {
                    ReallocateVehicleActor(actorAddr, team, actorType, nameId, actorName);
                }
                notUpdated.Remove(actorAddr);
            }

            // Remove old vehicles that are no longer in the ActorList
            foreach (var actorIdToRemove in notUpdated)
            {
                _actors.TryRemove(actorIdToRemove, out var _);
            }
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
                PlayerStateAddress = 0 // Vehicles don't have PlayerState
            };
        }

        /// <summary>
        /// Updates all 'Player' values (Position,health,direction,etc.)
        /// </summary>
        public void UpdateAllPlayers()
        {
            try
            {
                var count = _actors.Count;
                if (count < 1) 
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
                    var actor = _actors[actorAddr];

                    var rootComponent = playerInstanceInfoRound.AddEntry<ulong>(i, 1, actorAddr + Offsets.Actor.RootComponent);

                    if (actor.ActorType == ActorType.Player)
                    {
                        playerInstanceInfoRound.AddEntry<float>(i, 2, actorAddr + Offsets.ASQSoldier.Health);

                        // Use the stored PlayerState address for team/squad info (new method)
                        if (actor.PlayerStateAddress != 0)
                        {
                            teamInfoRound.AddEntry<int>(i, 6, actor.PlayerStateAddress, null, Offsets.ASQPlayerState.TeamID);
                            teamInfoRound.AddEntry<ulong>(i, 7, actor.PlayerStateAddress, null, Offsets.ASQPlayerState.SquadState);
                        }
                        else
                        {
                            // Fallback to old method for team info
                        var pawnPlayerState = playerInstanceInfoRound.AddEntry<ulong>(i, 3, actorAddr + Offsets.Pawn.PlayerState);
                        teamInfoRound.AddEntry<int>(i, 6, pawnPlayerState, null, Offsets.ASQPlayerState.TeamID);
                            teamInfoRound.AddEntry<ulong>(i, 7, pawnPlayerState, null, Offsets.ASQPlayerState.SquadState);
                        }

                        // ESP bone tracking - Uncomment these lines to enable ESP
                         SetupESPEntries(playerInstanceInfoRound, meshRound, boneInfoRound, i, actorAddr);
                    }
                    else if (Names.Deployables.Contains(actor.ActorType))
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
                        // Get team ID from PlayerState
                        if (results.TryGetValue(6, out var teamResult) && teamResult.TryGetResult<int>(out var teamId))
                        {
                            actor.TeamID = teamId;
                        }

                        // Handle squad information
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

                            if (updateSquads && results.TryGetValue(7, out var squadStateResult) && 
                                squadStateResult.TryGetResult<ulong>(out var squadStateAddr) && squadStateAddr != 0)
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
                                catch { /* Silently fail */ }
                            }
                        }

                        // ESP bone tracking - Uncomment to enable
                         UpdatePlayerESPData(actor, results);
                        
                        // Initialize empty bone data when ESP is disabled
                        InitializeEmptyESPData(actor);
                    }
                    else if (Names.Deployables.Contains(actor.ActorType))
                    {
                        if (results.TryGetValue(4, out var teamResult) &&
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

        #region ESP Functionality (Ready to Enable)
        /// <summary>
        /// Sets up ESP scatter read entries for mesh and bone data.
        /// Call this from SetupPlayerEntries() to enable ESP.
        /// </summary>
        private void SetupESPEntries(ScatterReadRound playerInstanceInfoRound, ScatterReadRound meshRound, 
            ScatterReadRound boneInfoRound, int index, ulong actorAddr)
        {
            // Read mesh pointer - use key 20 to avoid collision with position/rotation keys (8-13)
            var meshPtr = playerInstanceInfoRound.AddEntry<ulong>(index, 20, actorAddr + Offsets.ASQSoldier.Mesh);
            
            // Read component to world transform - use key 21
            meshRound.AddEntry<FTransform>(index, 21, meshPtr, null, Offsets.USceneComponent.ComponentToWorld);
            
            // Read bone array pointer - use key 22
            var boneArrayPtr = meshRound.AddEntry<ulong>(index, 22, meshPtr, null, 0x5B8);
            
            // Read individual bone transforms - start from key 50 to avoid all other keys
            for (int j = 0; j < _boneIds.Length; j++)
            {
                boneInfoRound.AddEntry<FTransform>(index, 50 + j, boneArrayPtr, null, (uint)(_boneIds[j] * 0x30));
            }
        }
        
        /// <summary>
        /// Updates mesh and bone data for ESP visualization.
        /// Call this from the main update loop to enable ESP.
        /// </summary>
        private void UpdatePlayerESPData(UActor actor, Dictionary<int, IScatterEntry> results)
        {
            // Initialize bone positions array if needed
            if (actor.BoneScreenPositions == null || actor.BoneScreenPositions.Length != _boneIds.Length)
            {
                actor.BoneScreenPositions = new Vector2[_boneIds.Length];
            }
            
            // Get mesh pointer - using key 20
            if (!results.TryGetValue(20, out var meshResult) || !meshResult.TryGetResult<ulong>(out var meshAddr) || meshAddr == 0)
            {
                ClearESPData(actor);
                return;
            }
            
            actor.Mesh = meshAddr;
            
            // Get component to world transform - using key 21
            if (results.TryGetValue(21, out var componentResult) && componentResult.TryGetResult<FTransform>(out var componentToWorld))
            {
                actor.ComponentToWorld = componentToWorld;
            }
            else
            {
                actor.ComponentToWorld = new FTransform(); // Default transform
            }
            
            // Get bone array pointer - using key 22
            if (!results.TryGetValue(22, out var boneArrayResult) || !boneArrayResult.TryGetResult<ulong>(out var boneArrayPtr) || boneArrayPtr == 0)
            {
                ClearESPData(actor);
                return;
            }
            
            // Process bone transforms
            ProcessBoneTransforms(actor, results);
        }
        
        /// <summary>
        /// Processes bone transforms and converts to screen coordinates.
        /// </summary>
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
            
            // Process each bone - using keys starting from 50
            for (int j = 0; j < _boneIds.Length; j++)
            {
                int resultKey = 50 + j;
                
                if (results.TryGetValue(resultKey, out var boneResult) && boneResult.TryGetResult<FTransform>(out var boneTransform))
                {
                    // Store bone transform
                    actor.BoneTransforms[_boneIds[j]] = boneTransform;
                    
                    // Transform to world space
                    Vector3 boneWorldPos = TransformToWorld(boneTransform, actor.ComponentToWorld);
                    Vector3D boneWorldPos3D = new Vector3D(boneWorldPos.X, boneWorldPos.Y, boneWorldPos.Z);
                    
                    // Convert to screen coordinates
                    Vector2 screenPos = Camera.WorldToScreen(viewInfo, boneWorldPos3D);
                    actor.BoneScreenPositions[j] = screenPos;
                    
                    if (screenPos != Vector2.Zero)
                    {
                        anyBoneSuccess = true;
                    }
                }
                else
                {
                    actor.BoneScreenPositions[j] = Vector2.Zero;
                }
            }
            
            // Clear if no bones were processed successfully
            if (!anyBoneSuccess)
            {
                ClearESPData(actor);
            }
        }
        
        /// <summary>
        /// Clears ESP data for an actor.
        /// </summary>
        private void ClearESPData(UActor actor)
        {
            actor.Mesh = 0;
            if (actor.BoneScreenPositions != null)
            {
                Array.Clear(actor.BoneScreenPositions, 0, actor.BoneScreenPositions.Length);
            }
            actor.BoneTransforms.Clear();
        }
        
        /// <summary>
        /// Initializes empty ESP data when ESP is disabled.
        /// </summary>
        private void InitializeEmptyESPData(UActor actor)
        {
            if (actor.BoneScreenPositions == null || actor.BoneScreenPositions.Length != _boneIds.Length)
            {
                actor.BoneScreenPositions = new Vector2[_boneIds.Length];
            }
            Array.Clear(actor.BoneScreenPositions, 0, actor.BoneScreenPositions.Length);
            actor.Mesh = 0;
            actor.BoneTransforms.Clear();
        }

        #endregion

        #region Helper Methods
        private Vector3 TransformToWorld(FTransform boneTransform, FTransform componentToWorld)
        {
            boneTransform.Scale3D = new Vector3(1, 1, 1);
            componentToWorld.Scale3D = new Vector3(1, 1, 1);
            Matrix4x4 boneMatrix = boneTransform.ToMatrix();
            Matrix4x4 worldMatrix = componentToWorld.ToMatrix();
            Matrix4x4 finalMatrix = boneMatrix * worldMatrix;
            return new Vector3(finalMatrix.M41, finalMatrix.M42, finalMatrix.M43);
        }
        #endregion
    }
}