using System;
using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// State-aware Collision feature that prevents memory write spam and only applies when player is alive
    /// </summary>
    public class Collision : StateAwareFeature
    {
        public const string NAME = "Collision";
        
        // Original collision values
        private byte _originalCollisionEnabled = 1; // QueryOnly (normal collision)
        
        // Reference to AirStuck for dependency checking
        private readonly AirStuck _airStuck;
        
        public enum ECollisionEnabled : byte
        {
            NoCollision = 0,
            QueryOnly = 1,
            PhysicsOnly = 2,
            QueryAndPhysics = 3
        }
        
        public Collision(ulong playerController, bool inGame, Game game, AirStuck airStuck = null)
            : base(playerController, inGame, game, NAME)
        {
            _airStuck = airStuck;
        }
        
        protected override bool ValidateGameState()
        {
            // Additional validation for collision system
            if (!base.ValidateGameState())
                return false;
                
            // Ensure we have root component for collision modification
            ulong rootComponent = Memory.ReadPtr(_cachedSoldierActor + Actor.RootComponent);
            return rootComponent != 0;
        }
        
        protected override bool ShouldApplyModifications()
        {
            return Program.Config.DisableCollision;
        }
        
        /// <summary>
        /// Check if AirStuck is enabled (required dependency for NoCollision)
        /// </summary>
        private bool IsAirStuckEnabled()
        {
            return _airStuck?.IsEnabled == true;
        }
        
        /// <summary>
        /// Override SetEnabled to enforce AirStuck dependency
        /// </summary>
        public new void SetEnabled(bool enable)
        {
            if (enable && !IsAirStuckEnabled())
            {
                Logger.Info($"[{_featureName}] Cannot enable NoCollision - AirStuck must be enabled first!");
                return;
            }
            
            base.SetEnabled(enable);
        }
        
        protected override void LoadOriginalValues()
        {
            SafeLoadOriginals(() =>
            {
                ulong soldierActor = _cachedSoldierActor;
                ulong rootComponent = Memory.ReadPtr(soldierActor + Actor.RootComponent);
                if (rootComponent == 0) return;
                
                ulong bodyInstanceAddr = rootComponent + UPrimitiveComponent.BodyInstance;
                
                // Load original collision state
                _originalCollisionEnabled = Memory.ReadValue<byte>(bodyInstanceAddr + FBodyInstance.CollisionEnabled);
                
                Logger.Debug($"[{_featureName}] Loaded original collision state: {(ECollisionEnabled)_originalCollisionEnabled} ({_originalCollisionEnabled})");
                
                // Save to config for persistence
                SaveOriginalValuesToConfig();
                
            }, "Collision system values");
        }
        
        protected override void ApplyModifications()
        {
            SafeApplyModifications(() =>
            {
                ulong soldierActor = _cachedSoldierActor;
                ulong rootComponent = Memory.ReadPtr(soldierActor + Actor.RootComponent);
                if (rootComponent == 0) return;
                
                ulong bodyInstanceAddr = rootComponent + UPrimitiveComponent.BodyInstance;
                
                // Disable collision
                Memory.WriteValue<byte>(bodyInstanceAddr + FBodyInstance.CollisionEnabled, (byte)ECollisionEnabled.NoCollision);
                
                Logger.Debug($"[{_featureName}] Applied collision modification: NoCollision (0)");
                
            }, "Collision system modification (1 physics address)");
        }
        
        protected override void RestoreOriginalValues()
        {
            SafeRestoreValues(() =>
            {
                ulong soldierActor = _cachedSoldierActor;
                ulong rootComponent = Memory.ReadPtr(soldierActor + Actor.RootComponent);
                if (rootComponent == 0) return;
                
                ulong bodyInstanceAddr = rootComponent + UPrimitiveComponent.BodyInstance;
                
                // Restore original collision state
                Memory.WriteValue<byte>(bodyInstanceAddr + FBodyInstance.CollisionEnabled, _originalCollisionEnabled);
                
                Logger.Debug($"[{_featureName}] Restored original collision state: {(ECollisionEnabled)_originalCollisionEnabled} ({_originalCollisionEnabled})");
                
            }, "Collision original values");
        }
        
        /// <summary>
        /// Get current collision state for debugging
        /// </summary>
        public ECollisionEnabled GetCurrentCollisionState()
        {
            try
            {
                if (!ValidateGameState()) return ECollisionEnabled.QueryOnly;
                
                ulong soldierActor = _cachedSoldierActor;
                ulong rootComponent = Memory.ReadPtr(soldierActor + Actor.RootComponent);
                if (rootComponent == 0) return ECollisionEnabled.QueryOnly;
                
                ulong bodyInstanceAddr = rootComponent + UPrimitiveComponent.BodyInstance;
                byte currentState = Memory.ReadValue<byte>(bodyInstanceAddr + FBodyInstance.CollisionEnabled);
                
                return (ECollisionEnabled)currentState;
            }
            catch
            {
                return ECollisionEnabled.QueryOnly;
            }
        }
        
        private void SaveOriginalValuesToConfig()
        {
            try
            {
                if (Config.TryLoadConfig(out var config))
                {
                    config.OriginalCollisionEnabled = _originalCollisionEnabled;
                    Config.SaveConfig(config);
                    Logger.Debug($"[{_featureName}] Saved original collision value to config");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{_featureName}] Error saving original collision value to config: {ex.Message}");
            }
        }
    }
} 