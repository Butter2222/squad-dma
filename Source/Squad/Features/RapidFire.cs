using System;
using Offsets;
using squad_dma;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class RapidFire : Manager, Weapon
    {
        public const string NAME = "RapidFire";
        private ulong _lastWeapon = 0;
        private ulong _lastVehicleWeapon = 0;
                
        // Original values for soldier weapon
        private float _soldierOriginalTimeBetweenShots = 0.0f;
        private float _soldierOriginalTimeBetweenSingleShots = 0.0f;
        
        // Original values for vehicle weapon
        private float _vehicleOriginalTimeBetweenShots = 0.0f;
        private float _vehicleOriginalTimeBetweenSingleShots = 0.0f;
        
        public RapidFire(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid())
            {
                Logger.Error($"[{NAME}] Cannot enable/disable rapid fire - local player is not valid");
                return;
            }
            
            Logger.Debug($"[{NAME}] Rapid fire {(enable ? "enabled" : "disabled")}");
            
            // If disabling, restore the last weapons' states
            if (!enable)
            {
                if (_lastWeapon != 0)
                {
                    RestoreWeapon(_lastWeapon, ref _soldierOriginalTimeBetweenShots, ref _soldierOriginalTimeBetweenSingleShots, false);
                }
                if (_lastVehicleWeapon != 0)
                {
                    RestoreWeapon(_lastVehicleWeapon, ref _vehicleOriginalTimeBetweenShots, ref _vehicleOriginalTimeBetweenSingleShots, true);
                }
            }
            
            // Apply to current weapons if enabled
            if (enable)
            {
                UpdateCachedPointers();
                if (_cachedCurrentWeapon != 0)
                {
                    Apply(_cachedCurrentWeapon, ref _soldierOriginalTimeBetweenShots, ref _soldierOriginalTimeBetweenSingleShots, false);
                }

                // Check for vehicle weapon
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                if (playerState != 0)
                {
                    ulong currentSeat = Memory.ReadPtr(playerState + ASQPlayerState.CurrentSeat);
                    if (currentSeat != 0)
                    {
                        ulong seatPawn = Memory.ReadPtr(currentSeat + USQVehicleSeatComponent.SeatPawn);
                        if (seatPawn != 0)
                        {
                            ulong vehicleInventory = Memory.ReadPtr(seatPawn + ASQVehicleSeat.VehicleInventory);
                            if (vehicleInventory != 0)
                            {
                                ulong vehicleWeapon = Memory.ReadPtr(vehicleInventory + USQPawnInventoryComponent.CurrentWeapon);
                                if (vehicleWeapon != 0)
                                {
                                    Apply(vehicleWeapon, ref _vehicleOriginalTimeBetweenShots, ref _vehicleOriginalTimeBetweenSingleShots, true);
                                }
                            }
                        }
                    }
                }
            }
        }
        
        public void OnWeaponChanged(ulong newWeapon, ulong oldWeapon)
        {
            if (!IsLocalPlayerValid()) return;
            
            try
            {
                // Restore the old weapon's state
                if (oldWeapon != 0)
                {
                    RestoreWeapon(oldWeapon, ref _soldierOriginalTimeBetweenShots, ref _soldierOriginalTimeBetweenSingleShots, false);
                }
                
                // Apply to new weapon if enabled
                if (Program.Config.RapidFire && newWeapon != 0)
                {
                    Apply(newWeapon, ref _soldierOriginalTimeBetweenShots, ref _soldierOriginalTimeBetweenSingleShots, false);
                }
                
                _lastWeapon = newWeapon;
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error handling weapon change: {ex.Message}");
            }
        }
        
        private void RestoreWeapon(ulong weapon, ref float originalTimeBetweenShots, ref float originalTimeBetweenSingleShots, bool isVehicle)
        {
            try
            {
                if (originalTimeBetweenShots != 0.0f)
                {
                    ulong weaponConfigOffset = weapon + ASQWeapon.WeaponConfig;
                    Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenShots, originalTimeBetweenShots);
                    Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenSingleShots, originalTimeBetweenSingleShots);
                    Logger.Debug($"[{NAME}] Restored original {(isVehicle ? "vehicle" : "soldier")} weapon values: TimeBetweenShots={originalTimeBetweenShots}, TimeBetweenSingleShots={originalTimeBetweenSingleShots}");
                }
                else
                {
                    Logger.Error($"[{NAME}] Cannot restore {(isVehicle ? "vehicle" : "soldier")} weapon values - original values not loaded");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error restoring {(isVehicle ? "vehicle" : "soldier")} weapon: {ex.Message}");
            }
        }
        
        private void Apply(ulong weapon, ref float originalTimeBetweenShots, ref float originalTimeBetweenSingleShots, bool isVehicle)
        {
            try
            {
                ulong weaponConfigOffset = weapon + ASQWeapon.WeaponConfig;
                Logger.Debug($"[{NAME}] Found weapon config at 0x{weaponConfigOffset:X} for {(isVehicle ? "vehicle" : "soldier")} weapon");

                if (Program.Config.RapidFire)
                {
                    // Store original values when first enabled
                    if (originalTimeBetweenShots == 0.0f)
                    {
                        originalTimeBetweenShots = Memory.ReadValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenShots);
                        originalTimeBetweenSingleShots = Memory.ReadValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenSingleShots);

                        Logger.Debug($"[{NAME}] Loaded original {(isVehicle ? "vehicle" : "soldier")} weapon values: TimeBetweenShots={originalTimeBetweenShots}, TimeBetweenSingleShots={originalTimeBetweenSingleShots}");

                        // Save original values to config
                        if (Config.TryLoadConfig(out var config))
                        {
                            if (isVehicle)
                            {
                                config.OriginalVehicleTimeBetweenShots = originalTimeBetweenShots;
                                config.OriginalVehicleTimeBetweenSingleShots = originalTimeBetweenSingleShots;
                            }
                            else
                            {
                                config.OriginalTimeBetweenShots = originalTimeBetweenShots;
                                config.OriginalTimeBetweenSingleShots = originalTimeBetweenSingleShots;
                            }
                            Config.SaveConfig(config);
                            Logger.Debug($"[{NAME}] Saved original {(isVehicle ? "vehicle" : "soldier")} weapon values to config");
                        }
                        else
                        {
                            Logger.Error($"[{NAME}] Failed to save original {(isVehicle ? "vehicle" : "soldier")} weapon values to config");
                        }
                    }

                    const float RAPID_FIRE_VALUE = 0.01f;
                    Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenShots, RAPID_FIRE_VALUE);
                    Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenSingleShots, RAPID_FIRE_VALUE);
                    Logger.Debug($"[{NAME}] Set {(isVehicle ? "vehicle" : "soldier")} weapon rapid fire values to {RAPID_FIRE_VALUE}");
                }
                else
                {
                    RestoreWeapon(weapon, ref originalTimeBetweenShots, ref originalTimeBetweenSingleShots, isVehicle);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error applying rapid fire to {(isVehicle ? "vehicle" : "soldier")} weapon: {ex.Message}");
            }
        }
        
        // Override Apply to do nothing since we handle weapon changes in OnWeaponChanged
        public override void Apply() { }
    }
} 