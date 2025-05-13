using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class NoCameraShake : Manager
    {
        public const string NAME = "NoCameraShake";
        private const int SHAKE_INFO_SIZE = 0x18;
        private const float DISABLED_SHAKE_SCALE = 0f;
        private const int TIMER_INTERVAL_MS = 1000; // Increased to 1 second to reduce spam
        private const int MAX_RETRIES = 3;
        
        private bool _isEnabled = false;
        private CancellationTokenSource _cancellationTokenSource;
        private int _retryCount = 0;
        
        public bool IsEnabled => _isEnabled;
        
        public NoCameraShake(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid())
            {
                Logger.Debug($"[{NAME}] Cannot enable/disable no camera shake - local player is not valid");
                return;
            }
            
            _isEnabled = enable;
            Logger.Debug($"[{NAME}] No camera shake {(enable ? "enabled" : "disabled")}");
            
            if (enable)
            {
                _retryCount = 0; // Reset retry count when enabling
                StartTimer();
            }
            else
            {
                StopTimer();
            }
        }

        private void StartTimer()
        {
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (!_isEnabled || !IsLocalPlayerValid())
                        {
                            StopTimer();
                            return;
                        }

                        if (!Memory.InGame)
                        {
                            await Task.Delay(TIMER_INTERVAL_MS, _cancellationTokenSource.Token);
                            continue;
                        }

                        Apply();
                    }
                    catch (Exception ex)
                    {
                        if (_retryCount < MAX_RETRIES)
                        {
                            _retryCount++;
                            Logger.Debug($"[{NAME}] Retry attempt {_retryCount}/{MAX_RETRIES}: {ex.Message}");
                        }
                        else
                        {
                            Logger.Error($"[{NAME}] Max retries reached, stopping timer: {ex.Message}");
                            StopTimer();
                            return;
                        }
                    }
                    await Task.Delay(TIMER_INTERVAL_MS, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);
        }

        private void StopTimer()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            _retryCount = 0;
        }

        public override void Apply()
        {
            try
            {
                if (!_isEnabled || !IsLocalPlayerValid() || !Memory.InGame)
                {
                    if (!Memory.InGame)
                    {
                        StopTimer();
                    }
                    return;
                }

                var cameraPointers = GetCameraPointers();
                if (!cameraPointers.IsValid)
                {
                    return;
                }

                DisableCameraShake(cameraPointers);
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error setting no camera shake: {ex.Message}");
            }
        }

        private (bool IsValid, ulong CameraManager, ulong ShakeModifier, ulong ActiveShakes) GetCameraPointers()
        {
            if (!IsLocalPlayerValid())
            {
                return (false, 0, 0, 0);
            }

            ulong cameraManagerPtr = _cachedCameraManager;
            if (cameraManagerPtr == 0)
            {
                cameraManagerPtr = Memory.ReadPtr(_playerController + PlayerController.PlayerCameraManager);
                if (cameraManagerPtr == 0)
                {
                    if (Memory.InGame)
                    {
                        Logger.Debug($"[{NAME}] Camera manager not ready yet");
                    }
                    return (false, 0, 0, 0);
                }
            }

            ulong cameraShakeModPtr = Memory.ReadPtr(cameraManagerPtr + PlayerCameraManager.CachedCameraShakeMod);
            if (cameraShakeModPtr == 0)
            {
                if (Memory.InGame)
                {
                    Logger.Debug($"[{NAME}] Camera shake modifier not ready yet");
                }
                return (false, 0, 0, 0);
            }

            ulong activeShakesDataPtr = Memory.ReadPtr(cameraShakeModPtr + UCameraModifier_CameraShake.ActiveShakes);
            return (true, cameraManagerPtr, cameraShakeModPtr, activeShakesDataPtr);
        }

        private void DisableCameraShake((bool IsValid, ulong CameraManager, ulong ShakeModifier, ulong ActiveShakes) pointers)
        {
            // Prevent new shakes by setting scale to 0
            Memory.WriteValue(pointers.ShakeModifier + UCameraModifier_CameraShake.SplitScreenShakeScale, DISABLED_SHAKE_SCALE);

            if (pointers.ActiveShakes == 0)
            {
                return;
            }

            // Get number of active shakes
            int activeShakesCount = Memory.ReadValue<int>(pointers.ShakeModifier + UCameraModifier_CameraShake.ActiveShakes + 0x8);
            if (activeShakesCount <= 0)
            {
                return;
            }

            // Create scatter write entries for each active shake
            var scatterEntries = new List<IScatterWriteDataEntry<float>>();
            for (int i = 0; i < activeShakesCount; i++)
            {
                ulong shakeBasePtr = Memory.ReadPtr(pointers.ActiveShakes + (uint)(i * SHAKE_INFO_SIZE));
                if (shakeBasePtr != 0)
                {
                    scatterEntries.Add(new ScatterWriteDataEntry<float>(shakeBasePtr + UCameraShakeBase.ShakeScale, DISABLED_SHAKE_SCALE));
                }
            }

            if (scatterEntries.Count > 0)
            {
                Memory.WriteScatter(scatterEntries);
            }
        }

        public void Dispose()
        {
            StopTimer();
            _cancellationTokenSource.Dispose();
        }
    }
} 