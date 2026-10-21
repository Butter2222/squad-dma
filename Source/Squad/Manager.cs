using Offsets;
using squad_dma.Source.Misc;
using squad_dma.Source.Squad.Features;

namespace squad_dma.Source.Squad
{
    public class Manager : IDisposable
    {
        public readonly ulong _playerController;
        public readonly bool _inGame;
        private readonly Config _config;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly Game _game;
        
        protected ulong _cachedPlayerState = 0;
        protected ulong _cachedSoldierActor = 0;
        protected ulong _cachedInventoryComponent = 0;
        protected ulong _cachedCurrentWeapon = 0;
        protected ulong _cachedCharacterMovement = 0;
        protected ulong _cachedWeaponStaticInfo = 0;
        protected ulong _cachedCurrentSeat = 0;
        protected ulong _cachedSeatPawn = 0;
        protected ulong _cachedVehicleInventory = 0;
        protected ulong _cachedVehicleWeapon = 0;
        protected ulong _cachedCameraManager = 0;
        protected DateTime _lastPointerUpdate = DateTime.MinValue;

        // Per-tick write batch — all features enqueue here; flushed once per loop iteration
        [ThreadStatic]
        private static List<IScatterWriteEntry> _pendingWrites;

        protected void EnqueueWrite<T>(ulong addr, T value) where T : unmanaged
        {
            _pendingWrites ??= new List<IScatterWriteEntry>(128);
            _pendingWrites.Add(new ScatterWriteDataEntry<T>(addr, value));
        }

        private void FlushPendingWrites()
        {
            if (_pendingWrites == null || _pendingWrites.Count == 0) return;
            try { Memory.WriteScatter(_pendingWrites); }
            catch (Exception ex) { Logger.Error($"[Manager] Scatter flush error: {ex.Message}"); }
            finally { _pendingWrites.Clear(); }
        }
        
        // Modules
        private Suppression _suppression;
        private InteractionDistances _interactionDistances;
        private ShootingInMainBase _shootingInMainBase;
        private SpeedHack _speedHack;  // Using state-aware version
        private Collision _collision;  // Using state-aware version
        private AirStuck _airStuck;  // Using state-aware version
        private QuickZoom _quickZoom;
        private RapidFire _rapidFire;
        private InfiniteAmmo _infiniteAmmo;
        private QuickSwap _quickSwap;
        private QuickAds _quickAds;
        private FireSupport _fireSupport;
        private ForceFullAuto _FullAuto;
        private NoCameraShake _noCameraShake;
        private NoSpread _noSpread;  // Using state-aware version
        private NoRecoil _noRecoil;  // Using state-aware version
        private NoSway _noSway;  // Using state-aware version
        private InstantGrenade _instantGrenade;
        private ThirdPerson _thirdPerson;
        private FreeCamera _freeCamera;
        private RenderTweaks _renderTweaks;

        // Weapon manager
        private WeaponManager _weaponManager;
        
        // Weapon detector (MortarCalculator instance)
        public MortarCalculator WeaponDetector { get; private set; }

        /// <summary>
        /// Constructor for feature classes that inherit from Manager
        /// </summary>
        /// <param name="playerController">The player controller address</param>
        /// <param name="inGame">Whether the game is currently active</param>
        public Manager(ulong playerController, bool inGame)
        {
            _playerController = playerController;
            _inGame = inGame;
            _cancellationTokenSource = null;
            
            _config = Program.Config; 
            
            UpdateCachedPointers();
        }
        
        public Manager(ulong playerController, bool inGame, Game game = null)
        {
            _playerController = playerController;
            _inGame = inGame;
            _cancellationTokenSource = new CancellationTokenSource();
            _config = Program.Config;
            _game = game;
            
            // Initialize cached pointers
            UpdateCachedPointers();
            
            // Initialize weapon manager
            _weaponManager = new WeaponManager(_playerController, _inGame, this);
            
            // Initialize weapon detector (MortarCalculator)
            WeaponDetector = new MortarCalculator(this, _game);
            
            // Initialize all feature modules
            InitializeFeatures();
            
            // Start a timer to apply features
            StartFeatureTimer();
        }
        
