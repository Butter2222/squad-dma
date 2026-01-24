using Offsets;
using squad_dma.Source.Misc;
using squad_dma.Source.Squad.Debug;
using squad_dma.Source.Squad.Features;
using System.Collections.ObjectModel;
using System.Numerics;

namespace squad_dma
{
    /// <summary>
    /// Class containing Game instance.
    /// </summary>
    public class Game
    {
        #region Fields
        private readonly ulong _squadBase;
        private volatile bool _inGame = false;
        private RegisteredActors _actors;
        private UActor _localUPlayer;
        private ulong _gameWorld;
        private ulong _gameInstance;
        private ulong _localPlayer;
        private ulong _playerController;
        private Vector3D _absoluteLocation;
        private string _currentLevel = string.Empty;
        private DateTime _lastTeamCheck = DateTime.MinValue;
        private const int TeamCheckInterval = 1000;

        private ulong _currentWeaponPtr;
        private ulong _lastNoRecoilWeaponPtr;
        private bool _isAimingDownSights;
        private bool _hasPipScope;
        private float _currentFOV;
        private int _magnificationIndex;
        private bool _isFiring = false;
        private PlayerState _currentPlayerState = PlayerState.Unknown;
        private PlayerState _previousPlayerState = PlayerState.Unknown;

        public Source.Squad.Manager _soldierManager;

        private GameTickets _gameTickets;
        private PlayerStats _gameStats;
        private DebugVehicles _debugVehicles;
        private DebugTeam _debugTeam;
        private DebugSoldier _debugSoldier;
        #endregion

        #region Properties
        public bool InGame => _inGame;
        public string MapName => _currentLevel;
        public UActor LocalPlayer => _localUPlayer;
        public ReadOnlyDictionary<ulong, UActor> Actors => _actors?.Actors;
        public Vector3D AbsoluteLocation => _absoluteLocation;
        public Dictionary<int, int> TeamTickets => _gameTickets?.GetTickets();
        public GameTickets GameTickets => _gameTickets;
        public PlayerStats GameStats => _gameStats;
        public bool IsAimingDownSights => _isAimingDownSights;
        public bool HasPipScope => _hasPipScope;
        public float CurrentFOV => _currentFOV;
        public bool IsFiring => _isFiring;
        public int MagnificationIndex => _magnificationIndex;
        public PlayerState CurrentPlayerState => _currentPlayerState;
        #endregion

        #region Constructor
        public Game(ulong squadBase)
        {
            _squadBase = squadBase;
            _gameTickets = null;
            _gameStats = null;
            _lastNoRecoilWeaponPtr = 0;
        }
        #endregion

        #region Public Methods
        public void SetSuppression(bool enable) => _soldierManager?.SetSuppression(enable);
        public void SetInteractionDistances(bool enable) => _soldierManager?.SetInteractionDistances(enable);
        public void SetShootingInMainBase(bool enable) => _soldierManager?.SetShootingInMainBase(enable);
        public void SetSpeedHack(bool enable) => _soldierManager?.SetSpeedHack(enable);
        public void SetAirStuck(bool enable) => _soldierManager?.SetAirStuck(enable);
        public void ForceDisableAirStuck() => _soldierManager?.ForceDisableAirStuck();
        public void DisableCollision(bool disable) => _soldierManager?.DisableCollision(disable);
        public void SetQuickZoom(bool enable) => _soldierManager?.SetQuickZoom(enable);
        public void SetRapidFire(bool enable) => _soldierManager?.SetRapidFire(enable);
        public void SetInfiniteAmmo(bool enable) => _soldierManager?.SetInfiniteAmmo(enable);
        public void SetQuickSwap(bool enable) => _soldierManager?.SetQuickSwap(enable);
        public void SetForceFullAuto(bool enable) => _soldierManager?.SetFullAuto(enable);
        public void SetNoCameraShake(bool enable) => _soldierManager?.SetNoCameraShake(enable);
        public void SetNoSpread(bool enable) => _soldierManager?.SetNoSpread(enable);
        public void SetNoRecoil(bool enable) => _soldierManager?.SetNoRecoil(enable);
        public void SetNoSway(bool enable) => _soldierManager?.SetNoSway(enable);
        public void SetInstantGrenade(bool enable) => _soldierManager?.SetInstantGrenade(enable);
        
