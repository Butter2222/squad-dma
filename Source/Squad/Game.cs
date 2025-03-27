using System.Collections.ObjectModel;
using System.Numerics;

namespace squad_dma
{
    /// <summary>
    /// Class containing Game instance.
    /// </summary>
    public class Game
    {
        private readonly ulong _squadBase;
        private volatile bool _inGame = false;
        private RegistredActors _actors;
        private UActor _localUPlayer;
        private ulong _gameWorld;
        private ulong _gameInstance;
        private ulong _localPlayer;
        private ulong _playerController;
        private Vector3 _absoluteLocation;
        private string _currentLevel = string.Empty;
        private bool _vehiclesLogged = false;

        // FOV
        private ulong _localPlayersPtr;
        private bool _isAimingDownSights;
        private bool _hasPipScope;
        private float _currentFOV;
        private int _magnificationIndex;
        private int _updateCounter = 0; // trying to optimize
        public enum GameStatus
        {
            NotFound,
            Menu,
            InGame,
        }

        #region Getters
        public bool InGame => _inGame;
        public string MapName => _currentLevel;
        public UActor LocalPlayer => _localUPlayer;
        public ReadOnlyDictionary<ulong, UActor> Actors
        {
            get
            {
                lock (actorsLock)
                {
                    return _actors?.Actors ?? new ReadOnlyDictionary<ulong, UActor>(new Dictionary<ulong, UActor>());
                }
            }
        }
        public Vector3 AbsoluteLocation => _absoluteLocation;
        public bool IsAimingDownSights => _isAimingDownSights;
        public bool HasPipScope => _hasPipScope;
        public float CurrentFOV => _currentFOV;

        #endregion

        /// <summary>
        /// Game Constructor.
        /// </summary>
        public Game(ulong squadBase)
        {
            _squadBase = squadBase;
        }

        public readonly object actorsLock = new object();

        #region GameLoop
        /// <summary>
        /// Main Game Loop executed by Memory Worker Thread.
        /// </summary>
        public void GameLoop()
        {
            try
            {
                if (!Memory.GetModuleBase())
                {
                    lock (actorsLock)
                    {
                        this._inGame = false;
                        //Program.Log("Game process not found, _inGame set to false.");
                    }
                    throw new GameEnded("Game process not found!");
                }

                lock (actorsLock)
                {
                    if (!this._inGame)
                    {
                        Program.Log("Checking game state...");
                        bool hasWorld = GetGameWorld();
                        Program.Log($"GetGameWorld: {hasWorld}");
                        if (!hasWorld) throw new Exception("GetGameWorld failed");
                        bool hasInstance = GetGameInstance();
                        Program.Log($"GetGameInstance: {hasInstance}");
                        if (!hasInstance) throw new Exception("GetGameInstance failed");
                        bool hasLevel = GetCurrentLevel();
                        Program.Log($"GetCurrentLevel: {hasLevel}");
                        if (!hasLevel) throw new Exception("GetCurrentLevel failed");
                        bool hasActors = InitActors();
                        Program.Log($"InitActors: {hasActors}");
                        if (!hasActors) throw new Exception("InitActors failed");
                        bool hasLocalPlayer = GetLocalPlayer();
                        Program.Log($"GetLocalPlayer: {hasLocalPlayer}");
                        if (!hasLocalPlayer) throw new Exception("GetLocalPlayer failed");

                        if (hasWorld && hasInstance && hasLevel && hasActors && hasLocalPlayer)
                        {
                            this._inGame = true;
                            Memory.GameStatus = Game.GameStatus.InGame;
                            //Program.Log("Game detected, _inGame set to true!");
                        }
                        else
                        {
                            this._vehiclesLogged = false;
                            throw new GameEnded("Game has not yet started!");
                        }
                    }

                    UpdateLocalPlayerInfo();
                    this._actors.UpdateList();
                    this._actors.UpdateAllPlayers();
                }
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
                Program.Log($"GameLoop failed: {ex.Message}");
                HandleUnexpectedException(ex);
            }
        }
        #endregion

        #region Methods

