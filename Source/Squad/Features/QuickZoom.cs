using System;
using System.Diagnostics;
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
        private float _capturedOriginalFOV = 0.0f;
        private float _startFOV = 0.0f;
        private float _targetFOV = 0.0f;
        private bool _isAnimating = false;
        private readonly Stopwatch _zoomTimer = new Stopwatch();

        private const float ZOOM_DURATION_MS = 180f;
        private const float ZOOM_TARGET_FOV = 30.0f;
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

        // Smoothstep: smooth ease-in/ease-out, no sudden velocity changes
        private static float SmoothStep(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        public override void Apply()
        {
            try
            {
                if (!IsLocalPlayerValid()) return;

                UpdateCachedPointers();
                ulong cameraManager = Memory.ReadPtr(_playerController + PlayerController.PlayerCameraManager);
                if (cameraManager == 0) return;

                float currentCameraFOV = Memory.ReadValue<float>(cameraManager + PlayerCameraManager.DefaultFOV);

                // State change must be checked before the idle branch so releasing the key
                // triggers the reverse animation rather than falling into idle and corrupting
                // _capturedOriginalFOV with the current zoomed value.
                if (_isQuickZoomEnabled != _lastZoomState)
                {
                    _startFOV = currentCameraFOV;
                    _targetFOV = _isQuickZoomEnabled
                        ? ZOOM_TARGET_FOV
                        : (_capturedOriginalFOV > 10f ? _capturedOriginalFOV : DEFAULT_FOV_FALLBACK);
                    _zoomTimer.Restart();
                    _isAnimating = true;
                    _lastZoomState = _isQuickZoomEnabled;
                }

                // Idle: not zoomed and no animation running — track original FOV for later restore
                if (!_isQuickZoomEnabled && !_isAnimating)
                {
                    if (currentCameraFOV > 10.0f && currentCameraFOV < 180.0f)
                        _capturedOriginalFOV = currentCameraFOV;
                    return;
                }

                if (_isAnimating)
                {
                    float progress = Math.Min((float)_zoomTimer.ElapsedMilliseconds / ZOOM_DURATION_MS, 1f);
                    float nextFOV = _startFOV + (_targetFOV - _startFOV) * SmoothStep(progress);

                    Memory.WriteValue<float>(cameraManager + PlayerCameraManager.DefaultFOV, nextFOV);

                    if (progress >= 1f)
                    {
                        _isAnimating = false;
                        _zoomTimer.Stop();

                        if (_isQuickZoomEnabled)
                            Logger.Debug($"[{NAME}] Zoomed to FOV: {_targetFOV}");
                        else
                            Logger.Debug($"[{NAME}] Restored FOV: {_capturedOriginalFOV}");
                    }
                }
                else if (_isQuickZoomEnabled)
                {
                    // Hold zoom FOV to resist game resets (e.g. weapon raise/lower)
                    Memory.WriteValue<float>(cameraManager + PlayerCameraManager.DefaultFOV, ZOOM_TARGET_FOV);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error in QuickZoom: {ex.Message}");
            }
        }
    }
}