        /// <summary>
        /// Updates cached pointers to avoid redundant memory reads
        /// </summary>
        protected void UpdateCachedPointers()
        {
            try
            {
                if ((DateTime.UtcNow - _lastPointerUpdate).TotalMilliseconds < 500 && 
                    _cachedPlayerState != 0 && _cachedSoldierActor != 0)
                    return;
                    
                if (!_inGame || _playerController == 0) return;
                
                _cachedPlayerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                if (_cachedPlayerState == 0) return;
                
                _cachedSoldierActor = Memory.ReadPtr(_cachedPlayerState + ASQPlayerState.Soldier);
                if (_cachedSoldierActor == 0) return;
                
                _cachedInventoryComponent = Memory.ReadPtr(_cachedSoldierActor + ASQSoldier.InventoryComponent);
                if (_cachedInventoryComponent != 0)
                {
                    _cachedCurrentWeapon = Memory.ReadPtr(_cachedInventoryComponent + USQPawnInventoryComponent.CurrentWeapon);
                    if (_cachedCurrentWeapon != 0)
                    {
                        _cachedWeaponStaticInfo = Memory.ReadPtr(_cachedCurrentWeapon + ASQEquipableItem.ItemStaticInfo);
                    }
                }
                
                _cachedCharacterMovement = Memory.ReadPtr(_cachedSoldierActor + Character.CharacterMovement);
                
                // Update vehicle-related pointers
                _cachedCurrentSeat = Memory.ReadPtr(_cachedPlayerState + ASQPlayerState.CurrentSeat);
                if (_cachedCurrentSeat != 0)
                {
                    _cachedSeatPawn = Memory.ReadPtr(_cachedCurrentSeat + USQVehicleSeatComponent.SeatPawn);
                    if (_cachedSeatPawn != 0)
                    {
                        _cachedVehicleInventory = Memory.ReadPtr(_cachedSeatPawn + ASQVehicleSeat.VehicleInventory);
                        if (_cachedVehicleInventory != 0)
                        {
                            _cachedVehicleWeapon = Memory.ReadPtr(_cachedVehicleInventory + USQPawnInventoryComponent.CurrentWeapon);
                        }
                    }
                }
                
                _cachedCameraManager = Memory.ReadPtr(_playerController + PlayerController.PlayerCameraManager);
                
                _lastPointerUpdate = DateTime.UtcNow;
            }
            catch
            {
                // Reset pointers on error
                _cachedPlayerState = 0;
                _cachedSoldierActor = 0;
                _cachedInventoryComponent = 0;
                _cachedCurrentWeapon = 0;
                _cachedCharacterMovement = 0;
                _cachedWeaponStaticInfo = 0;
                _cachedCurrentSeat = 0;
                _cachedSeatPawn = 0;
                _cachedVehicleInventory = 0;
                _cachedVehicleWeapon = 0;
                _cachedCameraManager = 0;
            }
        }
        
        protected ulong GetCachedWeaponStaticInfo(ulong weapon)
        {
            if (weapon == _cachedCurrentWeapon)
            {
                return _cachedWeaponStaticInfo;
            }
            return Memory.ReadPtr(weapon + ASQEquipableItem.ItemStaticInfo);
        }
        
        /// <summary>
        /// Static info of the current gun, or 0 when the item is a grenade. Gun features write offsets that lie far
        /// past the end of a grenade's static info object, so they must never be handed one.
        /// </summary>
        protected ulong GetGunStaticInfo(ulong weapon)
        {
            if (weapon == 0 || IsGrenade(weapon)) return 0;
            return GetCachedWeaponStaticInfo(weapon);
        }

        // UClass* -> derives from SQGrenade. A class never changes, so every class is resolved once.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, bool> GrenadeClassCache = new();