        public PlayerState GetPlayerState() => _currentPlayerState;
        
        public void LogVehicles(bool force = false) => _debugVehicles?.LogVehicles(force);
        public void VehicleTeam() => _debugVehicles?.VehicleTeam();
        public void LogTeamInfo() => _debugTeam?.LogTeamInfo();
        public void ReadCurrentWeapons(bool includeOtherPlayers = false) => _debugSoldier?.ReadCurrentWeapons(includeOtherPlayers);
        public void ListVehicles() => _debugVehicles?.ListVehicles();
        
        public void WaitForGame()
        {
            while (true)
            {
                try
                {
                    if (!Memory.GetModuleBase())
                    {
                        throw new GameNotRunningException("Process terminated during wait");
                    }

                    if (GetGameWorld() && GetGameInstance() && GetCurrentLevel() && InitActors() && GetLocalPlayer())
                    {
                        if (!Memory.GetModuleBase())
                        {
                            throw new GameNotRunningException("Process terminated during initialization");
                        }

                        Thread.Sleep(1000);
                        Logger.Info("Game has started!!");
                        
                        if (!string.IsNullOrEmpty(_currentLevel))
                        {
                            Logger.Info($"Map is \"{_currentLevel}\"");
                        }
                        else
                        {
                            Logger.Error("Map not found");
                        }
                        
                        this._inGame = true;
                        Memory.GameStatus = GameStatus.InGame;
                        
                        _gameTickets = new GameTickets(_gameWorld, _localUPlayer);
                        _gameStats = new PlayerStats(_playerController);
                        
                        InitializeManagers();
                        
                        return;
                    }
                }
                catch (GameNotRunningException)
                {
                    throw; // Propagate up to break out of wait loop
                }
                Thread.Sleep(500);
            }
        }

        public void GameLoop()
        {
            try
            {
                if (!this._inGame)
                {
                    throw new GameEnded("Game has ended!");
                }

                UpdateLocalPlayerInfo();
                this._actors.UpdateList();
                this._actors.UpdateAllPlayers();
            }
            catch (DMAShutdown)
            {
                HandleDMAShutdown();
            }
            catch (GameEnded e)
            {
                HandleGameEnded(e);
            }
            catch (Exception ex)
            {
                HandleUnexpectedException(ex);
            }
        }
        #endregion

        #region Private Methods
        private void InitializeManagers()
        {
            _soldierManager = new Source.Squad.Manager(_playerController, _inGame, _actors, this);

            _debugVehicles = new DebugVehicles(_playerController, _inGame, _actors);
            _debugTeam = new DebugTeam(_inGame, _localUPlayer, _actors?.Actors);
            _debugSoldier = new DebugSoldier(_playerController, _inGame);
        }

        private bool TryExecute(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch { return false; }
        }

        private void HandleDMAShutdown()
        {
            Logger.Info("DMA shutdown");
            this._inGame = false;
        }

        private void HandleGameEnded(GameEnded e)
        {
            Logger.Info("Game has ended!");
            this._inGame = false;
            Memory.GameStatus = GameStatus.Menu;
            Memory.Restart();
        }

        private void HandleUnexpectedException(Exception ex)
        {
            Logger.Error($"CRITICAL ERROR - Game ended due to unhandled exception: {ex}");
            this._inGame = false;
        }

        private bool GetGameWorld() => 
            TryExecute(() => _gameWorld = Memory.ReadPtr(_squadBase + Offsets.GameObjects.GWorld));
  
        private bool GetGameInstance() => 
            TryExecute(() => _gameInstance = Memory.ReadPtr(_gameWorld + Offsets.World.OwningGameInstance));

        private bool GetCurrentLevel() => 
            TryExecute(() => 
            {
                var currentLayer = Memory.ReadPtr(_gameInstance + Offsets.GameInstance.CurrentLayer);
                var currentLevelIdPtr = currentLayer + Offsets.SQLayer.LevelID;
                var currentLevelId = Memory.ReadValue<uint>(currentLevelIdPtr);
                _currentLevel = Memory.GetNamesById([currentLevelId])[currentLevelId];
            });

