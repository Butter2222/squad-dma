using Offsets;
using squad_dma.Source.Squad;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class InstantGrenade : Weapon
    {
        public const string NAME = "InstantGrenade";
        private readonly ulong _playerController;
        private readonly bool _inGame;
        private bool _isEnabled;
        private ulong _currentWeapon;
        private readonly Config _config;

        private Dictionary<ulong, float> _originalGrenadeValues = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> _originalAnimValues = new Dictionary<ulong, float>();

        private Dictionary<string, float> _configGrenadeValues = new Dictionary<string, float>();
        private Dictionary<string, float> _configAnimValues = new Dictionary<string, float>();
        private Dictionary<string, byte> _configItemValues = new Dictionary<string, byte>();

        private byte _originalItemCount;
        private byte _originalMaxItemCount;

        private readonly List<IScatterWriteDataEntry<float>> _instantGrenadeEntries = new List<IScatterWriteDataEntry<float>>
        {
            new ScatterWriteDataEntry<float>(0 + FSQGrenadeData.ThrowReadyTime, 0.0f),
            new ScatterWriteDataEntry<float>(0 + FSQGrenadeData.OverhandThrowTime, 0.0f),
            new ScatterWriteDataEntry<float>(0 + FSQGrenadeData.UnderhandThrowTime, 0.0f),
            new ScatterWriteDataEntry<float>(0 + FSQGrenadeData.OverhandThrowDuration, 0.0f),
            new ScatterWriteDataEntry<float>(0 + FSQGrenadeData.UnderhandThrowDuration, 0.0f),
            new ScatterWriteDataEntry<float>(0 + FSQGrenadeData.ThrowModeTransitionTime, 0.0f),
            new ScatterWriteDataEntry<float>(0 + FSQGrenadeData.ReloadTime, 0.0f)
        };

        private readonly List<IScatterWriteDataEntry<float>> _instantAnimEntries = new List<IScatterWriteDataEntry<float>>
        {
            new ScatterWriteDataEntry<float>(0 + UAnimSequenceBase.SequenceLength, 0.0f),
            new ScatterWriteDataEntry<float>(0 + UAnimSequenceBase.RateScale, 1000.0f),
            new ScatterWriteDataEntry<float>(0 + UAnimMontage.BlendInTime, 0.0f),
            new ScatterWriteDataEntry<float>(0 + UAnimMontage.blendOutTime, 0.0f),
            new ScatterWriteDataEntry<float>(0 + UAnimMontage.BlendOutTriggerTime, 0.0f)
        };

        private readonly List<IScatterWriteDataEntry<bool>> _instantAnimBoolEntries = new List<IScatterWriteDataEntry<bool>>
        {
            new ScatterWriteDataEntry<bool>(0 + UAnimMontage.bEnableAutoBlendOut, false)
        };

        public bool IsEnabled => _isEnabled;

        public InstantGrenade(ulong playerController, bool inGame)
        {
            _playerController = playerController;
            _inGame = inGame;
            _isEnabled = false;
            _currentWeapon = 0;
            _config = Program.Config;
        }

        public void SetEnabled(bool enable)
        {
            if (_isEnabled == enable) return;
            _isEnabled = enable;
            
            Logger.Debug($"[{NAME}] {(enable ? "Enabled" : "Disabled")} instant grenade");
            
            if (enable)
            {
                // Get current weapon through player state -> soldier -> inventory
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
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"[{NAME}] Error getting current weapon: {ex.Message}");
                }
                
                Apply();
            }
            else
            {
                Recover();
            }
        }

        public void OnWeaponChanged(ulong newWeapon, ulong oldWeapon)
        {
            if (oldWeapon != 0 && _isEnabled)
            {
                Recover();
            }
            
            _currentWeapon = newWeapon;
            if (_isEnabled)
            {
                Apply();
            }
        }

        public void Apply()
        {
            try
            {
                if (!_inGame || _playerController == 0 || _currentWeapon == 0) return;

                // Verify it's actually a grenade by checking the class name
                string weaponClassName = Memory.GetActorClassName(_currentWeapon);
                if (!weaponClassName.Contains("Grenade") && 
                    !weaponClassName.Contains("Frag") && 
                    !weaponClassName.Contains("Smoke") && 
                    !weaponClassName.Contains("Flash"))
                {
                    return;
                }

                Logger.Debug($"[{NAME}] Applying instant grenade to {weaponClassName} at 0x{_currentWeapon:X}");

                var grenadeConfigPtr = _currentWeapon + (ulong)ASQGrenade.GrenadeConfig;
                if (grenadeConfigPtr == 0) return;

                if (_originalItemCount == 0 && _originalMaxItemCount == 0)
                {
                    _originalItemCount = Memory.ReadValue<byte>(_currentWeapon + (ulong)ASQEquipableItem.ItemCount);
                    _originalMaxItemCount = Memory.ReadValue<byte>(_currentWeapon + (ulong)ASQEquipableItem.MaxItemCount);
                    
                    _configItemValues["ItemCount"] = _originalItemCount;
                    _configItemValues["MaxItemCount"] = _originalMaxItemCount;
                    
                    Logger.Debug($"[{NAME}] Stored original item count: {_originalItemCount}, max item count: {_originalMaxItemCount}");
                    
                    if (Config.TryLoadConfig(out var config))
                    {
                        config.OriginalGrenadeItemValues = _configItemValues;
                        Config.SaveConfig(config);
                        Logger.Debug($"[{NAME}] Saved original item values to config");
                    }
                }

                Memory.WriteValue<byte>(_currentWeapon + (ulong)ASQEquipableItem.ItemCount, 10);
                Memory.WriteValue<byte>(_currentWeapon + (ulong)ASQEquipableItem.MaxItemCount, 10);
                Memory.WriteValue<bool>(grenadeConfigPtr + (ulong)FSQGrenadeData.bInfiniteAmmo, true);

                if (_originalGrenadeValues.Count == 0)
                {
                    foreach (var entry in _instantGrenadeEntries)
                    {
                        float originalValue = Memory.ReadValue<float>(grenadeConfigPtr + entry.Address);
                        _originalGrenadeValues[entry.Address] = originalValue;
                        
                        string configKey = $"Grenade_{entry.Address:X}";
                        _configGrenadeValues[configKey] = originalValue;
                        
                        Logger.Debug($"[{NAME}] Stored original grenade value at 0x{entry.Address:X}: {originalValue}");
                    }

                    if (Config.TryLoadConfig(out var config))
                    {
                        config.OriginalGrenadeValues = _configGrenadeValues;
                        Config.SaveConfig(config);
                        Logger.Debug($"[{NAME}] Saved original grenade values to config");
                    }
                }

                var grenadeEntries = _instantGrenadeEntries.Select(entry => 
                    new ScatterWriteDataEntry<float>(grenadeConfigPtr + entry.Address, entry.Data)).ToList();
                Memory.WriteScatter(grenadeEntries);

                var grenadeStaticInfoPtr = Memory.ReadPtr(_currentWeapon + (ulong)ASQGrenade.GrenadeStaticInfo);
                if (grenadeStaticInfoPtr != 0)
                {
                    // Modify all animation montages to be instant
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponOverhandPinpull1pMontage);
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponOverhandPinpull3pMontage);
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.OverhandPinpull1pMontage);
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.OverhandPinpull3pMontage);
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponOverhandThrow1pMontage);
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponOverhandThrow3pMontage);
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.OverhandThrow1pMontage);
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.OverhandThrow3pMontage);
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponUnderhandPinpull1pMontage);
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponUnderhandPinpull3pMontage);
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.UnderhandPinpull1pMontage);
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.UnderhandPinpull3pMontage);
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponUnderhandThrow1pMontage);
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponUnderhandThrow3pMontage);
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.UnderhandThrow1pMontage);
                    ModifyAnimationMontage(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.UnderhandThrow3pMontage);
                }

                Logger.Debug($"[{NAME}] Successfully applied instant grenade modifications");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error applying instant grenade: {ex.Message}");
            }
        }

        private void ModifyAnimationMontage(ulong montagePtr)
        {
            if (montagePtr == 0) return;

            try
            {
                if (_originalAnimValues.Count == 0)
                {
                    foreach (var entry in _instantAnimEntries)
                    {
                        float originalValue = Memory.ReadValue<float>(montagePtr + entry.Address);
                        _originalAnimValues[entry.Address] = originalValue;
                        
                        string configKey = $"Anim_{entry.Address:X}";
                        _configAnimValues[configKey] = originalValue;
                        
                        Logger.Debug($"[{NAME}] Stored original anim value at 0x{entry.Address:X}: {originalValue}");
                    }

                    if (Config.TryLoadConfig(out var config))
                    {
                        config.OriginalGrenadeAnimValues = _configAnimValues;
                        Config.SaveConfig(config);
                        Logger.Debug($"[{NAME}] Saved original anim values to config");
                    }
                }

                var animEntries = _instantAnimEntries.Select(entry => 
                    new ScatterWriteDataEntry<float>(montagePtr + entry.Address, entry.Data)).ToList();
                var boolEntries = _instantAnimBoolEntries.Select(entry => 
                    new ScatterWriteDataEntry<bool>(montagePtr + entry.Address, entry.Data)).ToList();
                
                Memory.WriteScatter(animEntries);
                Memory.WriteScatter(boolEntries);
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error modifying animation montage: {ex.Message}");
            }
        }

        private void Recover()
        {
            try
            {
                if (!_inGame || _playerController == 0 || _currentWeapon == 0) return;

                string weaponClassName = Memory.GetActorClassName(_currentWeapon);
                
                // Only proceed if it's actually a grenade
                if (!weaponClassName.Contains("Grenade") && 
                    !weaponClassName.Contains("Frag") && 
                    !weaponClassName.Contains("Smoke") && 
                    !weaponClassName.Contains("Flash"))
                {
                    return;
                }

                Logger.Debug($"[{NAME}] Recovering original values for {weaponClassName}");

                if (_originalItemCount != 0 && _originalMaxItemCount != 0)
                {
                    Memory.WriteValue<byte>(_currentWeapon + (ulong)ASQEquipableItem.ItemCount, _originalItemCount);
                    Memory.WriteValue<byte>(_currentWeapon + (ulong)ASQEquipableItem.MaxItemCount, _originalMaxItemCount);
                    Logger.Debug($"[{NAME}] Restored original item count: {_originalItemCount}, max item count: {_originalMaxItemCount}");
                }

                var grenadeConfigPtr = _currentWeapon + (ulong)ASQGrenade.GrenadeConfig;
                if (grenadeConfigPtr != 0)
                {
                    if (_originalGrenadeValues.Count > 0)
                    {
                        var restoreEntries = _originalGrenadeValues.Select(kvp => 
                            new ScatterWriteDataEntry<float>(grenadeConfigPtr + kvp.Key, kvp.Value)).ToList();
                        Memory.WriteScatter(restoreEntries);
                        Logger.Debug($"[{NAME}] Restored original grenade values");
                    }

                    Memory.WriteValue<bool>(grenadeConfigPtr + (ulong)FSQGrenadeData.bInfiniteAmmo, false);
                }

                // Restore animation montage values
                var grenadeStaticInfoPtr = Memory.ReadPtr(_currentWeapon + (ulong)ASQGrenade.GrenadeStaticInfo);
                if (grenadeStaticInfoPtr != 0)
                {
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponOverhandPinpull1pMontage);
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponOverhandPinpull3pMontage);
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.OverhandPinpull1pMontage);
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.OverhandPinpull3pMontage);
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponOverhandThrow1pMontage);
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponOverhandThrow3pMontage);
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.OverhandThrow1pMontage);
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.OverhandThrow3pMontage);
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponUnderhandPinpull1pMontage);
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponUnderhandPinpull3pMontage);
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.UnderhandPinpull1pMontage);
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.UnderhandPinpull3pMontage);
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponUnderhandThrow1pMontage);
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.WeaponUnderhandThrow3pMontage);
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.UnderhandThrow1pMontage);
                    RestoreAnimationMontageValues(grenadeStaticInfoPtr + (ulong)USQGrenadeStaticInfo.UnderhandThrow3pMontage);
                }

                Logger.Debug($"[{NAME}] Successfully recovered original values for {weaponClassName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error recovering original values: {ex.Message}");
            }
        }

        private void RestoreAnimationMontageValues(ulong montagePtr)
        {
            if (montagePtr == 0) return;

            try
            {
                if (_originalAnimValues.Count > 0)
                {
                    var restoreEntries = _originalAnimValues.Select(kvp => 
                        new ScatterWriteDataEntry<float>(montagePtr + kvp.Key, kvp.Value)).ToList();
                    var boolEntries = _instantAnimBoolEntries.Select(entry => 
                        new ScatterWriteDataEntry<bool>(montagePtr + entry.Address, true)).ToList();
                    
                    Memory.WriteScatter(restoreEntries);
                    Memory.WriteScatter(boolEntries);
                    Logger.Debug($"[{NAME}] Restored original anim values at 0x{montagePtr:X}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error restoring animation montage values: {ex.Message}");
            }
        }
    }
} 