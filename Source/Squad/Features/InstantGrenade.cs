using Offsets;
using squad_dma.Source.Squad;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// Removes the pin-pull / throw delays of grenades and keeps a full stack.
    ///
    /// The grenade timings live on the grenade actor itself (FSQGrenadeData) and are copied from the static info
    /// when it spawns, so they are written once per grenade. The montages are shared assets: the same
    /// UAnimMontage is referenced from the static info and from the actor, and from every grenade of that type, so
    /// each one is changed once and its own original values are kept for the restore.
    /// Only the item count needs attention afterwards, since the game lowers it on every throw.
    ///
    /// Everything is built into one scatter write. The old version issued around a hundred separate DMA
    /// operations on every feature tick, which starved the other features and left their writes late.
    /// </summary>
    public class InstantGrenade : Manager, Weapon
    {
        public const string NAME = "InstantGrenade";

        private const int MaintainIntervalMs = 250;
        private const int StackSize = 10;

        private bool _isEnabled = false;
        private ulong _appliedGrenade = 0;
        private long _nextMaintain = 0;
        private readonly object _sync = new();

        // Per grenade actor: values that were there before the first write
        private int _originalItemCount;
        private int _originalMaxItemCount;
        private readonly Dictionary<uint, float> _originalGrenadeValues = new();

        // Per montage asset (shared), keyed by its address
        private readonly Dictionary<ulong, MontageOriginal> _montages = new();

        private sealed class MontageOriginal
        {
            public float[] Floats;
            public bool AutoBlendOut;
        }

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
            lock (_sync)
            {
                if (_isEnabled == enable) return;
                _isEnabled = enable;

                if (!enable)
                {
                    Recover(_appliedGrenade);
                    _appliedGrenade = 0;
                    return;
                }

                ulong weapon = GetCurrentWeapon();
                if (weapon != 0 && IsGrenade(weapon))
                    ApplyGrenade(weapon);
            }
        }

        public void OnWeaponChanged(ulong newWeapon, ulong oldWeapon)
        {
            lock (_sync)
            {
                // Only the grenade this feature changed is touched on the way out. The old weapon is usually
                // a gun, and writing grenade offsets into a gun corrupts it.
                if (_appliedGrenade != 0 && _appliedGrenade != newWeapon)
                {
                    Recover(_appliedGrenade);
                    _appliedGrenade = 0;
                }

                if (_isEnabled && newWeapon != 0 && IsGrenade(newWeapon))
                    ApplyGrenade(newWeapon);
            }
        }

        public override void Apply()
        {
            lock (_sync)
            {
                if (!_isEnabled) return;

                ulong weapon = _cachedCurrentWeapon != 0 ? _cachedCurrentWeapon : GetCurrentWeapon();
                if (weapon == 0 || !IsGrenade(weapon))
                {
                    // A gun is in hand now; whatever grenade was changed before is restored
                    if (_appliedGrenade != 0)
                    {
                        Recover(_appliedGrenade);
                        _appliedGrenade = 0;
                    }
                    return;
                }

                if (weapon != _appliedGrenade)
                {
                    ApplyGrenade(weapon);
                    return;
                }

                Maintain(weapon);
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

        /// <summary>
        /// Keeps the stack full. Runs a few times a second and costs one read while nothing has changed.
        /// </summary>
        private void Maintain(ulong weapon)
        {
            long now = Environment.TickCount64;
            if (now < _nextMaintain) return;
            _nextMaintain = now + MaintainIntervalMs;

            try
            {
                if (Memory.ReadValue<int>(weapon + ASQEquipableItem.ItemCount) == StackSize)
                    return;

                Memory.WriteValue<int>(weapon + ASQEquipableItem.ItemCount, StackSize);
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Maintain failed: {ex.Message}");
            }
        }

        private void ApplyGrenade(ulong weapon)
        {
            try
            {
                ulong staticInfo = Memory.ReadPtr(weapon + ASQGrenade.GrenadeStaticInfo);
                if (staticInfo == 0) return;

                ulong config = weapon + ASQGrenade.GrenadeConfig;

                _originalGrenadeValues.Clear();
                _originalItemCount = Memory.ReadValue<int>(weapon + ASQEquipableItem.ItemCount);
                _originalMaxItemCount = Memory.ReadValue<int>(weapon + ASQEquipableItem.MaxItemCount);
                foreach (var (offset, _) in GrenadeTimingFields)
                    _originalGrenadeValues[offset] = Memory.ReadValue<float>(config + offset);

                var montages = ResolveMontages(weapon, staticInfo);
                ReadMontageOriginals(montages);

                var writes = new List<IScatterWriteEntry>(32 + montages.Count * 6);
                writes.Add(new ScatterWriteDataEntry<bool>(config + FSQGrenadeData.bInfiniteAmmo, true));
                writes.Add(new ScatterWriteDataEntry<int>(weapon + ASQEquipableItem.ItemCount, StackSize));
                writes.Add(new ScatterWriteDataEntry<int>(weapon + ASQEquipableItem.MaxItemCount, StackSize));
                foreach (var (offset, value) in GrenadeTimingFields)
                    writes.Add(new ScatterWriteDataEntry<float>(config + offset, value));

                foreach (ulong montage in montages)
                {
                    if (!_montages.ContainsKey(montage)) continue; // failed the sanity check, left alone
                    foreach (var (offset, value) in MontageFloatFields)
                        writes.Add(new ScatterWriteDataEntry<float>(montage + offset, value));
                    writes.Add(new ScatterWriteDataEntry<bool>(montage + UAnimMontage.bEnableAutoBlendOut, false));
                }

                Memory.WriteScatter(writes);

                _appliedGrenade = weapon;
                _nextMaintain = Environment.TickCount64 + MaintainIntervalMs;
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Apply failed for 0x{weapon:X}: {ex.Message}");
            }
        }

        /// <summary>
        /// Distinct montage assets of this grenade. The static info and the actor point at the same assets, so
        /// the 32 slots usually boil down to a handful.
        /// </summary>
        private static List<ulong> ResolveMontages(ulong weapon, ulong staticInfo)
        {
            int total = StaticInfoMontageOffsets.Length + ActorMontageOffsets.Length;
            var map = new ScatterReadMap(total);
            var round = map.AddRound();
            int i = 0;
            foreach (uint off in StaticInfoMontageOffsets)
                { round.AddEntry<ulong>(i, 0, staticInfo + off); i++; }
            foreach (uint off in ActorMontageOffsets)
                { round.AddEntry<ulong>(i, 0, weapon + off); i++; }
            map.Execute();

            var result = new List<ulong>(total);
            var seen = new HashSet<ulong>();
            for (int n = 0; n < total; n++)
            {
                if (map.Results[n].TryGetValue(0, out var r) && r.TryGetResult<ulong>(out var ptr) && ptr != 0 && seen.Add(ptr))
                    result.Add(ptr);
            }
            return result;
        }

        /// <summary>
        /// Remembers the original values of each montage that isn't known yet. A montage that doesn't look like one
        /// (no sane length or rate) is skipped, so a bad pointer never gets floats written into it.
        /// </summary>
        private void ReadMontageOriginals(List<ulong> montages)
        {
            var fresh = montages.Where(m => !_montages.ContainsKey(m)).ToList();
            if (fresh.Count == 0) return;

            int perMontage = MontageFloatFields.Length + 1;
            var map = new ScatterReadMap(fresh.Count);
            var round = map.AddRound();
            for (int i = 0; i < fresh.Count; i++)
            {
                for (int f = 0; f < MontageFloatFields.Length; f++)
                    round.AddEntry<float>(i, f, fresh[i] + MontageFloatFields[f].offset);
                round.AddEntry<bool>(i, MontageFloatFields.Length, fresh[i] + UAnimMontage.bEnableAutoBlendOut);
            }
            map.Execute();

            for (int i = 0; i < fresh.Count; i++)
            {
                var r = map.Results[i];
                var floats = new float[MontageFloatFields.Length];
                bool ok = true;
                for (int f = 0; f < floats.Length && ok; f++)
                    ok = r.TryGetValue(f, out var fr) && fr.TryGetResult<float>(out floats[f]);
                if (!ok || !r.TryGetValue(perMontage - 1, out var br) || !br.TryGetResult<bool>(out bool autoBlend))
                    continue;

                float length = floats[0], rate = floats[1];
                bool alreadyModified = length == 0f && rate == MontageFloatFields[1].value;
                if (alreadyModified || float.IsNaN(length) || length < 0f || length > 120f || float.IsNaN(rate) || rate <= 0f || rate > 100f)
                    continue;

                _montages[fresh[i]] = new MontageOriginal { Floats = floats, AutoBlendOut = autoBlend };
            }
        }

        private void Recover(ulong weapon)
        {
            if (weapon == 0) return;

            try
            {
                var writes = new List<IScatterWriteEntry>(32 + _montages.Count * 6);

                // The grenade actor may be gone (used up or destroyed), so it's only written to while it is still a grenade
                if (IsGrenade(weapon))
                {
                    ulong config = weapon + ASQGrenade.GrenadeConfig;
                    writes.Add(new ScatterWriteDataEntry<bool>(config + FSQGrenadeData.bInfiniteAmmo, false));
                    writes.Add(new ScatterWriteDataEntry<byte>(weapon + ASQGrenade.GrenadeState, 0));
                    writes.Add(new ScatterWriteDataEntry<bool>(weapon + ASQGrenade.bCachedOverhandThrow, false));
                    writes.Add(new ScatterWriteDataEntry<bool>(weapon + ASQGrenade.bCachedOverhandPinpull, false));
                    writes.Add(new ScatterWriteDataEntry<bool>(weapon + ASQGrenade.bCachedUnderhandThrow, false));
                    writes.Add(new ScatterWriteDataEntry<bool>(weapon + ASQGrenade.bCachedUnderhandPinpull, false));
                    writes.Add(new ScatterWriteDataEntry<bool>(weapon + ASQGrenade.bCachedReloading, false));

                    if (_originalItemCount > 0)
                        writes.Add(new ScatterWriteDataEntry<int>(weapon + ASQEquipableItem.ItemCount, _originalItemCount));
                    if (_originalMaxItemCount > 0)
                        writes.Add(new ScatterWriteDataEntry<int>(weapon + ASQEquipableItem.MaxItemCount, _originalMaxItemCount));

                    foreach (var kvp in _originalGrenadeValues)
                        writes.Add(new ScatterWriteDataEntry<float>(config + kvp.Key, kvp.Value));
                }

                // Montages are shared assets that outlive the grenade, so they always go back
                foreach (var (montage, original) in _montages)
                {
                    for (int f = 0; f < MontageFloatFields.Length; f++)
                        writes.Add(new ScatterWriteDataEntry<float>(montage + MontageFloatFields[f].offset, original.Floats[f]));
                    writes.Add(new ScatterWriteDataEntry<bool>(montage + UAnimMontage.bEnableAutoBlendOut, original.AutoBlendOut));
                }

                if (writes.Count > 0)
                    Memory.WriteScatter(writes);

                _originalGrenadeValues.Clear();
                _montages.Clear();
                _originalItemCount = 0;
                _originalMaxItemCount = 0;
                Logger.Debug($"[{NAME}] Restored grenade at 0x{weapon:X}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Restore failed for 0x{weapon:X}: {ex.Message}");
            }
        }
    }
}