        /// <summary>
        /// Handles the scenario when DMA shutdown occurs.
        /// </summary>
        private void HandleDMAShutdown()
        {
            Program.Log("DMA shutdown");
            this._inGame = false;
        }

        /// <summary>
        /// Handles the scenario when the game ends.
        /// </summary>
        /// <param name="e">The GameEnded exception instance containing details about the game end.</param>
        private void HandleGameEnded(GameEnded e)
        {
            Program.Log("Game has ended!");
            lock (actorsLock)
            {
                this._inGame = false;
                if (_actors != null)
                {
                    _actors._actors.Clear();
                }
            }
            Memory.GameStatus = Game.GameStatus.Menu;
            Memory.Restart();
        }

        /// <summary>
        /// Handles unexpected exceptions that occur during the game loop.
        /// </summary>
        /// <param name="ex">The exception instance that was thrown.</param>
        private void HandleUnexpectedException(Exception ex)
        {
            Program.Log($"CRITICAL ERROR - Game ended due to unhandled exception: {ex}");
            this._inGame = false;
        }

        /// <summary>
        /// Waits until game has started before returning to caller.
        /// </summary>
        /// 
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
                        Program.Log("Game has started!!");
                        this._inGame = true;
                        Memory.GameStatus = Game.GameStatus.InGame;
                        return;
                    }
                }
                catch (GameNotRunningException)
                {
                    throw; // Propagate up to break out of wait loop
                }
                catch (Exception ex) when (IsExpectedException(ex))
                {
                    Program.Log($"Ignoring expected exception during wait: {ex.Message}");
                }

                Thread.Sleep(500);
            }
        }

        private static bool IsExpectedException(Exception ex)
        {
            return ex is NullReferenceException
                || ex is AccessViolationException
                || ex is DMAException;
        }


        /// <summary>
        /// Gets Game Object Manager ptr
        /// </summary>
        private bool GetGameWorld()
        {
            try
            {
                _gameWorld = Memory.ReadPtr(_squadBase + Offsets.GameObjects.GWorld);
                // Program.Log($"Found Game World at 0x{_gameWorld:X},\n0x{_gameWorld + 0x00F8:X}=0x16,\n0x{_gameWorld + 0x0158:X}=0x28,\n0x{_gameWorld + 0x01A8:X}=0x50,\n0x{_gameWorld + 0x0270:X}=0x370,\n0x{_gameWorld + 0x05E8:X}=0x90,\n0x{_gameWorld + 0x06D0:X}=0xC8");
                return true;
            }
            catch { return false; }
        }
        /// <summary>
        /// Gets GameInstance
        /// </summary>
        private bool GetGameInstance()
        {
            try
            {
                _gameInstance = Memory.ReadPtr(_gameWorld + Offsets.World.OwningGameInstance);
                // Program.Log($"Found Game Instance at 0x{_gameInstance:X}");
                return true;
            }
            catch { return false; }
        }
        /// <summary>
        /// Gets GameInstance
        /// </summary>
        private bool GetCurrentLevel()
        {
            try
            {
                var currentLayer = Memory.ReadPtr(_gameInstance + Offsets.GameInstance.CurrentLayer);
                var currentLevelIdPtr = currentLayer + Offsets.SQLayer.LevelID;
                var currentLevelId = Memory.ReadValue<uint>(currentLevelIdPtr);
                _currentLevel = Memory.GetNamesById([currentLevelId])[currentLevelId];
                Program.Log("Current level is " + _currentLevel);
                return true;
            }
            catch { return false; }
        }
        /// <summary>
        /// Initializes actors list
        /// </summary>
        private bool InitActors()
        {
            try
            {
                var persistentLevel = Memory.ReadPtr(_gameWorld + Offsets.World.PersistentLevel);
                // Program.Log($"Found PersistentLevel at 0x{persistentLevel:X}");
                lock (actorsLock)
                {
                    _actors = new RegistredActors(persistentLevel);
                }
                return true;
            }
            catch { return false; }
        }
        /// <summary>
        /// Gets LocalPlayer
        /// </summary>
        private bool GetLocalPlayer()
        {
            try
            {
                _localPlayersPtr = Memory.ReadPtr(_gameInstance + Offsets.GameInstance.LocalPlayers);
                _localPlayer = Memory.ReadPtr(_localPlayersPtr);
                // Program.Log($"Found LocalPlayer at 0x{_localPlayer:X}");
                _localUPlayer = new UActor(_localPlayer);
                _localUPlayer.Team = Team.Unknown;
                GetPlayerController();
                return true;
            }
            catch { return false; }

        }
        private bool UpdateLocalPlayerInfo()
        {
            try
            {
                GetCameraCache();

                _updateCounter++;
                if (_updateCounter % 10 != 0)
                {
                    return true;
                }
                _updateCounter = 0;

                var scatterMap = new ScatterReadMap(1);
                var round1 = scatterMap.AddRound();
                var round2 = scatterMap.AddRound();
                var round3 = scatterMap.AddRound();

                var pawnPtr = Memory.ReadPtr(_playerController + Offsets.PlayerController.AcknowledgedPawn);
                if (pawnPtr == 0)
                {
                    _isAimingDownSights = false;
                    _hasPipScope = false;
                    _currentFOV = 90f;
                    return true;
                }

                string pawnClassName = Memory.GetActorClassName(pawnPtr);
                //Program.Log($"PawnClassName : {pawnClassName}");

                bool isInVehicle = !pawnClassName.Contains("BP_Soldier");

                var cameraManagerPtr = Memory.ReadPtr(_playerController + Offsets.PlayerController.PlayerCameraManager);
                float cameraFOV = Memory.ReadValue<float>(cameraManagerPtr + Offsets.Camera.CameraFov);

                // Todo
                if (isInVehicle)
                {
                    _currentFOV = cameraFOV; 
                    _isAimingDownSights = false;
                    _hasPipScope = false;
                    return true;
                }

                // On Foot
                ulong inventoryPtr = Memory.ReadPtr(pawnPtr + Offsets.ASQSoldier.InventoryComponent);
                if (inventoryPtr == 0)
                {
                    _isAimingDownSights = false;
                    _hasPipScope = false;
                    _currentFOV = cameraFOV;
                    return true;
                }

                var weaponPtrEntry = round1.AddEntry<ulong>(0, 0, inventoryPtr + Offsets.USQPawnInventoryComponent.CurrentWeapon);
                scatterMap.Execute();

                if (!scatterMap.Results[0][0].TryGetResult<ulong>(out ulong weaponPtr) || weaponPtr == 0)
                {
                    _isAimingDownSights = false;
                    _hasPipScope = false;
                    _currentFOV = cameraFOV;
                    return true;
                }

                round2.AddEntry<byte>(0, 1, weaponPtr + Offsets.ASQWeapon.bAimingDownSights);
                round2.AddEntry<ulong>(0, 2, weaponPtr + Offsets.ASQWeapon.CachedPipScope);
                round2.AddEntry<float>(0, 3, weaponPtr + Offsets.ASQWeapon.CurrentFOV);
                scatterMap.Execute();

                _isAimingDownSights = scatterMap.Results[0][1].TryGetResult<byte>(out byte ads) && ads == 1;
                _hasPipScope = scatterMap.Results[0][2].TryGetResult<ulong>(out ulong pipScopePtr) && pipScopePtr != 0;
                float weaponFOV = scatterMap.Results[0][3].TryGetResult<float>(out float currFOV) && currFOV > 10f && currFOV < 180f ? currFOV : cameraFOV;

                // FOV Handle
                _currentFOV = _isAimingDownSights ? weaponFOV : cameraFOV; 

                if (_isAimingDownSights && _hasPipScope && pipScopePtr != 0)
                {
                    round3.AddEntry<int>(0, 6, pipScopePtr + Offsets.USQPipScopeCaptureComponent.CurrentMagnificationLevel);
                    scatterMap.Execute();

                    _magnificationIndex = scatterMap.Results[0][6].TryGetResult<int>(out int idx) && idx >= 0 && idx < 3 ? idx : 0;

                    float magnification = _magnificationIndex switch
                    {
                        0 => 3f,  // x3
                        1 => 6f,  // x6
                        2 => 9f,  // x9
                        _ => 1f  
                    };

                    if (magnification > 1f)
                    {
                        _currentFOV = weaponFOV / magnification; 
                    }
                }

                // Program.Log($"ADS: {_isAimingDownSights}, HasPipScope: {_hasPipScope}, CurrentFOV: {_currentFOV}");
                return true;
            }
            catch (Exception ex)
            {
                Program.Log($"Error in UpdateLocalPlayerInfo : {ex.Message}");
                _isAimingDownSights = false;
                _hasPipScope = false;
                _currentFOV = 90f;
                return false;
            }
        }

        /// <summary>
        /// Gets PlayerController
        /// </summary>
        private bool GetPlayerController()
        {
            try
            {
                _playerController = Memory.ReadPtr(_localPlayer + Offsets.UPlayer.PlayerController);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Gets CameraCache
        /// </summary>
        private bool GetCameraCache()
        {
            try
            {
                var cameraInfoScatterMap = new ScatterReadMap(1);
                var cameraManagerRound = cameraInfoScatterMap.AddRound();
                var cameraInfoRound = cameraInfoScatterMap.AddRound();

                var cameraManagerPtr = cameraManagerRound.AddEntry<ulong>(0, 0, _playerController + Offsets.PlayerController.PlayerCameraManager);
                cameraManagerRound.AddEntry<int>(0, 11, _gameWorld + Offsets.World.WorldOrigin);
                cameraManagerRound.AddEntry<int>(0, 12, _gameWorld + Offsets.World.WorldOrigin + 0x4);
                cameraManagerRound.AddEntry<int>(0, 13, _gameWorld + Offsets.World.WorldOrigin + 0x8);
                cameraInfoRound.AddEntry<Vector3>(0, 1, cameraManagerPtr, null, Offsets.Camera.CameraLocation);
                cameraInfoRound.AddEntry<Vector3>(0, 2, cameraManagerPtr, null, Offsets.Camera.CameraRotation);

                cameraInfoScatterMap.Execute();

                if (!cameraInfoScatterMap.Results[0][1].TryGetResult<Vector3>(out var location))
                {
                    return false;
                }
                if (!cameraInfoScatterMap.Results[0][2].TryGetResult<Vector3>(out var rotation))
                {
                    return false;
                }
                if (cameraInfoScatterMap.Results[0][11].TryGetResult<int>(out var absoluteX)
                && cameraInfoScatterMap.Results[0][12].TryGetResult<int>(out var absoluteY)
                && cameraInfoScatterMap.Results[0][13].TryGetResult<int>(out var absoluteZ))
                {
                    _absoluteLocation = new Vector3(absoluteX, absoluteY, absoluteZ);
                    // Program.Log(_absoluteLocation.ToString());
                }
                _localUPlayer.Position = location;
                _localUPlayer.Rotation = new Vector2(rotation.Y, rotation.X);
                _localUPlayer.Rotation3D = rotation;
                return true;
            }
            catch { return false; }
        }

        public void LogVehicles(bool force = false)
        {
            if (!force && _vehiclesLogged)
                return;

            var actorBaseWithName = this._actors.GetActorBaseWithName();
            if (actorBaseWithName.Count > 0)
            {
                var names = Memory.GetNamesById([.. actorBaseWithName.Values.Distinct()]);

                foreach (var nameEntry in names)
                {
                    if (!nameEntry.Value.StartsWith("BP_Soldier"))
                    {
                        Program.Log($"{nameEntry.Key} {nameEntry.Value}");
                    }
                }
                _vehiclesLogged = !force; // Reset only if not forced
            }
            else
            {
                Program.Log("No entries found.");
            }
        }

    }
    #endregion


    #region Exceptions
    public class GameNotRunningException : Exception
    {
        public GameNotRunningException()
        {
        }

        public GameNotRunningException(string message)
            : base(message)
        {
        }

        public GameNotRunningException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class GameEnded : Exception
    {
        public GameEnded()
        {

        }

        public GameEnded(string message)
            : base(message)
        {
        }

        public GameEnded(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
    #endregion
}