using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// State-aware NoCameraShake feature that disables camera shake by nulling the camera shake modifier pointer
    /// </summary>
    public class NoCameraShake : StateAwareFeature
    {
        public const string NAME = "NoCameraShake";
        
        // Original camera shake modifier pointer
        private ulong _originalPtr = 0;
        
        public NoCameraShake(ulong playerController, bool inGame, Game game)
            : base(playerController, inGame, game, NAME)
        {
        }
        
        protected override bool ShouldApplyModifications()
        {
            return Program.Config.NoCameraShake;
        }

        /// <summary>
        /// Load original camera shake modifier pointer
        /// </summary>
        protected override void LoadOriginalValues()
        {
            SafeLoadOriginals(() =>
            {
                ulong camMgr = _cachedCameraManager;
                if (camMgr == 0) return;
                
                _originalPtr = Memory.ReadPtr(camMgr + PlayerCameraManager.CachedCameraShakeMod);
                if (_originalPtr == 0) return;
                
                Logger.Debug($"[{_featureName}] Loaded original camera shake modifier: 0x{_originalPtr:X}");
            }, "camera shake modifier");
        }
        
        /// <summary>
        /// Apply modifications by nulling the camera shake modifier pointer
        /// </summary>
        protected override void ApplyModifications()
        {
            SafeApplyModifications(() =>
            {
                ulong camMgr = _cachedCameraManager;
                if (camMgr == 0) return;
                
                Memory.WriteValue(camMgr + PlayerCameraManager.CachedCameraShakeMod, 0UL);
                Logger.Debug($"[{_featureName}] Applied no camera shake (disabled camera shake modifier)");
                
            }, "camera shake modifier nulling");
        }

        /// <summary>
        /// Restore original camera shake modifier pointer
        /// </summary>
        protected override void RestoreOriginalValues()
        {
            SafeRestoreValues(() =>
            {
                ulong camMgr = _cachedCameraManager;
                if (camMgr == 0) return;
                
                Memory.WriteValue(camMgr + PlayerCameraManager.CachedCameraShakeMod, _originalPtr);
                Logger.Debug($"[{_featureName}] Restored original camera shake modifier: 0x{_originalPtr:X}");
                
            }, "camera shake modifier restoration");
        }
    }
} 