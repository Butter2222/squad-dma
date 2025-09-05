using System;
using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// State-aware SpeedHack feature that prevents memory write spam and only applies when player is alive
    /// </summary>
    public class SpeedHack : StateAwareFeature
    {
        public const string NAME = "SpeedHack";
        
        private float _originalTimeDilation = 1.0f;
        private const float SPEED_MULTIPLIER = 4.0f;
        private const float NORMAL_SPEED = 1.0f;
        
        public SpeedHack(ulong playerController, bool inGame, Game game)
            : base(playerController, inGame, game, NAME)
        {
        }
        
        protected override bool ShouldApplyModifications()
        {
            return Program.Config.SetSpeedHack;
        }
        
        protected override void LoadOriginalValues()
        {
            SafeLoadOriginals(() =>
            {
                // Load original time dilation value
                _originalTimeDilation = Memory.ReadValue<float>(_cachedSoldierActor + Actor.CustomTimeDilation);
                Logger.Debug($"[{_featureName}] Loaded original time dilation: {_originalTimeDilation}");
                
            }, "SpeedHack time dilation value");
        }
        
        protected override void ApplyModifications()
        {
            SafeApplyModifications(() =>
            {
                Memory.WriteValue<float>(_cachedSoldierActor + Actor.CustomTimeDilation, SPEED_MULTIPLIER);
                Logger.Debug($"[{_featureName}] Applied speed multiplier: {SPEED_MULTIPLIER}x");
                
            }, "SpeedHack time dilation modification");
        }
        
        protected override void RestoreOriginalValues()
        {
            SafeRestoreValues(() =>
            {
                Memory.WriteValue<float>(_cachedSoldierActor + Actor.CustomTimeDilation, _originalTimeDilation);
                Logger.Debug($"[{_featureName}] Restored original time dilation: {_originalTimeDilation}");
                
            }, "SpeedHack original time dilation");
        }
    }
}