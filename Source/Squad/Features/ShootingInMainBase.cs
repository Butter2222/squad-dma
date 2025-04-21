using System;
using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class ShootingInMainBase : Manager, Weapon
    {
        public const string NAME = "ShootingInMainBase";
        private bool _isEnabled = false;
        
        public bool IsEnabled => _isEnabled;
        
        private ulong _lastWeapon = 0;
        
        public ShootingInMainBase(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid())
            {
                Logger.Error($"[{NAME}] Cannot enable/disable shooting in main base - local player is not valid");
                return;
            }
            
            _isEnabled = enable;
            Logger.Debug($"[{NAME}] Shooting in main base {(enable ? "enabled" : "disabled")}");
            
            if (!enable)
            {
                if (_lastWeapon != 0)
                {
                    RestoreWeapon(_lastWeapon);
                }
                
                UpdateCachedPointers();
                if (_cachedCurrentWeapon != 0)
                {
                    RestoreWeapon(_cachedCurrentWeapon);
                }
            }
            
            if (enable)
            {
                UpdateCachedPointers();
                if (_cachedCurrentWeapon != 0)
                {
                    Apply(_cachedCurrentWeapon);
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
                if (Program.Config.AllowShootingInMainBase && newWeapon != 0)
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
                ulong itemStaticInfo = Memory.ReadPtr(weapon + ASQEquipableItem.ItemStaticInfo);
                if (itemStaticInfo == 0)
                {
                    Logger.Error($"[{NAME}] Cannot restore weapon - item static info is not valid");
                    return;
                }

                Memory.WriteValue<bool>(itemStaticInfo + ASQSoldier.bUsableInMainBase, false);
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
                ulong itemStaticInfo = Memory.ReadPtr(weapon + ASQEquipableItem.ItemStaticInfo);
                if (itemStaticInfo == 0)
                {
                    Logger.Error($"[{NAME}] Cannot apply shooting in main base to weapon - item static info is not valid");
                    return;
                }

                Memory.WriteValue<bool>(itemStaticInfo + ASQSoldier.bUsableInMainBase, Program.Config.AllowShootingInMainBase);
                Logger.Debug($"[{NAME}] Applied shooting in main base to weapon at 0x{weapon:X}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error applying shooting in main base to weapon at 0x{weapon:X}: {ex.Message}");
            }
        }
        
        // Override Apply to do nothing since we handle weapon changes in OnWeaponChanged
        public override void Apply() { }
    }
} 