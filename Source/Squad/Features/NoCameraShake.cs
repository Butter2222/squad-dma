using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class NoCameraShake : Manager
    {
        public const string NAME = "NoCameraShake";
        private const int SHAKE_INFO_SIZE = 0x18;
        private const float DISABLED_SHAKE_SCALE = 0f;
        private const int TIMER_INTERVAL_MS = 1;
        
        private bool _isEnabled = false;
        private CancellationTokenSource _cancellationTokenSource;
        
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
                Logger.Error($"[{NAME}] Cannot enable/disable no camera shake - local player is not valid");
                return;
            }
            
            _isEnabled = enable;
            Logger.Debug($"[{NAME}] No camera shake {(enable ? "enabled" : "disabled")}");
            
            if (enable)
            {
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
                        Apply();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[{NAME}] Error in timer task: {ex.Message}");
                    }
                    await Task.Delay(TIMER_INTERVAL_MS, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);
        }

        private void StopTimer()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
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
            ulong cameraManagerPtr = Memory.ReadPtr(_playerController + PlayerController.PlayerCameraManager);
            if (cameraManagerPtr == 0)
            {
                if (Memory.InGame)
                {
                    Logger.Error($"[{NAME}] Cannot apply no camera shake - camera manager is not valid");
                }
                return (false, 0, 0, 0);
            }

            ulong cameraShakeModPtr = Memory.ReadPtr(cameraManagerPtr + PlayerCameraManager.CachedCameraShakeMod);
            if (cameraShakeModPtr == 0)
            {
                if (Memory.InGame)
                {
                    Logger.Error($"[{NAME}] Cannot apply no camera shake - camera shake modifier is not valid");
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