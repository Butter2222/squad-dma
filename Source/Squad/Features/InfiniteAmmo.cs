using System;
using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// State-aware InfiniteAmmo feature that prevents memory write spam and only applies when player is alive
    /// </summary>
    public class InfiniteAmmo : StateAwareFeature, Weapon
    {
        public const string NAME = "InfiniteAmmo";
        
        private ulong _lastWeapon = 0;
        private ulong _lastVehicleWeapon = 0;
        
        // Track applied weapons to avoid re-application
        private HashSet<ulong> _appliedWeapons = new HashSet<ulong>();
        
        public InfiniteAmmo(ulong playerController, bool inGame, Game game)
            : base(playerController, inGame, game, NAME)
        {
        }
        
        /// <summary>
        /// Override SetEnabled to immediately apply/restore to current weapon when enabled/disabled
        /// </summary>
        public override void SetEnabled(bool enable)
        {
            if (enable)
            {
                _isEnabled = enable;
                Logger.Debug($"[{_featureName}] Feature enabled");
                
                // If enabling, immediately apply to current weapon if player is alive
                if (IsLocalPlayerValid() && _cachedCurrentWeapon != 0)
                {
                    ApplyToWeapon(_cachedCurrentWeapon);
                    _isApplied = true; // Mark as applied
                }
            }
            else
            {
                _isEnabled = enable;
                Logger.Debug($"[{_featureName}] Feature disabled");
                
                // If disabling, immediately restore all applied weapons
                if (_appliedWeapons.Count > 0)
                {
                    foreach (ulong weaponPtr in _appliedWeapons.ToList())
                    {
                        RestoreWeapon(weaponPtr);
                    }
                    _isApplied = false; // Mark as not applied
                }
            }
        }
        
        protected override bool ShouldApplyModifications()
        {
            return Program.Config.InfiniteAmmo;
        }
        
        protected override void LoadOriginalValues()
        {
            SafeLoadOriginals(() =>
            {
                // InfiniteAmmo doesn't need to load original values as it only toggles flags
                // The weapon system handles original state restoration
                Logger.Debug($"[{_featureName}] InfiniteAmmo uses weapon flag toggles - no original values to load");
                
            }, "InfiniteAmmo weapon flags");
        }
        
        protected override void ApplyModifications()
        {
            SafeApplyModifications(() =>
            {
                int weaponsModified = 0;
                
                // Apply to current infantry weapon
                if (_cachedCurrentWeapon != 0)
                {
                    ApplyToWeapon(_cachedCurrentWeapon);
                    weaponsModified++;
                }
                
                // Apply to vehicle weapon if in vehicle
                if (IsInVehicle() && _cachedVehicleWeapon != 0)
                {
                    ApplyToWeapon(_cachedVehicleWeapon);
                    weaponsModified++;
                }
                
                Logger.Debug($"[{_featureName}] Applied infinite ammo to {weaponsModified} weapon(s)");
                
            }, "InfiniteAmmo weapon modifications");
        }
        
        protected override void RestoreOriginalValues()
        {
            SafeRestoreValues(() =>
            {
                int weaponsRestored = 0;
                
                // Restore all previously applied weapons
                foreach (ulong weaponPtr in _appliedWeapons.ToList())
                {
                    RestoreWeapon(weaponPtr);
                    weaponsRestored++;
                }
                
                // Clear applied weapons list
                _appliedWeapons.Clear();
                
                Logger.Debug($"[{_featureName}] Restored {weaponsRestored} weapon(s) to original ammo state");
                
            }, "InfiniteAmmo original weapon states");
        }
        
        // Weapon interface implementation
        public void OnWeaponChanged(ulong newWeapon, ulong oldWeapon)
        {
            try
            {
                // Restore old weapon if it was applied
                if (oldWeapon != 0 && _appliedWeapons.Contains(oldWeapon))
                {
                    RestoreWeapon(oldWeapon);
                }
                
                // Track weapon change
                _lastWeapon = newWeapon;
                
                // Apply to new weapon if enabled and state allows
                if (_isEnabled && ShouldApplyModifications() && newWeapon != 0)
                {
                    ApplyToWeapon(newWeapon);
                }
                
                // Handle vehicle weapon changes
                ulong vehicleWeapon = IsInVehicle() ? _cachedVehicleWeapon : 0;
                if (vehicleWeapon != _lastVehicleWeapon)
                {
                    if (_lastVehicleWeapon != 0 && _appliedWeapons.Contains(_lastVehicleWeapon))
                    {
                        RestoreWeapon(_lastVehicleWeapon);
                    }
                    
                    _lastVehicleWeapon = vehicleWeapon;
                    
                    if (_isEnabled && ShouldApplyModifications() && vehicleWeapon != 0)
                    {
                        ApplyToWeapon(vehicleWeapon);
                    }
                }
                
                Logger.Debug($"[{_featureName}] Weapon changed");
                
            }
            catch (Exception ex)
            {
                Logger.Error($"[{_featureName}] Error handling weapon change: {ex.Message}");
            }
        }
        
        private void ApplyToWeapon(ulong weapon)
        {
            try
            {
                if (weapon == 0) return;
                    
                ulong weaponConfigOffset = weapon + ASQWeapon.WeaponConfig;
                
                // bInfiniteAmmo and bInfiniteMags are bit flags within the same byte
                // bInfiniteAmmo = bit 0, bInfiniteMags = bit 1
                byte currentFlags = Memory.ReadValue<byte>(weaponConfigOffset);
                
                // Set bit 0 (bInfiniteAmmo) and bit 1 (bInfiniteMags)
                byte newFlags = (byte)(currentFlags | 0x03); // Set bits 0 and 1
                
                // Write the new flags byte
                Memory.WriteValue<byte>(weaponConfigOffset, newFlags);
                
                // Try writing to weapon static info as well (in case there are multiple locations)
                try
                {
                    ulong itemStaticInfo = Memory.ReadPtr(weapon + ASQEquipableItem.ItemStaticInfo);
                    if (itemStaticInfo != 0)
                    {
                        Memory.WriteValue<byte>(itemStaticInfo, newFlags);
                    }
                }
                catch { }
                
                // Try to force weapon refresh by writing to CurrentFireMode and back
                try
                {
                    int currentFireMode = Memory.ReadValue<int>(weapon + ASQWeapon.CurrentFireMode);
                    Memory.WriteValue<int>(weapon + ASQWeapon.CurrentFireMode, currentFireMode);
                }
                catch { }
                
                _appliedWeapons.Add(weapon);
                Logger.Debug($"[{_featureName}] Applied infinite ammo to weapon");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{_featureName}] Error applying infinite ammo: {ex.Message}");
            }
        }
        
        private void RestoreWeapon(ulong weapon)
        {
            try
            {
                if (weapon == 0) return;
                    
                ulong weaponConfigOffset = weapon + ASQWeapon.WeaponConfig;
                
                // Read current flags byte
                byte currentFlags = Memory.ReadValue<byte>(weaponConfigOffset);
                
                // Clear bit 0 (bInfiniteAmmo) and bit 1 (bInfiniteMags)
                byte newFlags = (byte)(currentFlags & ~0x03); // Clear bits 0 and 1
                
                // Write the new flags byte
                Memory.WriteValue<byte>(weaponConfigOffset, newFlags);
                
                _appliedWeapons.Remove(weapon);
                Logger.Debug($"[{_featureName}] Restored weapon ammo");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{_featureName}] Error restoring weapon: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Override Apply to do nothing since we handle weapon changes in OnWeaponChanged and state management in base class
        /// </summary>
        public override void Apply()
        {
            // The base StateAwareFeature.Apply() handles state management
            base.Apply();
        }
    }
} 