using Offsets;
using squad_dma;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// State-aware NoRecoil feature that prevents memory write spam and only applies when player is alive
    /// </summary>
    public class NoRecoil : StateAwareFeature, Weapon
    {
        public const string NAME = "NoRecoil";
        
        // Original values for animation instance
        private Dictionary<ulong, float> _originalAnimValues = new Dictionary<ulong, float>();
        
        // Original values for weapon static info
        private Dictionary<ulong, float> _originalWeaponValues = new Dictionary<ulong, float>();
        
        // Original camera recoil state
        private bool _originalCameraRecoil = true;

        // Config storage for animation values
        private Dictionary<string, float> _configAnimValues = new Dictionary<string, float>();

        // Config storage for weapon values
        private Dictionary<string, float> _configWeaponValues = new Dictionary<string, float>();
        
        private ulong _lastWeapon = 0;
        private ulong _lastAnimInstance = 0;
        private ulong _lastWeaponStaticInfo = 0;
        
        // Animation instance recoil entries (39 total entries)
        private readonly List<IScatterWriteDataEntry<float>> _noRecoilAnimEntries = new List<IScatterWriteDataEntry<float>>
        {
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.WeapRecoilRelLoc, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.WeapRecoilRelLoc + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.WeapRecoilRelLoc + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.MoveRecoilFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.RecoilCanRelease, 0f),
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.FinalRecoilSigma, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.FinalRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.FinalRecoilSigma + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.FinalRecoilMean, 0f),      // X
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.FinalRecoilMean + 4, 0f),  // Y
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.FinalRecoilMean + 8, 0f),  // Z
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.StandRecoilMean, 0f),      // X
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.StandRecoilMean + 4, 0f),  // Y
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.StandRecoilMean + 8, 0f),  // Z
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.StandRecoilSigma, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.StandRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.StandRecoilSigma + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.CrouchRecoilMean, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.CrouchRecoilMean + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.CrouchRecoilMean + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.CrouchRecoilSigma, 0f),    // X
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.CrouchRecoilSigma + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.CrouchRecoilSigma + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.ProneRecoilMean, 0f),      // X
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.ProneRecoilMean + 4, 0f),  // Y
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.ProneRecoilMean + 8, 0f),  // Z
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.ProneRecoilSigma, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.ProneRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.ProneRecoilSigma + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.ProneTransitionRecoilMean, 0f), // X
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.ProneTransitionRecoilMean + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.ProneTransitionRecoilMean + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.ProneTransitionRecoilSigma, 0f), // X
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.ProneTransitionRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.ProneTransitionRecoilSigma + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.WeaponPunch, 0f),          // Pitch
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.WeaponPunch + 4, 0f),      // Yaw
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.WeaponPunch + 8, 0f),      // Roll
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.BipodRecoilMean, 0f),      // X
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.BipodRecoilMean + 4, 0f),  // Y
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.BipodRecoilMean + 8, 0f),  // Z
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.BipodRecoilSigma, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.BipodRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + USQAnimInstanceSoldier1P.BipodRecoilSigma + 8, 0f), // Z
        };

        // Weapon static info recoil entries (30+ entries)
        private readonly List<IScatterWriteDataEntry<float>> _noRecoilWeaponEntries = new List<IScatterWriteDataEntry<float>>
        {
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.RecoilCameraOffsetFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.RecoilWeaponRelLocFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.AddMoveRecoil, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.MaxMoveRecoilFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.StandRecoilMean, 0f),      // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.StandRecoilMean + 4, 0f),  // Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.StandRecoilMean + 8, 0f),  // Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.StandRecoilSigma, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.StandRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.StandRecoilSigma + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.StandAdsRecoilMean, 0f),   // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.StandAdsRecoilMean + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.StandAdsRecoilMean + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.StandAdsRecoilSigma, 0f),  // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.StandAdsRecoilSigma + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.StandAdsRecoilSigma + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.CrouchRecoilMean, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.CrouchRecoilMean + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.CrouchRecoilMean + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.CrouchRecoilSigma, 0f),    // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.CrouchRecoilSigma + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.CrouchRecoilSigma + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.CrouchAdsRecoilMean, 0f),  // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.CrouchAdsRecoilMean + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.CrouchAdsRecoilMean + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.CrouchAdsRecoilSigma, 0f), // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.CrouchAdsRecoilSigma + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.CrouchAdsRecoilSigma + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneRecoilMean, 0f),      // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneRecoilMean + 4, 0f),  // Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneRecoilMean + 8, 0f),  // Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneRecoilSigma, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneRecoilSigma + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneAdsRecoilMean, 0f),   // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneAdsRecoilMean + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneAdsRecoilMean + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneAdsRecoilSigma, 0f),  // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneAdsRecoilSigma + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneAdsRecoilSigma + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.BipodRecoilMean, 0f),      // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.BipodRecoilMean + 4, 0f),  // Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.BipodRecoilMean + 8, 0f),  // Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.BipodRecoilSigma, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.BipodRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.BipodRecoilSigma + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.BipodAdsRecoilMean, 0f),   // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.BipodAdsRecoilMean + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.BipodAdsRecoilMean + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.BipodAdsRecoilSigma, 0f),  // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.BipodAdsRecoilSigma + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.BipodAdsRecoilSigma + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneTransitionRecoilMean, 0f), // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneTransitionRecoilMean + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneTransitionRecoilMean + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneTransitionRecoilSigma, 0f), // X
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneTransitionRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + USQWeaponStaticInfo.ProneTransitionRecoilSigma + 8, 0f), // Z
        };
        
        public NoRecoil(ulong playerController, bool inGame, Game game)
            : base(playerController, inGame, game, NAME)
        {
        }
        
        protected override bool ShouldApplyModifications()
        {
            return Program.Config.NoRecoil;
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
                foreach (var entry in _noRecoilAnimEntries)
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
                        foreach (var entry in _noRecoilWeaponEntries)
                        {
                            float originalValue = Memory.ReadValue<float>(weaponStaticInfo + entry.Address);
                            _originalWeaponValues[entry.Address] = originalValue;
                            
                            // Store in config with descriptive key
                            string configKey = $"Weapon_{entry.Address:X}";
                            _configWeaponValues[configKey] = originalValue;
                        }
                    }
                }
                
                // Get original camera recoil state
                _originalCameraRecoil = Memory.ReadValue<bool>(_cachedSoldierActor + ASQSoldier.bIsCameraRecoilActive);
                
                // Save to config
                SaveOriginalValuesToConfig();
                
                Logger.Debug($"[{_featureName}] Loaded {_originalAnimValues.Count} animation values and {_originalWeaponValues.Count} weapon values");
                
            }, "NoRecoil values");
        }
        
        protected override void ApplyModifications()
        {
            SafeApplyModifications(() =>
            {
                // Apply animation modifications
                if (_lastAnimInstance != 0)
                {
                    var animEntries = _noRecoilAnimEntries.Select(entry => 
                        new ScatterWriteDataEntry<float>(_lastAnimInstance + entry.Address, entry.Data)).ToList();
                    Memory.WriteScatter(animEntries);
                    Logger.Debug($"[{_featureName}] Applied {animEntries.Count} animation modifications");
                }
                
                // Apply weapon modifications
                if (_lastWeaponStaticInfo != 0)
                {
                    var weaponEntries = _noRecoilWeaponEntries.Select(entry => 
                        new ScatterWriteDataEntry<float>(_lastWeaponStaticInfo + entry.Address, entry.Data)).ToList();
                    Memory.WriteScatter(weaponEntries);
                    Logger.Debug($"[{_featureName}] Applied {weaponEntries.Count} weapon modifications");
                }
                
                // Disable camera recoil
                Memory.WriteValue(_cachedSoldierActor + ASQSoldier.bIsCameraRecoilActive, false);
                
            }, "NoRecoil modifications (70+ memory addresses)");
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
                
                // Restore camera recoil
                Memory.WriteValue(_cachedSoldierActor + ASQSoldier.bIsCameraRecoilActive, _originalCameraRecoil);
                
            }, "NoRecoil original values");
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
                    config.OriginalNoRecoilAnimValues = _configAnimValues;
                    config.OriginalNoRecoilWeaponValues = _configWeaponValues;
                    config.OriginalCameraRecoil = _originalCameraRecoil;
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