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

        private const float ZOOM_IN_DURATION_MS = 220f;
        private const float ZOOM_OUT_DURATION_MS = 160f;
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

        // Ease-in cubic: slow start, accelerates — used for zoom-in
        // Easing in front-loads the subtle perceptual shift before the FOV compression peaks,
        // which reduces the disorienting "lurch" sensation at the end of the transition.
        private static float EaseIn(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return t * t * t;
        }

        // Ease-out cubic: fast start, decelerates — used for zoom-out
        // Eyes re-expand quickly and settle naturally; a slow start on zoom-out feels laggy.
        private static float EaseOut(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            t = 1f - t;
            return 1f - t * t * t;
        }

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

                float currentCameraFOV = Memory.ReadValue<float>(cameraManager + PlayerCameraManager.DefaultFOV);

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

                if (!_isQuickZoomEnabled && !_isAnimating)
                {
                    if (currentCameraFOV > 10.0f && currentCameraFOV < 180.0f)
                        _capturedOriginalFOV = currentCameraFOV;
                    return;
                }

                if (_isAnimating)
                {
                    float duration = _isQuickZoomEnabled ? ZOOM_IN_DURATION_MS : ZOOM_OUT_DURATION_MS;
                    float progress = Math.Min((float)_zoomTimer.ElapsedMilliseconds / duration, 1f);
                    float eased = _isQuickZoomEnabled ? EaseIn(progress) : EaseOut(progress);
                    float nextFOV = _startFOV + (_targetFOV - _startFOV) * eased;

                    EnqueueWrite<float>(cameraManager + PlayerCameraManager.DefaultFOV, nextFOV);

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
                    EnqueueWrite<float>(cameraManager + PlayerCameraManager.DefaultFOV, ZOOM_TARGET_FOV);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error in QuickZoom: {ex.Message}");
            }
        }
    }
}
