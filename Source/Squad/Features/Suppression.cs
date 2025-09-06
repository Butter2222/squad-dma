using System;
using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// State-aware Suppression feature that prevents memory write spam and only applies when player is alive
    /// </summary>
    public class Suppression : StateAwareFeature
    {
        public const string NAME = "Suppression";
        
        private float _originalSuppressionPercentage = 0.0f;
        private float _originalMaxSuppression = -1.0f;
        private float _originalSuppressionMultiplier = 1.0f;
        private bool _originalCameraRecoil = true;
        
        public Suppression(ulong playerController, bool inGame, Game game)
            : base(playerController, inGame, game, NAME)
        {
        }
        
        protected override bool ShouldApplyModifications()
        {
            return Program.Config.DisableSuppression;
        }
        
        protected override void LoadOriginalValues()
        {
            SafeLoadOriginals(() =>
            {
                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0) return;
                
                _originalSuppressionPercentage = Memory.ReadValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage);
                _originalMaxSuppression = Memory.ReadValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage);
                _originalSuppressionMultiplier = Memory.ReadValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier);
                _originalCameraRecoil = Memory.ReadValue<bool>(soldierActor + ASQSoldier.bIsCameraRecoilActive);
                
                Logger.Debug($"[{_featureName}] Loaded original suppression values: Percentage={_originalSuppressionPercentage}, Max={_originalMaxSuppression}, Multiplier={_originalSuppressionMultiplier}, CameraRecoil={_originalCameraRecoil}");
                
                // Save original values to config
                SaveOriginalValuesToConfig();
                
            }, "Suppression values");
        }
        
        protected override void ApplyModifications()
        {
            SafeApplyModifications(() =>
            {
                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0) return;
                
                Memory.WriteValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage, 0.0f);
                Memory.WriteValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage, 0.0f);
                Memory.WriteValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier, 0.0f);
                Memory.WriteValue(soldierActor + ASQSoldier.bIsCameraRecoilActive, false);
                
                Logger.Debug($"[{_featureName}] Applied suppression modifications (disabled suppression effects)");
                
            }, "Suppression modifications");
        }
        
        protected override void RestoreOriginalValues()
        {
            SafeRestoreValues(() =>
            {
                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0) return;
                
                Memory.WriteValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage, _originalSuppressionPercentage);
                Memory.WriteValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage, _originalMaxSuppression);
                Memory.WriteValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier, _originalSuppressionMultiplier);
                Memory.WriteValue(soldierActor + ASQSoldier.bIsCameraRecoilActive, _originalCameraRecoil);
                
                Logger.Debug($"[{_featureName}] Restored original suppression values");
                
            }, "Suppression original values");
        }
        
        private void SaveOriginalValuesToConfig()
        {
            try
            {
                if (Config.TryLoadConfig(out var config))
                {
                    config.OriginalSuppressionPercentage = _originalSuppressionPercentage;
                    config.OriginalMaxSuppression = _originalMaxSuppression;
                    config.OriginalSuppressionMultiplier = _originalSuppressionMultiplier;
                    config.OriginalCameraRecoil = _originalCameraRecoil;
                    Config.SaveConfig(config);
                    Logger.Debug($"[{_featureName}] Saved original suppression values to config");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{_featureName}] Error saving original suppression values to config: {ex.Message}");
            }
        }
    }
} 