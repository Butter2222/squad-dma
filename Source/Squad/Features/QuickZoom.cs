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
        private bool _lastZoomState = false;
        private float _originalFOV = 0.0f;

        private const float ZOOM_TARGET_FOV = 42.0f;
        private const float DEFAULT_FOV_FALLBACK = 90.0f;

        public QuickZoom(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }

        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isQuickZoomEnabled = enable;
            Logger.Debug($"[{NAME}] Quick Zoom {(enable ? "enabled" : "disabled")}");
        }

        private static bool IsSaneFOV(float fov) => fov > 10.0f && fov < 180.0f;

        public override void Apply()
        {
            try
            {
                if (!IsLocalPlayerValid()) return;

                UpdateCachedPointers();
                ulong cameraManager = _cachedCameraManager != 0
                    ? _cachedCameraManager
                    : Memory.ReadPtr(_playerController + PlayerController.PlayerCameraManager);
                if (cameraManager == 0) return;

                ulong fovAddress = cameraManager + PlayerCameraManager.DefaultFOV;

                if (_isQuickZoomEnabled != _lastZoomState)
                {
                    if (_isQuickZoomEnabled)
                    {
                        // Remember the player's own FOV before it is replaced
                        float fov = Memory.ReadValue<float>(fovAddress);
                        if (IsSaneFOV(fov))
                            _originalFOV = fov;
                    }
                    else
                    {
                        EnqueueWrite<float>(fovAddress, IsSaneFOV(_originalFOV) ? _originalFOV : DEFAULT_FOV_FALLBACK);
                        Logger.Debug($"[{NAME}] Restored FOV: {_originalFOV}");
                    }

                    _lastZoomState = _isQuickZoomEnabled;
                }

                // Held: keep the game from resetting the FOV
                if (_isQuickZoomEnabled)
                    EnqueueWrite<float>(fovAddress, ZOOM_TARGET_FOV);
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error in QuickZoom: {ex.Message}");
            }
        }
    }
}
