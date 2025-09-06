using System;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// Base class for all state-aware features that only apply modifications when player is alive
    /// and prevent unsafe memory writes through spam protection
    /// </summary>
    public abstract class StateAwareFeature : Manager
    {
        protected bool _isEnabled = false;
        protected bool _isApplied = false;
        protected PlayerState _lastKnownState = PlayerState.Unknown;
        protected bool _originalsLoaded = false;
        
        protected readonly Game _game;
        protected readonly string _featureName;
        
        public bool IsEnabled => _isEnabled;
        public bool IsApplied => _isApplied;
        
        protected StateAwareFeature(ulong playerController, bool inGame, Game game, string featureName)
            : base(playerController, inGame)
        {
            _game = game;
            _featureName = featureName;
        }
        
        /// <summary>
        /// Enable or disable the feature
        /// </summary>
        public virtual void SetEnabled(bool enable)
        {
            _isEnabled = enable;
            Logger.Debug($"[{_featureName}] Feature {(enable ? "enabled" : "disabled")}");
            
            // If disabling, immediately restore original values regardless of state
            if (!enable && _isApplied)
            {
                RestoreOriginalValues();
            }
        }
        
        /// <summary>
        /// Main apply method - handles state checking and safe memory operations
        /// </summary>
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
                
                // Only apply modifications when player is alive
                if (currentState != PlayerState.Alive)
                {
                    // If we were previously applied and player is no longer alive, restore values
                    if (_isApplied && currentState != PlayerState.Alive)
                    {
                        Logger.Debug($"[{_featureName}] Player state changed from {_lastKnownState} to {currentState}, restoring original values");
                        RestoreOriginalValues();
                    }
                    
                    _lastKnownState = currentState;
                    return;
                }
                
                // Player is alive - reset flags to allow reapplication
                if (_lastKnownState != PlayerState.Alive)
                {
                    Logger.Debug($"[{_featureName}] Player became alive, resetting flags for reapplication");
                    _originalsLoaded = false;
                    _isApplied = false;
                }
                
                // Validate player and required actors
                if (!ValidateGameState())
                {
                    Logger.Debug($"[{_featureName}] Game state not valid, skipping apply");
                    return;
                }
                
                // Load original values if not already loaded
                if (!_originalsLoaded)
                {
                    LoadOriginalValues();
                }
                
                // Apply modifications only if we have original values and feature should be active
                if (_originalsLoaded && ShouldApplyModifications())
                {
                    ApplyModifications();
                }
                else if (!ShouldApplyModifications() && _isApplied)
                {
                    RestoreOriginalValues();
                }
                
                _lastKnownState = currentState;
            }
            catch (Exception ex)
            {
                Logger.Error($"[{_featureName}] Error in Apply(): {ex.Message}");
            }
        }
        
        /// <summary>
        /// Validate that the game state is ready for memory operations
        /// </summary>
        protected virtual bool ValidateGameState()
        {
            if (!IsLocalPlayerValid())
            {
                return false;
            }
            
            UpdateCachedPointers();
            return _cachedSoldierActor != 0;
        }
        
        /// <summary>
        /// Determine if modifications should be applied based on config and state
        /// </summary>
        protected abstract bool ShouldApplyModifications();
        
        /// <summary>
        /// Load original values from memory - called once when player becomes alive
        /// </summary>
        protected abstract void LoadOriginalValues();
        
        /// <summary>
        /// Apply the feature modifications - called only once per alive session
        /// </summary>
        protected abstract void ApplyModifications();
        
        /// <summary>
        /// Restore original values - called when player dies or feature is disabled
        /// </summary>
        protected abstract void RestoreOriginalValues();
        
        /// <summary>
        /// Helper method to safely apply modifications with spam protection
        /// </summary>
        protected void SafeApplyModifications(Action applyAction, string operationName)
        {
            try
            {
                // Only write if not already applied to prevent spam
                if (!_isApplied)
                {
                    applyAction();
                    _isApplied = true;
                    Logger.Debug($"[{_featureName}] Applied {operationName}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{_featureName}] Error applying {operationName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Helper method to safely restore values
        /// </summary>
        protected void SafeRestoreValues(Action restoreAction, string operationName)
        {
            try
            {
                if (!_isApplied || !_originalsLoaded)
                {
                    return;
                }
                
                if (!ValidateGameState())
                {
                    Logger.Debug($"[{_featureName}] Cannot restore {operationName} - game state not valid");
                    return;
                }
                
                restoreAction();
                _isApplied = false;
                Logger.Debug($"[{_featureName}] Restored {operationName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{_featureName}] Error restoring {operationName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Helper method to safely load original values
        /// </summary>
        protected void SafeLoadOriginals(Action loadAction, string operationName)
        {
            try
            {
                loadAction();
                _originalsLoaded = true;
                Logger.Debug($"[{_featureName}] Loaded original values for {operationName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{_featureName}] Error loading original values for {operationName}: {ex.Message}");
                _originalsLoaded = false;
            }
        }
    }
}
