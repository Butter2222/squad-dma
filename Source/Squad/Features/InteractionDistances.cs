using System;
using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class InteractionDistances : Manager
    {
        public const string NAME = "InteractionDistances";
        
        private bool _isEnabled = false;
        private bool _isApplied = false;
        private PlayerState _lastKnownState = PlayerState.Unknown;
        
        public bool IsEnabled => _isEnabled;
        
        private float _originalUseInteractDistance = 0.0f;
        private float _originalInteractableRadiusMultiplier = 0.0f;
        private bool _originalsLoaded = false;
        
        private readonly Game _game;
        
        public InteractionDistances(ulong playerController, bool inGame, Game game = null)
            : base(playerController, inGame)
        {
            _game = game;
        }
        
        public void SetEnabled(bool enable)
        {
            _isEnabled = enable;
            Logger.Debug($"[{NAME}] Interaction distances {(enable ? "enabled" : "disabled")}");
            
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
                
                // Only apply interaction distance modifications when player is alive
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
                
                // Apply interaction distance modifications only if config is enabled and we have original values
                if (Program.Config.SetInteractionDistances && _originalsLoaded)
                {
                    ApplyInteractionDistanceModifications(soldierActor);
                }
                else if (!Program.Config.SetInteractionDistances && _isApplied)
                {
                    RestoreOriginalValues();
                }
                
                _lastKnownState = currentState;
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error applying interaction distances: {ex.Message}");
            }
        }
        
        private void LoadOriginalValues(ulong soldierActor)
        {
            try
            {
                _originalUseInteractDistance = Memory.ReadValue<float>(soldierActor + ASQSoldier.UseInteractDistance);
                _originalInteractableRadiusMultiplier = Memory.ReadValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier);
                
                _originalsLoaded = true;
                Logger.Debug($"[{NAME}] Loaded original interaction distance values: UseInteractDistance={_originalUseInteractDistance}, InteractableRadiusMultiplier={_originalInteractableRadiusMultiplier}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error loading original values: {ex.Message}");
            }
        }
        
        private void ApplyInteractionDistanceModifications(ulong soldierActor)
        {
            try
            {
                // Only write if not already applied to prevent spam
                if (!_isApplied)
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UseInteractDistance, 5000.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier, 100.0f);
                    
                    _isApplied = true;
                    Logger.Debug($"[{NAME}] Applied interaction distance modifications (extended values: 5000.0f, 100.0f)");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error applying interaction distance modifications: {ex.Message}");
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
                
                Memory.WriteValue<float>(soldierActor + ASQSoldier.UseInteractDistance, _originalUseInteractDistance);
                Memory.WriteValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier, _originalInteractableRadiusMultiplier);
                
                _isApplied = false;
                Logger.Debug($"[{NAME}] Restored original interaction distance values: UseInteractDistance={_originalUseInteractDistance}, InteractableRadiusMultiplier={_originalInteractableRadiusMultiplier}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error restoring original values: {ex.Message}");
            }
        }
    }
} 