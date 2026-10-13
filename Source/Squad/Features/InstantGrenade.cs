using Offsets;
using squad_dma.Source.Squad;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class InstantGrenade : Manager, Weapon
    {
        public const string NAME = "InstantGrenade";
        private bool _isEnabled = false;
        private ulong _lastAppliedWeapon = 0;

        private Dictionary<ulong, float> _originalGrenadeValues = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> _originalAnimValues = new Dictionary<ulong, float>();
        private Dictionary<ulong, bool> _originalAnimBoolValues = new Dictionary<ulong, bool>();
        private int _originalItemCount;
        private int _originalMaxItemCount;

        private static readonly (uint offset, float value)[] GrenadeTimingFields =
        {
            (FSQGrenadeData.ThrowReadyTime, 0f),
            (FSQGrenadeData.OverhandThrowTime, 0f),
            (FSQGrenadeData.UnderhandThrowTime, 0f),
            (FSQGrenadeData.OverhandThrowDuration, 0f),
            (FSQGrenadeData.UnderhandThrowDuration, 0f),
            (FSQGrenadeData.ThrowModeTransitionTime, 0f),
            (FSQGrenadeData.ReloadTime, 0f),
        };

        private static readonly (uint offset, float value)[] MontageFloatFields =
        {
            (UAnimSequenceBase.SequenceLength, 0f),
            (UAnimSequenceBase.RateScale, 1000f),
            (UAnimMontage.BlendInTime, 0f),
            (UAnimMontage.BlendOutTime, 0f),
            (UAnimMontage.BlendOutTriggerTime, 0f),
        };

        private static readonly uint[] StaticInfoMontageOffsets =
        {
            USQGrenadeStaticInfo.WeaponOverhandPinpull1pMontage,
            USQGrenadeStaticInfo.WeaponOverhandPinpull3pMontage,
            USQGrenadeStaticInfo.OverhandPinpull1pMontage,
            USQGrenadeStaticInfo.OverhandPinpull3pMontage,
            USQGrenadeStaticInfo.WeaponOverhandThrow1pMontage,
            USQGrenadeStaticInfo.WeaponOverhandThrow3pMontage,
            USQGrenadeStaticInfo.OverhandThrow1pMontage,
            USQGrenadeStaticInfo.OverhandThrow3pMontage,
            USQGrenadeStaticInfo.WeaponUnderhandPinpull1pMontage,
            USQGrenadeStaticInfo.WeaponUnderhandPinpull3pMontage,
            USQGrenadeStaticInfo.UnderhandPinpull1pMontage,
            USQGrenadeStaticInfo.UnderhandPinpull3pMontage,
            USQGrenadeStaticInfo.WeaponUnderhandThrow1pMontage,
            USQGrenadeStaticInfo.WeaponUnderhandThrow3pMontage,
            USQGrenadeStaticInfo.UnderhandThrow1pMontage,
            USQGrenadeStaticInfo.UnderhandThrow3pMontage,
        };

        private static readonly uint[] ActorMontageOffsets =
        {
            ASQGrenade.ActorWeaponOverhandPinpull1pMontage,
            ASQGrenade.ActorWeaponOverhandPinpull3pMontage,
            ASQGrenade.ActorOverhandPinpull1pMontage,
            ASQGrenade.ActorOverhandPinpull3pMontage,
            ASQGrenade.ActorWeaponOverhandThrow1pMontage,
            ASQGrenade.ActorWeaponOverhandThrow3pMontage,
            ASQGrenade.ActorOverhandThrow1pMontage,
            ASQGrenade.ActorOverhandThrow3pMontage,
            ASQGrenade.ActorWeaponUnderhandPinpull1pMontage,
            ASQGrenade.ActorWeaponUnderhandPinpull3pMontage,
            ASQGrenade.ActorUnderhandPinpull1pMontage,
            ASQGrenade.ActorUnderhandPinpull3pMontage,
            ASQGrenade.ActorWeaponUnderhandThrow1pMontage,
            ASQGrenade.ActorWeaponUnderhandThrow3pMontage,
            ASQGrenade.ActorUnderhandThrow1pMontage,
            ASQGrenade.ActorUnderhandThrow3pMontage,
        };

        public bool IsEnabled => _isEnabled;

        public InstantGrenade(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }

        public void SetEnabled(bool enable)
        {
            if (_isEnabled == enable) return;

            _isEnabled = enable;

            if (!enable)
            {
                RecoverGrenade(_lastAppliedWeapon);
                _lastAppliedWeapon = 0;
            }
            else
            {
                ulong weapon = GetCurrentWeapon();
                if (weapon != 0 && IsGrenade(weapon))
                    ApplyGrenade(weapon);
            }
        }

        public void OnWeaponChanged(ulong newWeapon, ulong oldWeapon)
        {
            if (_isEnabled && oldWeapon != 0)
                RecoverGrenade(oldWeapon);

            ClearOriginals();
            _lastAppliedWeapon = 0;

            if (_isEnabled && newWeapon != 0 && IsGrenade(newWeapon))
                ApplyGrenade(newWeapon);
        }

        public override void Apply()
        {
            if (!_isEnabled) return;

            ulong weapon = _cachedCurrentWeapon != 0 ? _cachedCurrentWeapon : GetCurrentWeapon();
            if (weapon == 0 || !IsGrenade(weapon)) return;

            ApplyGrenade(weapon);
        }

        private static bool IsGrenade(ulong equipableItem)
        {
            if (equipableItem == 0) return false;
            try
            {
                ulong weaponConfig = Memory.ReadPtr(equipableItem + ASQWeapon.WeaponConfig);
                if (weaponConfig != 0) return false;

                ulong grenadeStaticInfo = Memory.ReadPtr(equipableItem + (ulong)ASQGrenade.GrenadeStaticInfo);
                return grenadeStaticInfo != 0;
            }
            catch
            {
                return false;
            }
        }

        private ulong GetCurrentWeapon()
        {
            try
            {
                UpdateCachedPointers();
                if (_cachedCurrentWeapon != 0)
                    return _cachedCurrentWeapon;

                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                if (playerState == 0) return 0;

                ulong soldier = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldier == 0) return 0;

                ulong inventory = Memory.ReadPtr(soldier + ASQSoldier.InventoryComponent);
                if (inventory == 0) return 0;

                return Memory.ReadPtr(inventory + USQPawnInventoryComponent.CurrentWeapon);
            }
            catch
            {
                return 0;
            }
        }

        private void ApplyGrenade(ulong weapon)
        {
            try
            {
                ulong grenadeStaticInfoPtr = Memory.ReadPtr(weapon + (ulong)ASQGrenade.GrenadeStaticInfo);
                if (grenadeStaticInfoPtr == 0) return;

                if (_lastAppliedWeapon != weapon)
                {
                    ClearOriginals();
                    _lastAppliedWeapon = weapon;
                }

                ulong grenadeConfigPtr = weapon + (ulong)ASQGrenade.GrenadeConfig;

                if (_originalGrenadeValues.Count == 0)
                {
                    _originalItemCount = Memory.ReadValue<int>(weapon + (ulong)ASQEquipableItem.ItemCount);
                    _originalMaxItemCount = Memory.ReadValue<int>(weapon + (ulong)ASQEquipableItem.MaxItemCount);
                    foreach (var (offset, _) in GrenadeTimingFields)
                        _originalGrenadeValues[offset] = Memory.ReadValue<float>(grenadeConfigPtr + offset);
                }

                Memory.WriteValue<bool>(grenadeConfigPtr + (ulong)FSQGrenadeData.bInfiniteAmmo, true);
                Memory.WriteValue<int>(weapon + (ulong)ASQEquipableItem.ItemCount, 10);
                Memory.WriteValue<int>(weapon + (ulong)ASQEquipableItem.MaxItemCount, 10);

                var grenadeWrites = GrenadeTimingFields
                    .Select(f => new ScatterWriteDataEntry<float>(grenadeConfigPtr + f.offset, f.value))
                    .ToList();
                Memory.WriteScatter(grenadeWrites);

                ModifyMontageSet(grenadeStaticInfoPtr, StaticInfoMontageOffsets);
                ModifyMontageSet(weapon, ActorMontageOffsets);
            }
            catch { }
        }

        private void ModifyMontageSet(ulong basePtr, uint[] offsets)
        {
            foreach (uint off in offsets)
            {
                try
                {
                    ulong montagePtr = Memory.ReadPtr(basePtr + off);
                    if (montagePtr == 0) continue;

                    if (_originalAnimValues.Count == 0)
                    {
                        foreach (var (offset, _) in MontageFloatFields)
                            _originalAnimValues[offset] = Memory.ReadValue<float>(montagePtr + offset);
                        _originalAnimBoolValues[UAnimMontage.bEnableAutoBlendOut] =
                            Memory.ReadValue<bool>(montagePtr + UAnimMontage.bEnableAutoBlendOut);
                    }

                    var floatWrites = MontageFloatFields
                        .Select(f => new ScatterWriteDataEntry<float>(montagePtr + f.offset, f.value))
                        .ToList();
                    Memory.WriteScatter(floatWrites);
                    Memory.WriteValue<bool>(montagePtr + UAnimMontage.bEnableAutoBlendOut, false);
                }
                catch { }
            }
        }

        private void RecoverGrenade(ulong weapon)
        {
            if (weapon == 0) return;
            try
            {
                ulong grenadeStaticInfoPtr = Memory.ReadPtr(weapon + (ulong)ASQGrenade.GrenadeStaticInfo);
                if (grenadeStaticInfoPtr == 0) return;

                ulong grenadeConfigPtr = weapon + (ulong)ASQGrenade.GrenadeConfig;
                Memory.WriteValue<bool>(grenadeConfigPtr + (ulong)FSQGrenadeData.bInfiniteAmmo, false);
                Memory.WriteValue<byte>(weapon + (ulong)ASQGrenade.GrenadeState, 0);
                Memory.WriteValue<bool>(weapon + (ulong)ASQGrenade.bCachedOverhandThrow, false);
                Memory.WriteValue<bool>(weapon + (ulong)ASQGrenade.bCachedOverhandPinpull, false);
                Memory.WriteValue<bool>(weapon + (ulong)ASQGrenade.bCachedReloading, false);

                if (_originalItemCount != 0)
                    Memory.WriteValue<int>(weapon + (ulong)ASQEquipableItem.ItemCount, _originalItemCount);
                if (_originalMaxItemCount != 0)
                    Memory.WriteValue<int>(weapon + (ulong)ASQEquipableItem.MaxItemCount, _originalMaxItemCount);

                if (_originalGrenadeValues.Count > 0)
                {
                    var restoreWrites = _originalGrenadeValues
                        .Select(kvp => new ScatterWriteDataEntry<float>(grenadeConfigPtr + kvp.Key, kvp.Value))
                        .ToList();
                    Memory.WriteScatter(restoreWrites);
                }

                RestoreMontageSet(grenadeStaticInfoPtr, StaticInfoMontageOffsets);
                RestoreMontageSet(weapon, ActorMontageOffsets);
            }
            catch { }
        }

        private void RestoreMontageSet(ulong basePtr, uint[] offsets)
        {
            foreach (uint off in offsets)
            {
                try
                {
                    ulong montagePtr = Memory.ReadPtr(basePtr + off);
                    if (montagePtr == 0) continue;

                    if (_originalAnimValues.Count > 0)
                    {
                        var restoreWrites = _originalAnimValues
                            .Select(kvp => new ScatterWriteDataEntry<float>(montagePtr + kvp.Key, kvp.Value))
                            .ToList();
                        Memory.WriteScatter(restoreWrites);
                    }

                    if (_originalAnimBoolValues.TryGetValue(UAnimMontage.bEnableAutoBlendOut, out bool origBool))
                        Memory.WriteValue<bool>(montagePtr + UAnimMontage.bEnableAutoBlendOut, origBool);
                }
                catch { }
            }
        }

        private void ClearOriginals()
        {
            _originalGrenadeValues.Clear();
            _originalAnimValues.Clear();
            _originalAnimBoolValues.Clear();
            _originalItemCount = 0;
            _originalMaxItemCount = 0;
        }
    }
}
