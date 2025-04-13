using System.Numerics;
using Offsets;

namespace squad_dma.Source.Squad.Features
{
    // Works but buggy
    // Game is constantly trying to reset the camera position
    // Dont know of a fix
    public class ThirdPerson : Manager
    {
        private Vector3 _smoothedCameraOffset;
        private float _cameraDistance = 400f;
        private float _cameraHeight = 100f;
        public bool _isThirdPersonEnabled = false;

        public ThirdPerson(ulong playerController, bool inGame) : base(playerController, inGame)
        {
        }

        public void SetEnabled(bool enable, float distance = 400f, float height = 100f)
        {
            _isThirdPersonEnabled = enable;
            _cameraDistance = distance;
            _cameraHeight = height;

            if (!_inGame || _playerController == 0)
                return;

            try
            {
                ulong pawnPtr = Memory.ReadPtr(_playerController + Controller.Pawn);
                if (pawnPtr == 0) return;

                bool currentMode = Memory.ReadValue<bool>(pawnPtr + 0x1654);
                if (currentMode != enable)
                {
                    Memory.WriteValue<bool>(pawnPtr + 0x1654, enable);
                }

                if (enable)
                {
                    Vector3 controlRot = Memory.ReadValue<Vector3>(pawnPtr + 0x2168);
                    float yawRad = controlRot.Y * (MathF.PI / 180f);

                    Vector3 idealOffset = new Vector3(
                        -distance * MathF.Cos(yawRad),
                        -distance * MathF.Sin(yawRad),
                        height
                    );

                    Vector3 currentOffset = Memory.ReadValue<Vector3>(pawnPtr + 0x21D0);
                    Vector3 smoothedOffset = Vector3.Lerp(currentOffset, idealOffset, 0.3f);

                    Memory.WriteValue<Vector3>(pawnPtr + 0x21D0, smoothedOffset);
                    Memory.WriteValue<float>(pawnPtr + 0x21DC, distance);

                    Memory.WriteValue<int>(pawnPtr + 0x21CD, 4); // DebugCameraFollowCharacter

                    Memory.WriteValue<bool>(pawnPtr + 0x2200, false);
                }
                else
                {
                    // Reset to first-person
                    Memory.WriteValue<float>(pawnPtr + 0x21DC, 0);
                    Memory.WriteValue<Vector3>(pawnPtr + 0x21D0, Vector3.Zero);
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Camera error: {ex.Message}");
            }
        }

        public override void Apply()
        {
            if (!_isThirdPersonEnabled || !_inGame || _playerController == 0)
                return;

            try
            {
                ulong pawnPtr = Memory.ReadPtr(_playerController + Controller.Pawn);
                if (pawnPtr == 0) return;

                Memory.WriteValue<int>(pawnPtr + 0x21CD, 4); // DebugCameraFollowCharacter
                Memory.WriteValue<bool>(pawnPtr + 0x1654, true);

                Vector3 controlRot = Memory.ReadValue<Vector3>(pawnPtr + 0x2168); // ControlRotation

                float yawRad = controlRot.Y * (MathF.PI / 180f);
                Vector3 idealOffset = new Vector3(
                    -_cameraDistance * MathF.Cos(yawRad),
                    -_cameraDistance * MathF.Sin(yawRad),
                    _cameraHeight
                );

                _smoothedCameraOffset = Vector3.Lerp(_smoothedCameraOffset, idealOffset, 0.1f);

                Memory.WriteValue<Vector3>(pawnPtr + 0x21D0, _smoothedCameraOffset);
                Memory.WriteValue<float>(pawnPtr + 0x21DC, _cameraDistance);

                Memory.WriteValue<Vector3>(pawnPtr + 0x21E0, controlRot);
            }
            catch (Exception ex)
            {
                Program.Log($"Camera update error: {ex.Message}");
            }
        }
    }
} 