        private bool InitActors() => 
            TryExecute(() => 
            {
                _actors = new RegisteredActors(_gameWorld);
            });
  
        private bool GetLocalPlayer() => 
            TryExecute(() => 
            {
                var localPlayers = Memory.ReadPtr(_gameInstance + Offsets.GameInstance.LocalPlayers);
                _localPlayer = Memory.ReadPtr(localPlayers);
                _localUPlayer = new UActor(_localPlayer);
                _localUPlayer.Team = Team.Unknown;
                GetPlayerController();
            });

        private bool GetPlayerController() => 
            TryExecute(() => _playerController = Memory.ReadPtr(_localPlayer + Offsets.UPlayer.PlayerController));

        private bool UpdateLocalPlayerInfo()
        {
            try
            {
                if ((DateTime.Now - _lastTeamCheck).TotalMilliseconds > TeamCheckInterval)
                {
                    _lastTeamCheck = DateTime.Now;

                    try
                    {
                        ulong playerState = Memory.ReadPtr(_playerController + Offsets.Controller.PlayerState);
                        if (playerState == 0)
                            return false;

                        int teamId = Memory.ReadValue<int>(playerState + Offsets.ASQPlayerState.TeamID);
                        _localUPlayer.TeamID = teamId;

                        ulong squadState = Memory.ReadPtr(_playerController + Offsets.SQPlayerController.SquadState);
                        if (squadState != 0)
                        {
                            int squadId = Memory.ReadValue<int>(squadState + Offsets.ASQSquadState.SquadId);
                            _localUPlayer.SquadID = squadId;
                        }
                    }
                    catch { return false; }
                }
                GetCameraCache();
                ProcessPlayerInfo();
                DetectPlayerState();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool GetCameraCache()
        {
            try
            {
                if (_playerController == 0 || _gameWorld == 0)
                    return false;
                
                var cameraInfoScatterMap = new ScatterReadMap(1);
                var cameraManagerRound = cameraInfoScatterMap.AddRound();
                var cameraInfoRound = cameraInfoScatterMap.AddRound();

                var cameraManagerPtr = Memory.ReadPtr(_playerController + Offsets.PlayerController.PlayerCameraManager);
                if (cameraManagerPtr == 0)
                    return false;

                var viewTargetPtr = cameraManagerPtr + Offsets.PlayerCameraManager.ViewTarget;
                var povPtr = viewTargetPtr + Offsets.FTViewTarget.POV;
                var worldOriginPtr = _gameWorld + Offsets.World.WorldOrigin;

                // World Origin (FVector)
                cameraManagerRound.AddEntry<double>(0, 11, worldOriginPtr);      // World Origin X
                cameraManagerRound.AddEntry<double>(0, 12, worldOriginPtr + 0x8);  // World Origin Y
                cameraManagerRound.AddEntry<double>(0, 13, worldOriginPtr + 0x10); // World Origin Z

                // FMinimalViewInfo structure
                // Location (0x0)
                cameraInfoRound.AddEntry<double>(0, 14, povPtr + 0x0);  // X
                cameraInfoRound.AddEntry<double>(0, 15, povPtr + 0x8);  // Y
                cameraInfoRound.AddEntry<double>(0, 16, povPtr + 0x10); // Z
                
                // Rotation (0x18)
                cameraInfoRound.AddEntry<double>(0, 17, povPtr + 0x18); // Pitch
                cameraInfoRound.AddEntry<double>(0, 18, povPtr + 0x20); // Yaw
                cameraInfoRound.AddEntry<double>(0, 19, povPtr + 0x28); // Roll

                cameraInfoScatterMap.Execute();

                if (cameraInfoScatterMap.Results[0][11].TryGetResult<double>(out var absoluteX) &&
                    cameraInfoScatterMap.Results[0][12].TryGetResult<double>(out var absoluteY) &&
                    cameraInfoScatterMap.Results[0][13].TryGetResult<double>(out var absoluteZ))
                {
                    _absoluteLocation = new Vector3D(absoluteX, absoluteY, absoluteZ);
                }
                else
                {
                    return false;
                }

                if (cameraInfoScatterMap.Results[0][14].TryGetResult<double>(out var x) &&
                    cameraInfoScatterMap.Results[0][15].TryGetResult<double>(out var y) &&
                    cameraInfoScatterMap.Results[0][16].TryGetResult<double>(out var z))
                {
                    _localUPlayer.Position = new Vector3D(
                        x + _absoluteLocation.X,
                        y + _absoluteLocation.Y,
                        z + _absoluteLocation.Z
                    );
                }
                else
                {
                    return false;
                }

                if (cameraInfoScatterMap.Results[0][17].TryGetResult<double>(out var rotX) &&
                    cameraInfoScatterMap.Results[0][18].TryGetResult<double>(out var rotY) &&
                    cameraInfoScatterMap.Results[0][19].TryGetResult<double>(out var rotZ))
                {
                    var rotation = new Vector3D(rotX, rotY, rotZ);
                    _localUPlayer.Rotation = new Vector2D(rotation.Y, rotation.X);
                    _localUPlayer.Rotation3D = rotation;
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool ProcessPlayerInfo()
        {
            var scatterMap = new ScatterReadMap(1);
            ulong pawnPtr = ReadPawnPointer();
            if (pawnPtr == 0)
            {
                ResetPlayerStateToDefault();
                return true;
            }

            string pawnClassName = Memory.GetActorClassName(pawnPtr);
            bool isInVehicle = !pawnClassName.Contains("BP_Soldier");
            float cameraFOV = ReadCameraFOV();

            if (isInVehicle)
            {
                _currentFOV = cameraFOV;
                _isAimingDownSights = false;
                _hasPipScope = false;
                return true;
            }

            return UpdateOnFootPlayerInfo(scatterMap, pawnPtr, cameraFOV);
        }

        private float ReadCameraFOV()
        {
            ulong cameraManagerPtr = Memory.ReadPtr(_playerController + Offsets.PlayerController.PlayerCameraManager);
            return Memory.ReadValue<float>(cameraManagerPtr + Offsets.PlayerCameraManager.DefaultFOV);
        }

        private bool UpdateOnFootPlayerInfo(ScatterReadMap scatterMap, ulong pawnPtr, float cameraFOV)
        {
            ulong inventoryPtr = Memory.ReadPtr(pawnPtr + Offsets.ASQSoldier.InventoryComponent);
            if (inventoryPtr == 0)
            {
                _isAimingDownSights = false;
                _hasPipScope = false;
                _currentFOV = cameraFOV;
                return true;
            }

            var round1 = scatterMap.AddRound();
            var weaponPtrEntry = round1.AddEntry<ulong>(0, 0, inventoryPtr + Offsets.USQPawnInventoryComponent.CurrentWeapon);
            scatterMap.Execute();

            if (!scatterMap.Results[0][0].TryGetResult<ulong>(out ulong weaponPtr) || weaponPtr == 0)
            {
                _isAimingDownSights = false;
                _hasPipScope = false;
                _currentFOV = cameraFOV;
                return true;
            }

            return UpdateWeaponInfo(scatterMap, weaponPtr, cameraFOV);
        }

        private bool UpdateWeaponInfo(ScatterReadMap scatterMap, ulong weaponPtr, float cameraFOV)
        {
            _currentWeaponPtr = weaponPtr;

            var round2 = scatterMap.AddRound();
            round2.AddEntry<byte>(0, 1, weaponPtr + Offsets.ASQWeapon.bAimingDownSights);
            round2.AddEntry<ulong>(0, 2, weaponPtr + Offsets.ASQWeapon.CachedPipScope);
            round2.AddEntry<float>(0, 3, weaponPtr + Offsets.ASQWeapon.CurrentFOV);
            round2.AddEntry<byte>(0, 4, weaponPtr + Offsets.ASQWeapon.CurrentState);
            scatterMap.Execute();

            _isAimingDownSights = scatterMap.Results[0][1].TryGetResult<byte>(out byte ads) && ads == 1;
            _hasPipScope = scatterMap.Results[0][2].TryGetResult<ulong>(out ulong pipScopePtr) && pipScopePtr != 0;
            float weaponFOV = scatterMap.Results[0][3].TryGetResult<float>(out float currFOV) && currFOV > 5f && currFOV < 180f ? currFOV : cameraFOV;
            _isFiring = scatterMap.Results[0][4].TryGetResult<byte>(out byte firing) && firing == 1;

            float finalFOV = cameraFOV;
            if (_isAimingDownSights)
            {
                finalFOV = weaponFOV;
                if (_hasPipScope && pipScopePtr != 0)
                {
                    UpdateScopeMagnification(pipScopePtr, weaponFOV, ref finalFOV);
                }
            }

            _currentFOV = finalFOV;
            return true;
        }

        private void UpdateScopeMagnification(ulong pipScopePtr, float weaponFOV, ref float fov)
        {
            int magnificationIdx = Memory.ReadValue<int>(pipScopePtr + Offsets.USQPipScopeCaptureComponent.CurrentMagnificationLevel);
            _magnificationIndex = (magnificationIdx >= 0 && magnificationIdx < 3) ? magnificationIdx : 0;

            float magnification = _magnificationIndex switch
            {
                0 => Program.Config.FirstScopeMagnification,
                1 => Program.Config.SecondScopeMagnification,
                2 => Program.Config.ThirdScopeMagnification,
                _ => 1f
            };

            if (magnification > 1f)
            {
                fov = GetZoomedFOV(magnification, weaponFOV);
            }
        }

        private float GetZoomedFOV(float magnification, float defaultFOV)
        {
            float defaultFOVRad = defaultFOV * 0.00872664626f;
            float zoomedHalfFOVRad = (float)Math.Atan(Math.Tan(defaultFOVRad) / magnification);
            return 2.0f * zoomedHalfFOVRad * 57.295779513f;
        }

        private void ResetPlayerStateToDefault()
        {
            _isAimingDownSights = false;
            _hasPipScope = false;
            _isFiring = false;
            _currentFOV = 90f;
            _currentWeaponPtr = 0;
            _lastNoRecoilWeaponPtr = 0;
        }

        private ulong ReadPawnPointer()
        {
            return Memory.ReadPtr(_playerController + Offsets.PlayerController.AcknowledgedPawn);
        }
        
        /// <summary>
        /// Detects the current player state based on game data
        /// </summary>
        private void DetectPlayerState()
        {
            try
            {
                PlayerState newState = PlayerState.Unknown;
                
                // If not in game, player is in main menu
                if (!_inGame)
                {
                    newState = PlayerState.MainMenu;
                }
                else if (_playerController == 0)
                {
                    // Check if we have a valid player controller and player state
                    newState = PlayerState.Unknown;
                }
                else
                {
                    ulong playerState = Memory.ReadPtr(_playerController + Offsets.Controller.PlayerState);
                    if (playerState == 0)
                    {
                        newState = PlayerState.CommandMenu;
                    }
                    else
                    {
                        // Check if we have a soldier actor (spawned)
                        ulong soldierActor = Memory.ReadPtr(playerState + Offsets.ASQPlayerState.Soldier);
                        if (soldierActor == 0)
                        {
                            newState = PlayerState.CommandMenu;
                        }
                        else
                        {
                            // Check soldier health to determine if alive or dead
                            float health = Memory.ReadValue<float>(soldierActor + Offsets.ASQSoldier.Health);
                            newState = health > 0 ? PlayerState.Alive : PlayerState.Dead;
                        }
                    }
                }
                
                // Log state changes
                if (newState != _currentPlayerState)
                {
                    Logger.Debug($"Player state changed: {_currentPlayerState} -> {newState}");
                    _previousPlayerState = _currentPlayerState;
                    _currentPlayerState = newState;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error detecting player state: {ex.Message}");
                _currentPlayerState = PlayerState.Unknown;
            }
        }
        #endregion
    }

    #region Exceptions
    public class GameNotRunningException : Exception
    {
        public GameNotRunningException() { }
        public GameNotRunningException(string message) : base(message) { }
        public GameNotRunningException(string message, Exception inner) : base(message, inner) { }
    }

    public class GameEnded : Exception
    {
        public GameEnded() { }
        public GameEnded(string message) : base(message) { }
        public GameEnded(string message, Exception inner) : base(message, inner) { }
    }
    #endregion
}
