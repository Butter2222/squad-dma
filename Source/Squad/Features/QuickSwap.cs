using System;
using Offsets;
using squad_dma;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class QuickSwap : Manager, Weapon
    {
        public const string NAME = "QuickSwap";
        private bool _isEnabled = false;
        
        public bool IsEnabled => _isEnabled;
        
        private ulong _lastWeapon = 0;
        
        public QuickSwap(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid())
            {
                Logger.Error($"[{NAME}] Cannot enable/disable quick swap - local player is not valid");
                return;
            }
            
            _isEnabled = enable;
            Logger.Debug($"[{NAME}] Quick swap {(enable ? "enabled" : "disabled")}");
            
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
                if (Program.Config.QuickSwap && newWeapon != 0)
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
                Logger.Debug($"[{NAME}] Restoring default swap values for weapon at 0x{weapon:X}");
                
                Memory.WriteValue<float>(weapon + ASQEquipableItem.EquipDuration, 1.2f);
                Memory.WriteValue<float>(weapon + ASQEquipableItem.UnequipDuration, 1.067f);
                Memory.WriteValue<float>(weapon + ASQEquipableItem.CachedEquipDuration, 1.2f);
                Memory.WriteValue<float>(weapon + ASQEquipableItem.CachedUnequipDuration, 1.067f);
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
                const float FAST_SWAP_VALUE = 0.01f;
                Logger.Debug($"[{NAME}] Setting quick swap values to {FAST_SWAP_VALUE} for weapon at 0x{weapon:X}");
                
                Memory.WriteValue<float>(weapon + ASQEquipableItem.EquipDuration, FAST_SWAP_VALUE);
                Memory.WriteValue<float>(weapon + ASQEquipableItem.UnequipDuration, FAST_SWAP_VALUE);
                Memory.WriteValue<float>(weapon + ASQEquipableItem.CachedEquipDuration, FAST_SWAP_VALUE);
                Memory.WriteValue<float>(weapon + ASQEquipableItem.CachedUnequipDuration, FAST_SWAP_VALUE);
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error applying quick swap to weapon at 0x{weapon:X}: {ex.Message}");
            }
        }
        
        // Override Apply to do nothing since we handle weapon changes in OnWeaponChanged
        public override void Apply() { }
    }
} 