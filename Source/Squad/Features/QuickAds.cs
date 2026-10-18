using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class QuickAds : Manager, Weapon
    {
        public const string NAME = "QuickAds";
        private const float FAST_ADS_TIME = 0.01f;

        private bool _isEnabled = false;
        private ulong _appliedWeapon = 0;
        private float _originalAdsTime = 0.0f;

        public bool IsEnabled => _isEnabled;

        public QuickAds(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }

        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid())
            {
                Logger.Error($"[{NAME}] Cannot enable/disable quick ADS - local player is not valid");
                return;
            }

            _isEnabled = enable;
            Logger.Debug($"[{NAME}] Quick ADS {(enable ? "enabled" : "disabled")}");

            if (!enable)
            {
                RestoreWeapon();
                return;
            }

            UpdateCachedPointers();
            ulong weapon = IsInVehicle() && _cachedVehicleWeapon != 0 ? _cachedVehicleWeapon : _cachedCurrentWeapon;
            if (weapon != 0)
                ApplyWeapon(weapon);
        }

        public void OnWeaponChanged(ulong newWeapon, ulong oldWeapon)
        {
            if (!IsLocalPlayerValid()) return;

            RestoreWeapon();

            if (_isEnabled && newWeapon != 0)
                ApplyWeapon(newWeapon);
        }

        // CalculatedADSTime is derived by the game, so it is rewritten every tick while a weapon is tracked
        public override void Apply()
        {
            if (_isEnabled && _appliedWeapon != 0)
                EnqueueWrite<float>(_appliedWeapon + ASQWeapon.CalculatedADSTime, FAST_ADS_TIME);
        }

        private void ApplyWeapon(ulong weapon)
        {
            if (weapon == _appliedWeapon || IsGrenade(weapon)) return;

            try
            {
                RestoreWeapon();

                _originalAdsTime = Memory.ReadValue<float>(weapon + ASQWeapon.CalculatedADSTime);
                _appliedWeapon = weapon;
                Logger.Debug($"[{NAME}] Tracking weapon at 0x{weapon:X}, original ADS time {_originalAdsTime}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error applying quick ADS to weapon at 0x{weapon:X}: {ex.Message}");
            }
        }

        private void RestoreWeapon()
        {
            if (_appliedWeapon == 0) return;

            ulong weapon = _appliedWeapon;
            _appliedWeapon = 0;

            try
            {
                Memory.WriteValue<float>(weapon + ASQWeapon.CalculatedADSTime, _originalAdsTime);
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error restoring weapon at 0x{weapon:X}: {ex.Message}");
            }
        }
    }
}
