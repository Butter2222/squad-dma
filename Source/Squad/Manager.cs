using Offsets;
using squad_dma.Source.Misc;
using squad_dma.Source.Squad.Features;
using System.Diagnostics.Eventing.Reader;

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
        private ForceFullAuto _FullAuto;
        private NoCameraShake _noCameraShake;
        private NoSpread _noSpread;  // Using state-aware version
        private NoRecoil _noRecoil;  // Using state-aware version
        private NoSway _noSway;  // Using state-aware version
        private InstantGrenade _instantGrenade;
        
        // Weapon manager
        private WeaponManager _weaponManager;

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
        
        public Manager(ulong playerController, bool inGame, RegistredActors actors, Game game = null)
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
                if ((DateTime.Now - _lastPointerUpdate).TotalMilliseconds < 500 && 
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
                
                _lastPointerUpdate = DateTime.Now;
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
            _FullAuto = new ForceFullAuto(_playerController, _inGame);
            _noCameraShake = new NoCameraShake(_playerController, _inGame);
            _noSpread = new NoSpread(_playerController, _inGame, _game);
            _noRecoil = new NoRecoil(_playerController, _inGame, _game);
            _noSway = new NoSway(_playerController, _inGame, _game);
            _instantGrenade = new InstantGrenade(_playerController, _inGame);
            
            // Register weapon features
            _weaponManager.RegisterFeature(_infiniteAmmo);
            _weaponManager.RegisterFeature(_FullAuto);
            _weaponManager.RegisterFeature(_noRecoil);
            _weaponManager.RegisterFeature(_noSpread);
            _weaponManager.RegisterFeature(_noSway);
            _weaponManager.RegisterFeature(_quickSwap);
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
                if (!_inGame || _playerController == 0) return false;
                
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
                        if (!_inGame || _playerController == 0 || !IsLocalPlayerValid()) 
                        {
                            await Task.Delay(1000, _cancellationTokenSource.Token);
                            continue;
                        }

                        UpdateCachedPointers();
                        _weaponManager.Update();

                        // Get current player state from game instance
                        PlayerState currentState = _game?.GetPlayerState() ?? PlayerState.Unknown;

                        // Apply features based on player state
                        ApplyFeaturesBasedOnState(currentState);

                        await Task.Delay(1000, _cancellationTokenSource.Token);
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
                    // No additional features for main menu
                    break;
                    
                case PlayerState.CommandMenu:
                    // Limited features for command menu state
                    break;
                    
                case PlayerState.Alive:
                    // Apply legacy features that need manual state management
                    ApplyAliveStateFeatures();
                    break;
                    
                case PlayerState.Dead:
                    // Apply limited legacy features when dead
                    ApplyDeadStateFeatures();
                    break;
                    
                case PlayerState.Unknown:
                default:
                    // Apply legacy features for unknown states
                    ApplyAliveStateFeatures();
                    break;
            }
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
        
        /// <summary>
        /// Apply all features (fallback behavior)
        /// </summary>
        private void ApplyAllFeatures()
        {
            if (_config.DisableSuppression && _suppression.IsEnabled)
                _suppression.Apply();

            if (_config.SetInteractionDistances && _interactionDistances.IsEnabled)
                _interactionDistances.Apply();

            if (_config.AllowShootingInMainBase && _shootingInMainBase.IsEnabled)
                _shootingInMainBase.Apply();

            if (_config.SetSpeedHack && _speedHack.IsEnabled)
                _speedHack.Apply();

            if (_config.SetAirStuck && _airStuck.IsEnabled)
                _airStuck.Apply();
                
            if (_config.DisableCollision && _collision.IsEnabled)
                _collision.Apply();
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

        public void SetInstantGrenade(bool enable)
        {
            _instantGrenade.SetEnabled(enable);
        }

        public void Dispose()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
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
        
        /// <summary>
        /// Gets the current vehicle inventory if player is in a vehicle
        /// </summary>
        public ulong GetVehicleInventory()
        {
            return _cachedVehicleInventory;
        }
        
        /// <summary>
        /// Gets the current seat pawn if player is in a vehicle
        /// </summary>
        public ulong GetSeatPawn()
        {
            return _cachedSeatPawn;
        }
        
        /// <summary>
        /// Gets the current seat if player is in a vehicle
        /// </summary>
        public ulong GetCurrentSeat()
        {
            return _cachedCurrentSeat;
        }
        
        #endregion
    }
}