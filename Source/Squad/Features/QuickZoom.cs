using System;
using Offsets;
using squad_dma;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class QuickZoom : Manager
    {
        public const string NAME = "QuickZoom";

        private bool _isQuickZoomEnabled = false;
        private float _originalFov = 0.0f;
        
        public QuickZoom(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isQuickZoomEnabled = enable;
            Logger.Debug($"[{NAME}] Quick Zoom {(enable ? "enabled" : "disabled")}");
            Apply();
        }
        
        public override void Apply()
        {
            try
            {
                if (!IsLocalPlayerValid()) return;
                
                UpdateCachedPointers();
                ulong cameraManager = Memory.ReadPtr(_playerController + PlayerController.PlayerCameraManager);
                if (cameraManager == 0) return;
                
                if (_isQuickZoomEnabled)
                {
                    // Only read and store original FOV if we don't have it cached
                    if (_originalFov == 0.0f)
                    {
                        _originalFov = Memory.ReadValue<float>(cameraManager + PlayerCameraManager.DefaultFOV);
                        Logger.Debug($"[{NAME}] Stored original FOV: {_originalFov}");
                        
                        // Save the original FOV to config
                        if (Config.TryLoadConfig(out var config))
                        {
                            config.OriginalFov = _originalFov;
                            Config.SaveConfig(config);
                            Logger.Debug($"[{NAME}] Saved original FOV to config");
                        }
                    }
                    Memory.WriteValue<float>(cameraManager + PlayerCameraManager.DefaultFOV, 30.0f);
                    Logger.Debug($"[{NAME}] Set FOV to 30.0");
                }
                else if (_originalFov != 0.0f)
                {
                    Memory.WriteValue<float>(cameraManager + PlayerCameraManager.DefaultFOV, _originalFov);
                    Logger.Debug($"[{NAME}] Restored original FOV: {_originalFov}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error setting Quick Zoom: {ex.Message}");
            }
        }
    }
} 