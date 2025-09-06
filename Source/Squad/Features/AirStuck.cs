using System;
using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// State-aware AirStuck feature that prevents memory write spam and only applies when player is alive
    /// </summary>
    public class AirStuck : StateAwareFeature
    {
        public const string NAME = "AirStuck";
                
        // Original movement values
        private byte _originalMovementMode = 1; // MOVE_Walking
        private byte _originalReplicatedMovementMode = 1; // MOVE_Walking
        private byte _originalReplicateMovement = 16;
        private float _originalMaxFlySpeed = 200.0f;
        private float _originalMaxCustomMovementSpeed = 600.0f;
        private float _originalMaxAcceleration = 500.0f;
        
        private enum EMovementMode : byte
        {
            MOVE_None = 0,
            MOVE_Walking = 1,
            MOVE_NavWalking = 2,
            MOVE_Falling = 3,
            MOVE_Swimming = 4,
            MOVE_Flying = 5,
            MOVE_Custom = 6,
            MOVE_MAX = 7
        }
        
        public AirStuck(ulong playerController, bool inGame, Game game, object unused = null)
            : base(playerController, inGame, game, NAME)
        {
        }
        
        protected override bool ValidateGameState()
        {
            // Additional validation for movement system
            if (!base.ValidateGameState())
                return false;
                
            // Ensure we have character movement component
            return _cachedCharacterMovement != 0;
        }
        
        protected override bool ShouldApplyModifications()
        {
            return Program.Config.AirStuckEnabled && Program.Config.SetAirStuck;
        }
        
        protected override void LoadOriginalValues()
        {
            SafeLoadOriginals(() =>
            {
                ulong characterMovement = _cachedCharacterMovement;
                ulong soldierActor = _cachedSoldierActor;
                
                // Load original movement system values
                _originalMovementMode = Memory.ReadValue<byte>(characterMovement + CharacterMovementComponent.MovementMode);
                _originalReplicatedMovementMode = Memory.ReadValue<byte>(characterMovement + Character.ReplicatedMovementMode);
                _originalReplicateMovement = Memory.ReadValue<byte>(soldierActor + Actor.bReplicateMovement);
                _originalMaxFlySpeed = Memory.ReadValue<float>(characterMovement + CharacterMovementComponent.MaxFlySpeed);
                _originalMaxCustomMovementSpeed = Memory.ReadValue<float>(characterMovement + CharacterMovementComponent.MaxCustomMovementSpeed);
                _originalMaxAcceleration = Memory.ReadValue<float>(characterMovement + CharacterMovementComponent.MaxAcceleration);
                
                Logger.Debug($"[{_featureName}] Loaded original movement values: Mode={_originalMovementMode}, " +
                           $"ReplicatedMode={_originalReplicatedMovementMode}, ReplicateMovement={_originalReplicateMovement}, " +
                           $"MaxFly={_originalMaxFlySpeed}, MaxCustom={_originalMaxCustomMovementSpeed}, MaxAccel={_originalMaxAcceleration}");
                
                // Save to config for persistence
                SaveOriginalValuesToConfig();
                
            }, "AirStuck movement system values");
        }
        
        protected override void ApplyModifications()
        {
            SafeApplyModifications(() =>
            {
                ulong characterMovement = _cachedCharacterMovement;
                ulong soldierActor = _cachedSoldierActor;
                
                // Apply flying movement modifications
                Memory.WriteValue<byte>(characterMovement + CharacterMovementComponent.MovementMode, (byte)EMovementMode.MOVE_Flying);
                Memory.WriteValue<byte>(characterMovement + Character.ReplicatedMovementMode, (byte)EMovementMode.MOVE_Flying);
                Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, 0);
                Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxFlySpeed, 2000.0f);
                Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxCustomMovementSpeed, 2000.0f);
                Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxAcceleration, 2000.0f);
                
                Logger.Debug($"[{_featureName}] Applied AirStuck: Flying mode enabled for scouting");
                
            }, "AirStuck flying movement modifications (6 memory addresses)");
        }
        
        protected override void RestoreOriginalValues()
        {
            SafeRestoreValues(() =>
            {
                ulong characterMovement = _cachedCharacterMovement;
                ulong soldierActor = _cachedSoldierActor;
                
                // Restore original movement values
                Memory.WriteValue<byte>(characterMovement + CharacterMovementComponent.MovementMode, _originalMovementMode);
                Memory.WriteValue<byte>(characterMovement + Character.ReplicatedMovementMode, _originalReplicatedMovementMode);
                Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, _originalReplicateMovement);
                Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxFlySpeed, _originalMaxFlySpeed);
                Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxCustomMovementSpeed, _originalMaxCustomMovementSpeed);
                Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxAcceleration, _originalMaxAcceleration);
                
                Logger.Debug($"[{_featureName}] Restored original movement - AirStuck disabled");
                
            }, "AirStuck original movement values");
        }
        
        /// <summary>
        /// Get reference to collision system for external management
        /// </summary>
        
        /// <summary>
        /// Force disable AirStuck - emergency recovery from stuck state
        /// </summary>
        public void ForceDisable()
        {
            try
            {
                Logger.Info($"[{_featureName}] Force disable triggered - emergency recovery");
                
                // Disable the feature
                SetEnabled(false);
                
                // Force restore original values if we can access game state
                if (ValidateGameState() && _originalsLoaded)
                {
                    RestoreOriginalValues();
                    _isApplied = false;
                }
                
                Logger.Info($"[{_featureName}] Force disable completed");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{_featureName}] Error during force disable: {ex.Message}");
            }
        }
        
        private void SaveOriginalValuesToConfig()
        {
            try
            {
                if (Config.TryLoadConfig(out var config))
                {
                    config.OriginalMovementMode = _originalMovementMode;
                    config.OriginalReplicatedMovementMode = _originalReplicatedMovementMode;
                    config.OriginalReplicateMovement = _originalReplicateMovement;
                    config.OriginalMaxFlySpeed = _originalMaxFlySpeed;
                    config.OriginalMaxCustomMovementSpeed = _originalMaxCustomMovementSpeed;
                    config.OriginalMaxAcceleration = _originalMaxAcceleration;
                    Config.SaveConfig(config);
                    Logger.Debug($"[{_featureName}] Saved original movement values to config");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{_featureName}] Error saving original values to config: {ex.Message}");
            }
        }
    }
} 