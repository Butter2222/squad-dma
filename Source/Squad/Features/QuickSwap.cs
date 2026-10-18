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
        
        private ulong _lastWeaponStaticInfo = 0;

        // Durations on the shared item static info: two flat floats followed by the swap durations of each FSQMutualWeapon entry
        private static readonly uint[] StaticInfoDurationOffsets =
        {
            USQItemStaticInfo.EmptyUnequipDuration,
            USQItemStaticInfo.EmptyEquipDuration,
        };

        private static readonly uint[] MutualWeaponDurationOffsets =
        {
            FSQMutualWeapon.BothEmptyEquipDuration,
            FSQMutualWeapon.PreviousEmptyOrIgnoreCurrentEquipDuration,
            FSQMutualWeapon.CurrentEmptyOrIgnorePreviousEquipDuration,
            FSQMutualWeapon.NoneEmptyOrIgnoreBothEquipDuration,
            FSQMutualWeapon.CurrentEmptyUnequipDuration,
            FSQMutualWeapon.NoneOrIgnoreCurrentUnequipDuration,
        };

        private const float FAST_SWAP_VALUE = 0.01f;
        private const int MAX_MUTUAL_WEAPONS = 64;

        private readonly Dictionary<ulong, float[]> _originalStaticDurations = new Dictionary<ulong, float[]>();
        private readonly List<ulong> _durationAddresses = new List<ulong>(16);

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
            
            if (!enable)
            {
                RestoreStaticDurations();
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
                ulong newWeaponStaticInfo = GetCachedWeaponStaticInfo(newWeapon);
                ulong oldWeaponStaticInfo = oldWeapon != 0 ? GetCachedWeaponStaticInfo(oldWeapon) : 0;
                
                if (oldWeapon != 0 && oldWeaponStaticInfo != _lastWeaponStaticInfo)
                {
                    RestoreWeapon(oldWeapon);
                }
                
                if (Program.Config.QuickSwap && newWeapon != 0 && newWeaponStaticInfo != _lastWeaponStaticInfo)
                {
                    Apply(newWeapon);
                }
                
                _lastWeaponStaticInfo = newWeaponStaticInfo;
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
                Logger.Debug($"[{NAME}] Setting quick swap values to {FAST_SWAP_VALUE} for weapon at 0x{weapon:X}");
                
                Memory.WriteValue<float>(weapon + ASQEquipableItem.EquipDuration, FAST_SWAP_VALUE);
                Memory.WriteValue<float>(weapon + ASQEquipableItem.UnequipDuration, FAST_SWAP_VALUE);
                Memory.WriteValue<float>(weapon + ASQEquipableItem.CachedEquipDuration, FAST_SWAP_VALUE);
                Memory.WriteValue<float>(weapon + ASQEquipableItem.CachedUnequipDuration, FAST_SWAP_VALUE);

                ApplyStaticDurations(GetCachedWeaponStaticInfo(weapon));
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error applying quick swap to weapon at 0x{weapon:X}: {ex.Message}");
            }
        }

        private void CollectStaticDurationAddresses(ulong staticInfo)
        {
            _durationAddresses.Clear();
            foreach (uint offset in StaticInfoDurationOffsets)
                _durationAddresses.Add(staticInfo + offset);

            ulong mutualWeapons = staticInfo + USQItemStaticInfo.MutualWeapons;
            ulong entries = Memory.ReadPtr(mutualWeapons);
            int count = Memory.ReadValue<int>(mutualWeapons + 0x8);
            if (entries == 0 || count <= 0 || count > MAX_MUTUAL_WEAPONS) return;

            for (int i = 0; i < count; i++)
            {
                ulong entry = entries + (ulong)i * FSQMutualWeapon.Size;
                foreach (uint offset in MutualWeaponDurationOffsets)
                    _durationAddresses.Add(entry + offset);
            }
        }

        // Static info is shared by every instance of the weapon, so each one is patched once until restored
        private void ApplyStaticDurations(ulong staticInfo)
        {
            if (staticInfo == 0 || _originalStaticDurations.ContainsKey(staticInfo)) return;

            CollectStaticDurationAddresses(staticInfo);
            var originals = new float[_durationAddresses.Count];
            for (int i = 0; i < originals.Length; i++)
                originals[i] = Memory.ReadValue<float>(_durationAddresses[i]);

            for (int i = 0; i < originals.Length; i++)
                Memory.WriteValue<float>(_durationAddresses[i], FAST_SWAP_VALUE);

            _originalStaticDurations[staticInfo] = originals;
        }

        private void RestoreStaticDurations()
        {
            foreach (var kvp in _originalStaticDurations)
            {
                try
                {
                    CollectStaticDurationAddresses(kvp.Key);
                    if (_durationAddresses.Count != kvp.Value.Length) continue;

                    for (int i = 0; i < kvp.Value.Length; i++)
                        Memory.WriteValue<float>(_durationAddresses[i], kvp.Value[i]);
                }
                catch (Exception ex)
                {
                    Logger.Error($"[{NAME}] Error restoring static info at 0x{kvp.Key:X}: {ex.Message}");
                }
            }
            _originalStaticDurations.Clear();
        }
        
        // Override Apply to do nothing since we handle weapon changes in OnWeaponChanged
        public override void Apply() { }
    }
} 