        /// <summary>
        /// Tells a grenade from a gun by its class chain. Reading fields at fixed offsets is not an option: a
        /// grenade object ends before the gun fields (WeaponConfig at 0x6D0) so those reads return heap garbage,
        /// which used to make grenades pass as guns and get gun values written past the end of the object.
        /// A failed read is not cached and counts as "not a grenade".
        /// </summary>
        protected static bool IsGrenade(ulong equipableItem)
        {
            if (equipableItem == 0) return false;
            try
            {
                ulong cls = Memory.ReadPtr(equipableItem + UObject.Class);
                if (cls == 0) return false;
                if (GrenadeClassCache.TryGetValue(cls, out bool cached)) return cached;

                bool isGrenade = false;
                ulong c = cls;
                for (int depth = 0; depth < 16 && c != 0; depth++)
                {
                    uint nameId = Memory.ReadValue<uint>(c + UObject.Name);
                    if (nameId != 0 && Memory.GetNamesById([nameId]).TryGetValue(nameId, out var name) && name == "SQGrenade")
                    {
                        isGrenade = true;
                        break;
                    }
                    c = Memory.ReadPtr(c + UStruct.SuperStruct);
                }

                GrenadeClassCache[cls] = isGrenade;
                return isGrenade;
            }
            catch
            {
                return false;
            }
        }

        private void InitializeFeatures()
        {
            _suppression = new Suppression(_playerController, _inGame, _game);
            _interactionDistances = new InteractionDistances(_playerController, _inGame, _game);
            _shootingInMainBase = new ShootingInMainBase(_playerController, _inGame);
            _speedHack = new SpeedHack(_playerController, _inGame, _game);
            _airStuck = new AirStuck(_playerController, _inGame, _game, null);
            _collision = new Collision(_playerController, _inGame, _game, _airStuck);
            _quickZoom = new QuickZoom(_playerController, _inGame);
            _rapidFire = new RapidFire(_playerController, _inGame);
            _infiniteAmmo = new InfiniteAmmo(_playerController, _inGame, _game);
            _quickSwap = new QuickSwap(_playerController, _inGame);
            _quickAds = new QuickAds(_playerController, _inGame);
            _fireSupport = new FireSupport(_playerController, _inGame, _game);
            _FullAuto = new ForceFullAuto(_playerController, _inGame);
            _noCameraShake = new NoCameraShake(_playerController, _inGame, _game);
            _noSpread = new NoSpread(_playerController, _inGame, _game);
            _noRecoil = new NoRecoil(_playerController, _inGame, _game);
            _noSway = new NoSway(_playerController, _inGame, _game);
            _instantGrenade = new InstantGrenade(_playerController, _inGame);
            _thirdPerson = new ThirdPerson(_playerController, _inGame);
            _freeCamera = new FreeCamera(_playerController, _inGame, _game);
            _renderTweaks = new RenderTweaks(_playerController, _inGame, _game);

            // Register weapon features
            _weaponManager.RegisterFeature(_infiniteAmmo);
            _weaponManager.RegisterFeature(_FullAuto);
            _weaponManager.RegisterFeature(_noRecoil);
            _weaponManager.RegisterFeature(_noSpread);
            _weaponManager.RegisterFeature(_noSway);
            _weaponManager.RegisterFeature(_quickSwap);
            _weaponManager.RegisterFeature(_quickAds);
            _weaponManager.RegisterFeature(_fireSupport);
            _weaponManager.RegisterFeature(_rapidFire);
            _weaponManager.RegisterFeature(_shootingInMainBase);
            _weaponManager.RegisterFeature(_instantGrenade);
        }
        
