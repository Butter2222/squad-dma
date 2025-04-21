using Offsets;
using squad_dma;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class NoSway : Manager, Weapon
    {
        public const string NAME = "NoSway";
                
        private bool _isEnabled = false;
        
        public bool IsEnabled => _isEnabled;
        
        // Original values for animation instance
        private Dictionary<ulong, float> _originalAnimValues = new Dictionary<ulong, float>();
        
        // Original values for weapon static info
        private Dictionary<ulong, float> _originalWeaponValues = new Dictionary<ulong, float>();

        // Config storage for animation values
        private Dictionary<string, float> _configAnimValues = new Dictionary<string, float>();

        // Config storage for weapon values
        private Dictionary<string, float> _configWeaponValues = new Dictionary<string, float>();
        
        private ulong _lastWeapon = 0;
        
        private readonly List<IScatterWriteDataEntry<float>> _noSwayAnimEntries = new List<IScatterWriteDataEntry<float>>
        {
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MoveSwayFactorMultiplier, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.SuppressSwayFactorMultiplier, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.WeaponPunchSwayCombinedRotator, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.WeaponPunchSwayCombinedRotator + 4, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.WeaponPunchSwayCombinedRotator + 8, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.UnclampedTotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.SwayData + FSQSwayData.UnclampedTotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.SwayData + FSQSwayData.TotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.SwayData + FSQSwayData.Sway, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.SwayData + FSQSwayData.Sway + 4, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.SwayData + FSQSwayData.Sway + 8, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.SwayAlignmentData + FSQSwayData.UnclampedTotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.SwayAlignmentData + FSQSwayData.TotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.SwayAlignmentData + FSQSwayData.Sway, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.SwayAlignmentData + FSQSwayData.Sway + 4, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.SwayAlignmentData + FSQSwayData.Sway + 8, 0f),
        };

        private readonly List<IScatterWriteDataEntry<float>> _noSwayWeaponEntries = new List<IScatterWriteDataEntry<float>>
        {
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.AddMoveSway, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MaxMoveSwayFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.SwayData + FSQSwayData.UnclampedTotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.SwayData + FSQSwayData.TotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.SwayData + FSQSwayData.Sway, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.SwayData + FSQSwayData.Sway + 4, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.SwayData + FSQSwayData.Sway + 8, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.SwayAlignmentData + FSQSwayData.UnclampedTotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.SwayAlignmentData + FSQSwayData.TotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.SwayAlignmentData + FSQSwayData.Sway, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.SwayAlignmentData + FSQSwayData.Sway + 4, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.SwayAlignmentData + FSQSwayData.Sway + 8, 0f),
        };

        public NoSway(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }

        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid())
            {
                Logger.Error($"[{NAME}] Cannot enable/disable no sway - local player is not valid");
                return;
            }
            
            _isEnabled = enable;
            Logger.Debug($"[{NAME}] No sway {(enable ? "enabled" : "disabled")}");
            
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
                if (Program.Config.NoSway && newWeapon != 0)
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
                if (_cachedSoldierActor == 0)
                {
                    Logger.Error($"[{NAME}] Cannot restore weapon - soldier actor is not valid");
                    return;
                }

                ulong animInstance = Memory.ReadPtr(_cachedSoldierActor + ASQSoldier.CachedAnimInstance1p);
                if (animInstance == 0)
                {
                    Logger.Error($"[{NAME}] Cannot restore weapon - animation instance is not valid");
                    return;
                }

                ulong weaponStaticInfo = GetCachedWeaponStaticInfo(weapon);
                if (weaponStaticInfo == 0)
                {
                    Logger.Error($"[{NAME}] Cannot restore weapon - weapon static info is not valid");
                    return;
                }

                // Restore original values
                if (_originalAnimValues.Count > 0)
                {
                    var restoreEntries = _originalAnimValues.Select(kvp => 
                        new ScatterWriteDataEntry<float>(animInstance + kvp.Key, kvp.Value)).ToList();
                    Memory.WriteScatter(restoreEntries);
                    Logger.Debug($"[{NAME}] Restored original anim values");
                }

                if (_originalWeaponValues.Count > 0)
                {
                    var restoreEntries = _originalWeaponValues.Select(kvp => 
                        new ScatterWriteDataEntry<float>(weaponStaticInfo + kvp.Key, kvp.Value)).ToList();
                    Memory.WriteScatter(restoreEntries);
                    Logger.Debug($"[{NAME}] Restored original weapon values");
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
                // Use cached pointers
                if (_cachedSoldierActor == 0)
                {
                    Logger.Error($"[{NAME}] Cannot apply no sway - soldier actor is not valid");
                    return;
                }

                // Get anim instance from cached soldier actor
                ulong animInstance = Memory.ReadPtr(_cachedSoldierActor + ASQSoldier.CachedAnimInstance1p);
                if (animInstance == 0)
                {
                    Logger.Error($"[{NAME}] Cannot apply no sway - animation instance is not valid");
                    return;
                }

                // Get weapon static info using cached version if possible
                ulong weaponStaticInfo = GetCachedWeaponStaticInfo(weapon);
                if (weaponStaticInfo == 0)
                {
                    Logger.Error($"[{NAME}] Cannot apply no sway - weapon static info is not valid");
                    return;
                }

                Logger.Debug($"[{NAME}] Applying no sway to anim instance at 0x{animInstance:X}");
                // Apply no sway to anim instance
                if (Program.Config.NoSway)
                {
                    // Store original values when first enabled
                    if (_originalAnimValues.Count == 0)
                    {
                        foreach (var entry in _noSwayAnimEntries)
                        {
                            float originalValue = Memory.ReadValue<float>(animInstance + entry.Address);
                            _originalAnimValues[entry.Address] = originalValue;
                            
                            // Store in config with descriptive key
                            string configKey = $"Anim_{entry.Address:X}";
                            _configAnimValues[configKey] = originalValue;
                            
                            Logger.Debug($"[{NAME}] Stored original anim value at 0x{entry.Address:X}: {originalValue}");
                        }

                        // Save to config
                        if (Config.TryLoadConfig(out var config))
                        {
                            config.OriginalNoSwayAnimValues = _configAnimValues;
                            Config.SaveConfig(config);
                            Logger.Debug($"[{NAME}] Saved original anim values to config");
                        }
                    }

                    var animEntries = _noSwayAnimEntries.Select(entry => 
                        new ScatterWriteDataEntry<float>(animInstance + entry.Address, entry.Data)).ToList();
                    Memory.WriteScatter(animEntries);
                }

                Logger.Debug($"[{NAME}] Applying no sway to weapon static info at 0x{weaponStaticInfo:X}");
                // Apply no sway to weapon static info
                if (Program.Config.NoSway)
                {
                    // Store original values when first enabled
                    if (_originalWeaponValues.Count == 0)
                    {
                        foreach (var entry in _noSwayWeaponEntries)
                        {
                            float originalValue = Memory.ReadValue<float>(weaponStaticInfo + entry.Address);
                            _originalWeaponValues[entry.Address] = originalValue;
                            
                            // Store in config with descriptive key
                            string configKey = $"Weapon_{entry.Address:X}";
                            _configWeaponValues[configKey] = originalValue;
                            
                            Logger.Debug($"[{NAME}] Stored original weapon value at 0x{entry.Address:X}: {originalValue}");
                        }

                        // Save to config
                        if (Config.TryLoadConfig(out var config))
                        {
                            config.OriginalNoSwayWeaponValues = _configWeaponValues;
                            Config.SaveConfig(config);
                            Logger.Debug($"[{NAME}] Saved original weapon values to config");
                        }
                    }

                    var weaponEntries = _noSwayWeaponEntries.Select(entry => 
                        new ScatterWriteDataEntry<float>(weaponStaticInfo + entry.Address, entry.Data)).ToList();
                    Memory.WriteScatter(weaponEntries);
                }

                Logger.Debug($"[{NAME}] Successfully {(Program.Config.NoSway ? "enabled" : "disabled")} no sway");
                Logger.Debug("=============================");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error applying no sway to weapon at 0x{weapon:X}: {ex.Message}");
            }
        }
        
        // Override Apply to do nothing since we handle weapon changes in OnWeaponChanged
        public override void Apply() { }
    }
} 