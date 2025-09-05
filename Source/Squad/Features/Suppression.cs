using System;
using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class Suppression : Manager
    {
        public const string NAME = "Suppression";
        
        private bool _isEnabled = false;
        private bool _isApplied = false;
        private PlayerState _lastKnownState = PlayerState.Unknown;
        
        public bool IsEnabled => _isEnabled;
        
        private float _originalSuppressionPercentage = 0.0f;
        private float _originalMaxSuppression = -1.0f;
        private float _originalSuppressionMultiplier = 1.0f;
        private bool _originalCameraRecoil = true;
        private bool _originalsLoaded = false;
        
        private readonly Game _game;
        
        public Suppression(ulong playerController, bool inGame, Game game = null)
            : base(playerController, inGame)
        {
            _game = game;
        }
                
        public void SetEnabled(bool enable)
        {
            _isEnabled = enable;
            Logger.Debug($"[{NAME}] Suppression {(enable ? "enabled" : "disabled")}");
            
            // If disabling, immediately restore original values regardless of state
            if (!enable && _isApplied)
            {
                RestoreOriginalValues();
            }
            
            // Apply will be called by the Manager based on player state
        }
        
        public override void Apply()
        {
            try
            {
                // Only apply if feature is enabled
                if (!_isEnabled)
                {
                    return;
                }
                
                // Get current player state
                PlayerState currentState = _game?.GetPlayerState() ?? PlayerState.Unknown;
                
                // Only apply suppression modifications when player is alive
                if (currentState != PlayerState.Alive)
                {
                    // If we were previously applied and player is no longer alive, restore values
                    if (_isApplied && currentState != PlayerState.Alive)
                    {
                        Logger.Debug($"[{NAME}] Player state changed from {_lastKnownState} to {currentState}, restoring original values");
                        RestoreOriginalValues();
                    }
                    
                    _lastKnownState = currentState;
                    return;
                }
                
                // Validate player and soldier actor
                if (!IsLocalPlayerValid())
                {
                    Logger.Debug($"[{NAME}] Local player not valid, skipping apply");
                    return;
                }
                
                UpdateCachedPointers();
                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0)
                {
                    Logger.Debug($"[{NAME}] Soldier actor not valid, skipping apply");
                    return;
                }
                
                // Load original values if not already loaded
                if (!_originalsLoaded)
                {
                    LoadOriginalValues(soldierActor);
                }
                
                // Apply suppression modifications only if config is enabled and we have original values
                if (Program.Config.DisableSuppression && _originalsLoaded)
                {
                    ApplySuppressionModifications(soldierActor);
                }
                
                _lastKnownState = currentState;
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error applying suppression: {ex.Message}");
            }
        }
        
        private void LoadOriginalValues(ulong soldierActor)
        {
            try
            {
                _originalSuppressionPercentage = Memory.ReadValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage);
                _originalMaxSuppression = Memory.ReadValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage);
                _originalSuppressionMultiplier = Memory.ReadValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier);
                _originalCameraRecoil = Memory.ReadValue<bool>(soldierActor + ASQSoldier.bIsCameraRecoilActive);
                
                _originalsLoaded = true;
                Logger.Debug($"[{NAME}] Loaded original suppression values: Percentage={_originalSuppressionPercentage}, Max={_originalMaxSuppression}, Multiplier={_originalSuppressionMultiplier}, CameraRecoil={_originalCameraRecoil}");
                
                // Save original values to config
                if (Config.TryLoadConfig(out var config))
                {
                    config.OriginalSuppressionPercentage = _originalSuppressionPercentage;
                    config.OriginalMaxSuppression = _originalMaxSuppression;
                    config.OriginalSuppressionMultiplier = _originalSuppressionMultiplier;
                    config.OriginalCameraRecoil = _originalCameraRecoil;
                    Config.SaveConfig(config);
                    Logger.Debug($"[{NAME}] Saved original suppression values to config");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error loading original values: {ex.Message}");
            }
        }
        
        private void ApplySuppressionModifications(ulong soldierActor)
        {
            try
            {
                // Only write if not already applied to prevent spam
                if (!_isApplied)
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage, 0.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage, 0.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier, 0.0f);
                    Memory.WriteValue(soldierActor + ASQSoldier.bIsCameraRecoilActive, false);
                    
                    _isApplied = true;
                    Logger.Debug($"[{NAME}] Applied suppression modifications (disabled suppression effects)");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error applying suppression modifications: {ex.Message}");
            }
        }
        
        private void RestoreOriginalValues()
        {
            try
            {
                if (!_isApplied || !_originalsLoaded)
                {
                    return;
                }
                
                UpdateCachedPointers();
                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0)
                {
                    Logger.Debug($"[{NAME}] Cannot restore - soldier actor not valid");
                    return;
                }
                
                Memory.WriteValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage, _originalSuppressionPercentage);
                Memory.WriteValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage, _originalMaxSuppression);
                Memory.WriteValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier, _originalSuppressionMultiplier);
                Memory.WriteValue(soldierActor + ASQSoldier.bIsCameraRecoilActive, _originalCameraRecoil);
                
                _isApplied = false;
                Logger.Debug($"[{NAME}] Restored original suppression values");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error restoring original values: {ex.Message}");
            }
        }
    }
} 