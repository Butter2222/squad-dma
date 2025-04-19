using System;
using System.Collections.Generic;
using Offsets;
using squad_dma;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class ForceFullAuto : Manager, Weapon
    {
        public const string NAME = "ForceFullAuto";
                
        private bool _isEnabled = false;
        
        public bool IsEnabled => _isEnabled;
        
        private int[] _originalFireModes = null;
        private bool _originalManualBolt = false;
        private bool _originalRequireAdsToShoot = false;
        private ulong _lastWeapon = 0;
        
        public ForceFullAuto(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid())
            {
                Logger.Error($"[{NAME}] Cannot enable/disable force full auto - local player is not valid");
                return;
            }
            
            _isEnabled = enable;
            Logger.Debug($"[{NAME}] Force Full Auto {(enable ? "enabled" : "disabled")}");
            
            // If disabling, restore the last weapon's state
            if (!enable && _lastWeapon != 0)
            {
                RestoreWeapon(_lastWeapon);
            }
            
            // Apply to current weapon if enabled
            if (enable)
            {
                UpdateCachedPointers();
                if (_cachedCurrentWeapon != 0)
                {
                    Apply(_cachedCurrentWeapon);
                }

                // Check for vehicle weapon
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                if (playerState != 0)
                {
                    ulong currentSeat = Memory.ReadPtr(playerState + ASQPlayerState.CurrentSeat);
                    if (currentSeat != 0)
                    {
                        Logger.Debug($"[{NAME}] Found current seat at 0x{currentSeat:X}");
                        ulong seatPawn = Memory.ReadPtr(currentSeat + USQVehicleSeatComponent.SeatPawn);
                        if (seatPawn != 0)
                        {
                            Logger.Debug($"[{NAME}] Found seat pawn at 0x{seatPawn:X}");
                            ulong vehicleInventory = Memory.ReadPtr(seatPawn + ASQVehicleSeat.VehicleInventory);
                            if (vehicleInventory != 0)
                            {
                                Logger.Debug($"[{NAME}] Found vehicle inventory at 0x{vehicleInventory:X}");
                                ulong vehicleWeapon = Memory.ReadPtr(vehicleInventory + USQPawnInventoryComponent.CurrentWeapon);
                                if (vehicleWeapon != 0)
                                {
                                    Logger.Debug($"[{NAME}] Applying force full auto to vehicle weapon at 0x{vehicleWeapon:X}");
                                    Apply(vehicleWeapon);
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
                    RestoreWeapon(oldWeapon);
                }
                
                // Apply to new weapon if enabled
                if (Program.Config.ForceFullAuto && newWeapon != 0)
                {
                    Apply(newWeapon);
                }
                
                _lastWeapon = newWeapon;
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error handling weapon change: {ex.Message}");
            }
        }
        
        private void RestoreWeapon(ulong weapon)
        {
            try
            {
                ulong weaponConfig = weapon + ASQWeapon.WeaponConfig;
                ulong fireModesArray = weaponConfig + FSQWeaponData.FireModes;
                ulong fireModesData = Memory.ReadPtr(fireModesArray);
                if (fireModesData == 0)
                {
                    Logger.Error($"[{NAME}] Failed to get fire modes data");
                    return;
                }
                
                int fireModeCount = Memory.ReadValue<int>(fireModesArray + 0x8);
                if (fireModeCount <= 0)
                {
                    Logger.Error($"[{NAME}] Invalid fire mode count");
                    return;
                }
                
                ulong itemStaticInfo = Memory.ReadPtr(weapon + ASQEquipableItem.ItemStaticInfo);
                if (itemStaticInfo == 0)
                {
                    Logger.Error($"[{NAME}] Failed to get weapon static info");
                    return;
                }

                // Restore original values or use defaults if not found
                if (_originalFireModes != null)
                {
                    for (int i = 0; i < fireModeCount && i < _originalFireModes.Length; i++)
                    {
                        ulong fireModeAddress = fireModesData + ((ulong)i * 4);
                        Memory.WriteValue(fireModeAddress, _originalFireModes[i]);
                    }
                    
                    Memory.WriteValue(itemStaticInfo + USQWeaponStaticInfo.bRequiresManualBolt, _originalManualBolt);
                    Memory.WriteValue(itemStaticInfo + USQWeaponStaticInfo.bRequireAdsToShoot, _originalRequireAdsToShoot);
                    Logger.Debug($"[{NAME}] Restored original fire modes: {string.Join(", ", _originalFireModes)}, ManualBolt={_originalManualBolt}, RequireAdsToShoot={_originalRequireAdsToShoot}");
                }
                else
                {
                    for (int i = 0; i < fireModeCount; i++)
                    {
                        ulong fireModeAddress = fireModesData + ((ulong)i * 4);
                        Memory.WriteValue(fireModeAddress, 1);
                    }
                    Memory.WriteValue(itemStaticInfo + USQWeaponStaticInfo.bRequiresManualBolt, true);
                    Memory.WriteValue(itemStaticInfo + USQWeaponStaticInfo.bRequireAdsToShoot, true);
                    Logger.Debug($"[{NAME}] Restored default fire modes (Single Fire) and enabled manual bolt and ADS requirement");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error restoring weapon at 0x{weapon:X}: {ex.Message}");
            }
        }
        
        private void Apply(ulong weapon)
        {
            try
            {
                ulong weaponConfig = weapon + ASQWeapon.WeaponConfig;
                ulong fireModesArray = weaponConfig + FSQWeaponData.FireModes;
                ulong fireModesData = Memory.ReadPtr(fireModesArray);
                if (fireModesData == 0)
                {
                    Logger.Error($"[{NAME}] Failed to get fire modes data");
                    return;
                }
                
                int fireModeCount = Memory.ReadValue<int>(fireModesArray + 0x8);
                if (fireModeCount <= 0)
                {
                    Logger.Error($"[{NAME}] Invalid fire mode count");
                    return;
                }
                
                ulong itemStaticInfo = Memory.ReadPtr(weapon + ASQEquipableItem.ItemStaticInfo);
                if (itemStaticInfo == 0)
                {
                    Logger.Error($"[{NAME}] Failed to get weapon static info");
                    return;
                }

                if (_originalFireModes == null)
                {
                    _originalFireModes = new int[fireModeCount];
                    for (int i = 0; i < fireModeCount; i++)
                    {
                        ulong fireModeAddress = fireModesData + ((ulong)i * 4);
                        _originalFireModes[i] = Memory.ReadValue<int>(fireModeAddress);
                    }
                    _originalManualBolt = Memory.ReadValue<bool>(itemStaticInfo + USQWeaponStaticInfo.bRequiresManualBolt);
                    _originalRequireAdsToShoot = Memory.ReadValue<bool>(itemStaticInfo + USQWeaponStaticInfo.bRequireAdsToShoot);

                    Logger.Debug($"[{NAME}] Stored original fire modes: {string.Join(", ", _originalFireModes)}, ManualBolt={_originalManualBolt}, RequireAdsToShoot={_originalRequireAdsToShoot}");

                    // Save original values to config
                    if (Config.TryLoadConfig(out var config))
                    {
                        config.OriginalFireModes = _originalFireModes;
                        config.OriginalManualBolt = _originalManualBolt;
                        config.OriginalRequireAdsToShoot = _originalRequireAdsToShoot;
                        Config.SaveConfig(config);
                        Logger.Debug($"[{NAME}] Saved original values to config");
                    }
                }

                for (int i = 0; i < fireModeCount; i++)
                {
                    ulong fireModeAddress = fireModesData + ((ulong)i * 4);
                    Memory.WriteValue(fireModeAddress, -1);
                }
                
                Memory.WriteValue(weapon + ASQWeapon.CurrentFireMode, 0);
                Memory.WriteValue(itemStaticInfo + USQWeaponStaticInfo.bRequiresManualBolt, false);
                Memory.WriteValue(itemStaticInfo + USQWeaponStaticInfo.bRequireAdsToShoot, false);
                
                Logger.Debug($"[{NAME}] Set all fire modes to Full Auto (-1) and disabled manual bolt and ADS requirement");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error applying force full auto to weapon at 0x{weapon:X}: {ex.Message}");
            }
        }
        
        // Override Apply to do nothing since we handle weapon changes in OnWeaponChanged
        public override void Apply() { }
    }
} 