        /// <summary>
        /// Checks if the local player is valid (has a valid player state and soldier actor)
        /// </summary>
        /// <returns>True if local player is valid, false otherwise</returns>
        public bool IsLocalPlayerValid()
        {
            try
            {
                if (!_inGame || (_game != null && !_game.InGame) || _playerController == 0) return false;
                
                ulong playerState = _cachedPlayerState != 0 ? _cachedPlayerState : Memory.ReadPtr(_playerController + Controller.PlayerState);
                if (playerState == 0) return false;
                
                ulong soldierActor = _cachedSoldierActor != 0 ? _cachedSoldierActor : Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return false;
                
                return true;
            }
            catch
            { return false; }
        }
        
        private void StartFeatureTimer()
        {
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (!_inGame || (_game != null && !_game.InGame) || _playerController == 0 || !IsLocalPlayerValid())
                        {
                            await Task.Delay(1000, _cancellationTokenSource.Token);
                            continue;
                        }

                        UpdateCachedPointers();
                        _weaponManager.Update();
                        
                        // Update weapon detector
                        WeaponDetector?.Update();

                        // Get current player state from game instance
                        PlayerState currentState = _game?.GetPlayerState() ?? PlayerState.Unknown;

                        // Apply features based on player state
                        ApplyFeaturesBasedOnState(currentState);

                        await Task.Delay(8, _cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Feature Timer Failed: {ex.Message}");
                        Logger.Error($"Stack Trace: {ex.StackTrace}");
                        Logger.Error($"Player Controller: {_playerController}");
                        Logger.Error($"In Game: {_inGame}");
                        Logger.Error($"Cached Player State: {_cachedPlayerState}");
                        Logger.Error($"Cached Soldier Actor: {_cachedSoldierActor}");
                        
                        await Task.Delay(1000, _cancellationTokenSource.Token);
                    }
                }
            }, _cancellationTokenSource.Token).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Logger.Error($"Feature Timer Task Faulted: {t.Exception?.Message}");
                }
            });
        }
        
        /// <summary>
        /// Apply method to be implemented by features
        /// </summary>
        public virtual void Apply() { }
        
        /// <summary>
        /// Apply features based on current player state
        /// </summary>
        /// <param name="playerState">Current player state</param>
        private void ApplyFeaturesBasedOnState(PlayerState playerState)
        {
            // Always call StateAwareFeature instances so they can handle their own state transitions
            ApplyStateAwareFeatures();

            switch (playerState)
            {
                case PlayerState.MainMenu:
                    break;

                case PlayerState.CommandMenu:
                    break;

                case PlayerState.Alive:
                    ApplyAliveStateFeatures();
                    break;

                case PlayerState.Dead:
                    ApplyDeadStateFeatures();
                    break;

                case PlayerState.Unknown:
                default:
                    ApplyAliveStateFeatures();
                    break;
            }

            // Flush all enqueued writes from this tick in a single DMA scatter call
            FlushPendingWrites();
        }
        
        /// <summary>
        /// Apply StateAwareFeature instances - called regardless of player state
        /// </summary>
        private void ApplyStateAwareFeatures()
        {
            // StateAwareFeature instances handle their own state management and config checking
            if (_config.DisableSuppression)
                _suppression.Apply();
                
            if (_config.NoRecoil)
                _noRecoil.Apply();
                
            if (_config.NoSpread)
                _noSpread.Apply();
                
            if (_config.NoSway)
                _noSway.Apply();
                
            if (_config.SetSpeedHack)
                _speedHack.Apply();
                
            if (_config.SetAirStuck)
                _airStuck.Apply();
                
            if (_config.DisableCollision)
                _collision.Apply();
                
            if (_config.SetFreeCamera)
                _freeCamera.Apply(); // watchdog only, the camera runs on its own thread

            if (_config.NoCameraShake)
                _noCameraShake.Apply();                
            // Apply QuickZoom continuously for smooth transitions
            _quickZoom.Apply();
        }
        
        /// <summary>
        /// Apply legacy features when player is alive
        /// </summary>
        private void ApplyAliveStateFeatures()
        {
            // Legacy features that need manual state management
            if (_config.SetInteractionDistances && _interactionDistances.IsEnabled)
                _interactionDistances.Apply();

            if (_config.AllowShootingInMainBase && _shootingInMainBase.IsEnabled)
                _shootingInMainBase.Apply();

            if (_instantGrenade.IsEnabled)
                _instantGrenade.Apply();

            if (_quickAds.IsEnabled)
                _quickAds.Apply();

            if (_fireSupport.IsEnabled)
                _fireSupport.Apply();
        }
        
        /// <summary>
        /// Apply legacy features when player is dead
        /// </summary>
        private void ApplyDeadStateFeatures()
        {
            // Only apply non-combat legacy features when dead
            if (_config.SetInteractionDistances && _interactionDistances.IsEnabled)
                _interactionDistances.Apply();
        }
        
        #region Feature Control Methods
        
        public void SetSuppression(bool enable)
        {
            _suppression.SetEnabled(enable);
        }
        
        public void SetInteractionDistances(bool enable)
        {
            _interactionDistances.SetEnabled(enable);
        }
        
        public void SetShootingInMainBase(bool enable)
        {
            _shootingInMainBase.SetEnabled(enable);
        }
        
        public void SetSpeedHack(bool enable)
        {
            _speedHack.SetEnabled(enable);
        }
        
        public void SetAirStuck(bool enable)
        {
            _airStuck.SetEnabled(enable);
        }
        
        public void ForceDisableAirStuck()
        {
            _airStuck.ForceDisable();
        }
        
        public void SetQuickZoom(bool enable)
        {
            _quickZoom.SetEnabled(enable);
        }
        
        public void DisableCollision(bool disable)
        {
            _collision.SetEnabled(disable);
        }
        
        public void SetRapidFire(bool enable)
        {
            _rapidFire.SetEnabled(enable);
        }

        public void SetFullAuto(bool enable)
        {
            _FullAuto.SetEnabled(enable);
        }
        
        public void SetInfiniteAmmo(bool enable)
        {
            _infiniteAmmo.SetEnabled(enable);
        }
        
        public void SetQuickSwap(bool enable)
        {
            _quickSwap.SetEnabled(enable);
        }
        
        public void SetFireSupport(bool enable)
        {
            _fireSupport.SetEnabled(enable);
        }

        public void SetQuickAds(bool enable)
        {
            _quickAds.SetEnabled(enable);
        }

        public void SetNoCameraShake(bool enable)
        {
            _noCameraShake.SetEnabled(enable);
        }
        
        public void SetNoSpread(bool enable)
        {
            _noSpread.SetEnabled(enable);
        }

        public void SetNoRecoil(bool enable)
        {
            _noRecoil.SetEnabled(enable);
        }

        public void SetNoSway(bool enable)
        {
            _noSway.SetEnabled(enable);
        }


        public void RefreshRenderTweaks() => _renderTweaks.Refresh();
        public void ShutdownRenderTweaks() => _renderTweaks?.Shutdown();

        public void SetFreeCamera(bool enable)
        {
            _freeCamera.SetEnabled(enable);
        }

        public void SetInstantGrenade(bool enable)
        {
            _instantGrenade.SetEnabled(enable);
        }

        public void Dispose()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel(); // not disposed, the feature loop still reads its token while it winds down
            }
            _fireSupport?.Shutdown();
            _freeCamera?.Shutdown();
            _renderTweaks?.Shutdown();
            _weaponManager?.Dispose();
        }
        #endregion

        #region Vehicle Helper Methods
        
        /// <summary>
        /// Checks if the player is in a vehicle
        /// </summary>
        public bool IsInVehicle()
        {
            return _cachedCurrentSeat != 0 && _cachedSeatPawn != 0;
        }
        
        /// <summary>
        /// Gets the current vehicle weapon if player is in a vehicle
        /// </summary>
        public ulong GetVehicleWeapon()
        {
            return _cachedVehicleWeapon;
        }
        #endregion
    }
}