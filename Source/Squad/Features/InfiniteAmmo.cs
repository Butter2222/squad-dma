using System;
using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class InfiniteAmmo : Manager, Weapon
    {
        public const string NAME = "InfiniteAmmo";
        private ulong _lastWeapon = 0;
        private bool _isEnabled = false;
        
        public bool IsEnabled => _isEnabled;
        
        public InfiniteAmmo(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid())
            {
                Logger.Error($"[{NAME}] Cannot enable/disable infinite ammo - local player is not valid");
                return;
            }
            
            _isEnabled = enable;
            Logger.Debug($"[{NAME}] Infinite ammo {(enable ? "enabled" : "disabled")}");
            
            UpdateCachedPointers();
            
            if (!enable)
            {
                // Restore both infantry and vehicle weapons
                if (_lastWeapon != 0)
                {
                    RestoreWeapon(_lastWeapon);
                }
                
                if (_cachedCurrentWeapon != 0)
                {
                    RestoreWeapon(_cachedCurrentWeapon);
                }
                
                if (IsInVehicle() && _cachedVehicleWeapon != 0)
                {
                    RestoreWeapon(_cachedVehicleWeapon);
                }
            }
            else
            {
                // Apply to both infantry and vehicle weapons
                if (_cachedCurrentWeapon != 0)
                {
                    Apply(_cachedCurrentWeapon);
                }
                
                if (IsInVehicle() && _cachedVehicleWeapon != 0)
                {
                    Apply(_cachedVehicleWeapon);
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
                if (_isEnabled && newWeapon != 0)
                {
                    Apply(newWeapon);
                }
                
                _lastWeapon = newWeapon;
                
                // Also handle vehicle weapon if in vehicle
                if (IsInVehicle() && _cachedVehicleWeapon != 0)
                {
                    if (_isEnabled)
                    {
                        Apply(_cachedVehicleWeapon);
                    }
                    else
                    {
                        RestoreWeapon(_cachedVehicleWeapon);
                    }
                }
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
                ulong weaponConfigOffset = weapon + ASQWeapon.WeaponConfig;
                Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteAmmo, 0);
                Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteMags, 0);
                Logger.Debug($"[{NAME}] Restored weapon at 0x{weapon:X}");
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
                ulong weaponConfigOffset = weapon + ASQWeapon.WeaponConfig;
                Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteAmmo, 1);
                Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteMags, 1);
                Logger.Debug($"[{NAME}] Applied infinite ammo to weapon at 0x{weapon:X}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error applying infinite ammo to weapon at 0x{weapon:X}: {ex.Message}");
            }
        }
        
        // Override Apply to do nothing since we handle weapon changes in OnWeaponChanged
        public override void Apply() { }
    }
} 