using System;
using System.Collections.Generic;
using squad_dma.Source.Misc;
using Offsets;
using squad_dma.Source.Squad.Features;

namespace squad_dma.Source.Squad
{
    /// <summary>
    /// Manages weapon changes and coordinates feature updates
    /// </summary>
    public class WeaponManager : IDisposable
    {
        private readonly ulong _playerController;
        private readonly bool _inGame;
        private readonly List<Weapon> _weaponFeatures;
        private readonly Manager _manager;
        private ulong _currentWeapon;
        private DateTime _lastWeaponCheck;
        private const int WEAPON_CHECK_INTERVAL = 100; // ms

        public WeaponManager(ulong playerController, bool inGame, Manager manager)
        {
            _playerController = playerController;
            _inGame = inGame;
            _manager = manager;
            _weaponFeatures = new List<Weapon>();
            _currentWeapon = 0;
            _lastWeaponCheck = DateTime.MinValue;
        }

        /// <summary>
        /// Registers a weapon feature to receive weapon change notifications
        /// </summary>
        public void RegisterFeature(Weapon feature)
        {
            if (!_weaponFeatures.Contains(feature))
            {
                _weaponFeatures.Add(feature);
                
                if (_currentWeapon == 0)
                {
                    try
                    {
                        ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                        if (playerState != 0)
                        {
                            ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                            if (soldierActor != 0)
                            {
                                ulong inventoryComponent = Memory.ReadPtr(soldierActor + ASQSoldier.InventoryComponent);
                                if (inventoryComponent != 0)
                                {
                                    _currentWeapon = Memory.ReadPtr(inventoryComponent + USQPawnInventoryComponent.CurrentWeapon);
                                    if (_currentWeapon != 0 && feature.IsEnabled)
                                    {
                                        feature.OnWeaponChanged(_currentWeapon, 0);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error initializing current weapon for feature: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Unregisters a weapon feature
        /// </summary>
        public void UnregisterFeature(Weapon feature)
        {
            _weaponFeatures.Remove(feature);
        }

        /// <summary>
        /// Checks for weapon changes and notifies features if needed
        /// </summary>
        public void Update()
        {
            if (!_inGame || _playerController == 0) return;

            // Limit how often we check for weapon changes
            if ((DateTime.Now - _lastWeaponCheck).TotalMilliseconds < WEAPON_CHECK_INTERVAL)
                return;

            _lastWeaponCheck = DateTime.Now;

            try
            {
                ulong newWeapon = 0;

                // Check for vehicle weapon first
                if (_manager.IsInVehicle())
                {
                    newWeapon = _manager.GetVehicleWeapon();
                }
                
                // If not in vehicle or no vehicle weapon, check infantry weapon
                if (newWeapon == 0)
                {
                    ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                    if (playerState == 0) return;

                    ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                    if (soldierActor == 0) return;

                    ulong inventoryComponent = Memory.ReadPtr(soldierActor + ASQSoldier.InventoryComponent);
                    if (inventoryComponent == 0) return;

                    newWeapon = Memory.ReadPtr(inventoryComponent + USQPawnInventoryComponent.CurrentWeapon);
                }

                // Only notify if the weapon has actually changed
                if (newWeapon != 0 && newWeapon != _currentWeapon)
                {
                    foreach (var feature in _weaponFeatures)
                    {
                        try
                        {
                            if (feature.IsEnabled)
                            {
                                feature.OnWeaponChanged(newWeapon, _currentWeapon);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error in weapon feature OnWeaponChanged: {ex.Message}");
                        }
                    }
                    _currentWeapon = newWeapon;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking weapon changes: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _weaponFeatures.Clear();
        }
    }
} 