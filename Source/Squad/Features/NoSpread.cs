using Offsets;
using squad_dma;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// State-aware NoSpread feature that prevents memory write spam and only applies when player is alive
    /// </summary>
    public class NoSpread : StateAwareFeature, Weapon
    {
        public const string NAME = "NoSpread";
        
        // Original values for animation instance
        private Dictionary<ulong, float> _originalAnimValues = new Dictionary<ulong, float>();
        
        // Original values for weapon static info
        private Dictionary<ulong, float> _originalWeaponValues = new Dictionary<ulong, float>();

        // Config storage for animation values
        private Dictionary<string, float> _configAnimValues = new Dictionary<string, float>();

        // Config storage for weapon values
        private Dictionary<string, float> _configWeaponValues = new Dictionary<string, float>();
        
        private ulong _lastWeapon = 0;
        private ulong _lastAnimInstance = 0;
        private ulong _lastWeaponStaticInfo = 0;
        
        // Animation instance spread entries (24 total entries)
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
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinBipodAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinBipodDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinProneAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinProneDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinCrouchAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinCrouchDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinStandAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinStandDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MinProneTransitionDeviation, 0f),
        };

        // Weapon static info spread entries (18 total entries)
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
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinBipodAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinBipodDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinProneAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinProneDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinCrouchAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinCrouchDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinStandAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinStandDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MinProneTransitionDeviation, 0f),
        };
        
        public NoSpread(ulong playerController, bool inGame, Game game)
            : base(playerController, inGame, game, NAME)
        {
        }
        
        protected override bool ShouldApplyModifications()
        {
            return Program.Config.NoSpread;
        }
        
        protected override void LoadOriginalValues()
        {
            SafeLoadOriginals(() =>
            {
                // Get animation instance
                ulong animInstance = Memory.ReadPtr(_cachedSoldierActor + ASQSoldier.CachedAnimInstance1p);
                if (animInstance == 0) return;
                
                _lastAnimInstance = animInstance;
                
                // Load original animation values
                foreach (var entry in _noSpreadAnimEntries)
                {
                    float originalValue = Memory.ReadValue<float>(animInstance + entry.Address);
                    _originalAnimValues[entry.Address] = originalValue;
                    
                    // Store in config with descriptive key
                    string configKey = $"Anim_{entry.Address:X}";
                    _configAnimValues[configKey] = originalValue;
                }
                
                // Get weapon and weapon static info
                ulong weapon = _cachedCurrentWeapon;
                if (weapon != 0)
                {
                    ulong weaponStaticInfo = GetCachedWeaponStaticInfo(weapon);
                    if (weaponStaticInfo != 0)
                    {
                        _lastWeaponStaticInfo = weaponStaticInfo;
                        
                        // Load original weapon values
                        foreach (var entry in _noSpreadWeaponEntries)
                        {
                            float originalValue = Memory.ReadValue<float>(weaponStaticInfo + entry.Address);
                            _originalWeaponValues[entry.Address] = originalValue;
                            
                            // Store in config with descriptive key
                            string configKey = $"Weapon_{entry.Address:X}";
                            _configWeaponValues[configKey] = originalValue;
                        }
                    }
                }
                
                // Save to config
                SaveOriginalValuesToConfig();
                
                Logger.Debug($"[{_featureName}] Loaded {_originalAnimValues.Count} animation values and {_originalWeaponValues.Count} weapon values");
                
            }, "NoSpread values");
        }
        
        protected override void ApplyModifications()
        {
            SafeApplyModifications(() =>
            {
                // Apply animation modifications
                if (_lastAnimInstance != 0)
                {
                    var animEntries = _noSpreadAnimEntries.Select(entry => 
                        new ScatterWriteDataEntry<float>(_lastAnimInstance + entry.Address, entry.Data)).ToList();
                    Memory.WriteScatter(animEntries);
                    Logger.Debug($"[{_featureName}] Applied {animEntries.Count} animation modifications");
                }
                
                // Apply weapon modifications
                if (_lastWeaponStaticInfo != 0)
                {
                    var weaponEntries = _noSpreadWeaponEntries.Select(entry => 
                        new ScatterWriteDataEntry<float>(_lastWeaponStaticInfo + entry.Address, entry.Data)).ToList();
                    Memory.WriteScatter(weaponEntries);
                    Logger.Debug($"[{_featureName}] Applied {weaponEntries.Count} weapon modifications");
                }
                
            }, "NoSpread modifications (42 memory addresses)");
        }
        
        protected override void RestoreOriginalValues()
        {
            SafeRestoreValues(() =>
            {
                // Restore animation values
                if (_lastAnimInstance != 0 && _originalAnimValues.Count > 0)
                {
                    var animRestoreEntries = _originalAnimValues.Select(kvp => 
                        new ScatterWriteDataEntry<float>(_lastAnimInstance + kvp.Key, kvp.Value)).ToList();
                    Memory.WriteScatter(animRestoreEntries);
                    Logger.Debug($"[{_featureName}] Restored {animRestoreEntries.Count} animation values");
                }
                
                // Restore weapon values
                if (_lastWeaponStaticInfo != 0 && _originalWeaponValues.Count > 0)
                {
                    var weaponRestoreEntries = _originalWeaponValues.Select(kvp => 
                        new ScatterWriteDataEntry<float>(_lastWeaponStaticInfo + kvp.Key, kvp.Value)).ToList();
                    Memory.WriteScatter(weaponRestoreEntries);
                    Logger.Debug($"[{_featureName}] Restored {weaponRestoreEntries.Count} weapon values");
                }
                
            }, "NoSpread original values");
        }
        
        // Weapon interface implementation
        public void OnWeaponChanged(ulong newWeapon, ulong oldWeapon)
        {
            // Reset applied state when weapon changes so modifications are reapplied
            if (newWeapon != _lastWeapon)
            {
                _isApplied = false;
                _lastWeapon = newWeapon;
                Logger.Debug($"[{_featureName}] Weapon changed, will reapply modifications on next update");
            }
        }
        
        private void SaveOriginalValuesToConfig()
        {
            try
            {
                if (Config.TryLoadConfig(out var config))
                {
                    config.OriginalNoSpreadAnimValues = _configAnimValues;
                    config.OriginalNoSpreadWeaponValues = _configWeaponValues;
                    Config.SaveConfig(config);
                    Logger.Debug($"[{_featureName}] Saved original values to config");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{_featureName}] Error saving original values to config: {ex.Message}");
            }
        }
    }
}