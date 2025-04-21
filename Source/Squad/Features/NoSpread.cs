using Offsets;
using squad_dma;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class NoSpread : Manager, Weapon
    {
        public const string NAME = "NoSpread";
                
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
        
        private readonly List<IScatterWriteDataEntry<float>> _noSpreadAnimEntries = new List<IScatterWriteDataEntry<float>>
        {
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MoveDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.ShotDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.FinalDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.FinalDeviation + 4, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.FinalDeviation + 8, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.FinalDeviation + 12, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.AddMoveDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MoveDeviationFactorRelease, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MaxMoveDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinMoveDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.FullStaminaDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.LowStaminaDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.AddShotDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.AddShotDeviationFactorAds, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.ShotDeviationFactorRelease, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinShotDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MaxShotDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinProneAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinProneDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinCrouchAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinCrouchDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinStandAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinStandDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinProneTransitionDeviation, 0f),
        };

        private readonly List<IScatterWriteDataEntry<float>> _noSpreadWeaponEntries = new List<IScatterWriteDataEntry<float>>
        {
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinShotDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MaxShotDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.AddShotDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.AddShotDeviationFactorAds, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ShotDeviationFactorRelease, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.LowStaminaDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.FullStaminaDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MoveDeviationFactorRelease, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.AddMoveDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MaxMoveDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinMoveDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinProneAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinProneDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinCrouchAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinCrouchDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinStandAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinStandDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinProneTransitionDeviation, 0f),
        };

        public NoSpread(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }

        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid())
            {
                Logger.Error($"[{NAME}] Cannot enable/disable no spread - local player is not valid");
                return;
            }
            
            _isEnabled = enable;
            Logger.Debug($"[{NAME}] No spread {(enable ? "enabled" : "disabled")}");
            
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
                if (Program.Config.NoSpread && newWeapon != 0)
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
                if (_cachedSoldierActor == 0)
                {
                    Logger.Error($"[{NAME}] Cannot apply no spread - soldier actor is not valid");
                    return;
                }

                ulong animInstance = Memory.ReadPtr(_cachedSoldierActor + ASQSoldier.CachedAnimInstance1p);
                if (animInstance == 0)
                {
                    Logger.Error($"[{NAME}] Cannot apply no spread - animation instance is not valid");
                    return;
                }

                ulong weaponStaticInfo = GetCachedWeaponStaticInfo(weapon);
                if (weaponStaticInfo == 0)
                {
                    Logger.Error($"[{NAME}] Cannot apply no spread - weapon static info is not valid");
                    return;
                }

                Logger.Debug($"[{NAME}] Applying no spread to anim instance at 0x{animInstance:X}");
                // Apply no spread to anim instance
                if (Program.Config.NoSpread)
                {
                    // Store original values when first enabled
                    if (_originalAnimValues.Count == 0)
                    {
                        foreach (var entry in _noSpreadAnimEntries)
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
                            config.OriginalNoSpreadAnimValues = _configAnimValues;
                            Config.SaveConfig(config);
                            Logger.Debug($"[{NAME}] Saved original anim values to config");
                        }
                    }

                    var animEntries = _noSpreadAnimEntries.Select(entry => 
                        new ScatterWriteDataEntry<float>(animInstance + entry.Address, entry.Data)).ToList();
                    Memory.WriteScatter(animEntries);
                }

                Logger.Debug($"[{NAME}] Applying no spread to weapon static info at 0x{weaponStaticInfo:X}");
                // Apply no spread to weapon static info
                if (Program.Config.NoSpread)
                {
                    // Store original values when first enabled
                    if (_originalWeaponValues.Count == 0)
                    {
                        foreach (var entry in _noSpreadWeaponEntries)
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
                            config.OriginalNoSpreadWeaponValues = _configWeaponValues;
                            Config.SaveConfig(config);
                            Logger.Debug($"[{NAME}] Saved original weapon values to config");
                        }
                    }

                    var weaponEntries = _noSpreadWeaponEntries.Select(entry => 
                        new ScatterWriteDataEntry<float>(weaponStaticInfo + entry.Address, entry.Data)).ToList();
                    Memory.WriteScatter(weaponEntries);
                }

                Logger.Debug($"[{NAME}] Successfully {(Program.Config.NoSpread ? "enabled" : "disabled")} no spread");
                Logger.Debug("=============================");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error applying no spread to weapon at 0x{weapon:X}: {ex.Message}");
            }
        }
        
        // Override Apply to do nothing since we handle weapon changes in OnWeaponChanged
        public override void Apply() { }
    }
} 