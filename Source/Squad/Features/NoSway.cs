using Offsets;
using squad_dma;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// State-aware NoSway feature that prevents memory write spam and only applies when player is alive
    /// </summary>
    public class NoSway : StateAwareFeature, Weapon
    {
        public const string NAME = "NoSway";
        
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
        
        // Animation instance sway entries (16 total entries)
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

        // Weapon static info sway entries (12 total entries)
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
        
        public NoSway(ulong playerController, bool inGame, Game game)
            : base(playerController, inGame, game, NAME)
        {
        }
        
        protected override bool ShouldApplyModifications()
        {
            return Program.Config.NoSway;
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
                foreach (var entry in _noSwayAnimEntries)
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
                        foreach (var entry in _noSwayWeaponEntries)
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
                
            }, "NoSway values");
        }
        
        protected override void ApplyModifications()
        {
            SafeApplyModifications(() =>
            {
                // Apply animation modifications
                if (_lastAnimInstance != 0)
                {
                    var animEntries = _noSwayAnimEntries.Select(entry => 
                        new ScatterWriteDataEntry<float>(_lastAnimInstance + entry.Address, entry.Data)).ToList();
                    Memory.WriteScatter(animEntries);
                    Logger.Debug($"[{_featureName}] Applied {animEntries.Count} animation modifications");
                }
                
                // Apply weapon modifications
                if (_lastWeaponStaticInfo != 0)
                {
                    var weaponEntries = _noSwayWeaponEntries.Select(entry => 
                        new ScatterWriteDataEntry<float>(_lastWeaponStaticInfo + entry.Address, entry.Data)).ToList();
                    Memory.WriteScatter(weaponEntries);
                    Logger.Debug($"[{_featureName}] Applied {weaponEntries.Count} weapon modifications");
                }
                
            }, "NoSway modifications (28 memory addresses)");
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
                
            }, "NoSway original values");
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
                    config.OriginalNoSwayAnimValues = _configAnimValues;
                    config.OriginalNoSwayWeaponValues = _configWeaponValues;
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