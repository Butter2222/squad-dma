using System.Numerics;

namespace squad_dma
{
    /// <summary>Screen-space rectangle in normalized device coordinates (-1..1, +Y up).</summary>
    internal struct NdcRect
    {
        public float MinX, MinY, MaxX, MaxY;

        public float Width => MaxX - MinX;
        public float Height => MaxY - MinY;
    }

    /// <summary>
    /// Decides per target whether to draw a box or a small marker. A box is only worth drawing once it is big
    /// enough to read, so far targets get a marker instead. Two thresholds (enter / exit) keep a target from
    /// flickering between the two while it sits near the limit, and a target the crosshair is on is always a box.
    /// One instance per drawing surface, since each has its own pixel sizes.
    /// </summary>
    internal sealed class TargetLod
    {
        private struct State { public bool Box; public int Frame; }

        private readonly Dictionary<ulong, State> _states = new();
        private readonly List<ulong> _stale = new();
        private int _frame;

        public void BeginFrame()
        {
            _frame++;
            if (_states.Count < 256 || (_frame & 127) != 0)
                return;

            _stale.Clear();
            foreach (var kv in _states)
                if (_frame - kv.Value.Frame > 240)
                    _stale.Add(kv.Key);
            foreach (var key in _stale)
                _states.Remove(key);
        }

        /// <param name="size">Size of the target's box in pixels (height for players, longest side for vehicles).</param>
        /// <param name="enterBox">Size at which a marker becomes a box.</param>
        /// <param name="exitBox">Size below which a box falls back to a marker.</param>
        public bool UseBox(ulong key, float size, float enterBox, float exitBox, bool aimedAt)
        {
            _states.TryGetValue(key, out var state);
            bool box = aimedAt || size >= (state.Box ? exitBox : enterBox);
            _states[key] = new State { Box = box, Frame = _frame };
            return box;
        }

        /// <summary>True when the point is inside the rectangle grown by <paramref name="pad"/> on every side.</summary>
        public static bool IsAimedAt(float left, float top, float right, float bottom, float pointX, float pointY, float pad)
            => pointX >= left - pad && pointX <= right + pad && pointY >= top - pad && pointY <= bottom + pad;
    }

    /// <summary>
    /// Boxes shared by the overlay and the aimview, built from the real size of the actor: the player capsule
    /// and the vehicle mesh bounds. Output is in NDC so each surface maps it onto its own canvas.
    /// </summary>
    internal static class EspGeometry
    {
        // Standing capsule, used until the first capsule read arrives
        private const float DefaultHalfHeight = 88f;
        private const float DefaultRadius = 34f;

        // Half extents (forward, right, up) in cm for vehicles whose mesh bounds have not been read yet
        private static readonly Vector3 SmallExtents = new(130f, 50f, 80f);
        private static readonly Vector3 MediumExtents = new(250f, 110f, 110f);
        private static readonly Vector3 LargeExtents = new(430f, 150f, 150f);
        private static readonly Vector3 ExtraLargeExtents = new(520f, 190f, 140f);

        private static readonly Dictionary<ActorType, Vector3> FallbackExtents = new()
        {
            { ActorType.Motorcycle, SmallExtents },

            { ActorType.JeepTransport, MediumExtents }, { ActorType.JeepLogistics, MediumExtents },
            { ActorType.JeepTurret, MediumExtents }, { ActorType.JeepArtillery, MediumExtents },
            { ActorType.JeepAntitank, MediumExtents }, { ActorType.JeepAntiAir, MediumExtents },
            { ActorType.JeepRWSTurret, MediumExtents }, { ActorType.Boat, MediumExtents },
            { ActorType.BoatLogistics, MediumExtents }, { ActorType.TrackedJeep, MediumExtents },
            { ActorType.LoachCAS, MediumExtents }, { ActorType.LoachScout, MediumExtents },

            { ActorType.TruckTransport, LargeExtents }, { ActorType.TruckLogistics, LargeExtents },
            { ActorType.TruckTransportArmed, LargeExtents }, { ActorType.TruckArtillery, LargeExtents },
            { ActorType.TruckAntiAir, LargeExtents }, { ActorType.APC, LargeExtents },
            { ActorType.TrackedAPC, LargeExtents }, { ActorType.TrackedAPCArtillery, LargeExtents },
            { ActorType.IFV, LargeExtents }, { ActorType.TrackedIFV, LargeExtents },
            { ActorType.TrackedLogistics, LargeExtents }, { ActorType.TransportHelicopter, LargeExtents },

            { ActorType.Tank, ExtraLargeExtents }, { ActorType.TankMGS, ExtraLargeExtents },
            { ActorType.AttackHelicopter, ExtraLargeExtents },
        };

        public static bool TryPlayerRect(UActor actor, in Vector3D position, bool optic, out NdcRect rect)
        {
            rect = default;

            float halfHeight = actor.CapsuleHalfHeight > 0f ? actor.CapsuleHalfHeight : DefaultHalfHeight;
            float radius = actor.CapsuleRadius > 0f ? actor.CapsuleRadius : DefaultRadius;

            var top = new Vector3D(position.X, position.Y, position.Z + halfHeight);
            var bottom = new Vector3D(position.X, position.Y, position.Z - halfHeight);

            if (!Camera.TryProject(top, optic, out var topNdc, out var topW) ||
                !Camera.TryProject(bottom, optic, out var bottomNdc, out var bottomW))
                return false;

            // Lateral size of the capsule at its depth: radius * focal scale / depth
            float depth = (topW + bottomW) * 0.5f;
            float halfWidth = radius * Camera.LateralScale(optic) / depth;
            float centerX = (topNdc.X + bottomNdc.X) * 0.5f;

            rect = new NdcRect
            {
                MinX = centerX - halfWidth,
                MaxX = centerX + halfWidth,
                MinY = Math.Min(topNdc.Y, bottomNdc.Y),
                MaxY = Math.Max(topNdc.Y, bottomNdc.Y),
            };
            return true;
        }

        public static bool TryVehicleRect(UActor actor, in Vector3D position, bool optic, out NdcRect rect)
        {
            rect = default;

            Span<Vector2> corners = stackalloc Vector2[8];
            if (!TryVehicleCorners(actor, position, optic, corners))
                return false;

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                if (corners[i].X < minX) minX = corners[i].X;
                if (corners[i].X > maxX) maxX = corners[i].X;
                if (corners[i].Y < minY) minY = corners[i].Y;
                if (corners[i].Y > maxY) maxY = corners[i].Y;
            }

            rect = new NdcRect { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
            return true;
        }

        /// <summary>
        /// The eight corners of the vehicle's oriented bounding box in NDC. Corner i uses bit 0 for forward,
        /// bit 1 for right and bit 2 for up (set = positive side), so two corners share an edge when their indices differ by one bit.
        /// </summary>
        public static bool TryVehicleCorners(UActor actor, in Vector3D position, bool optic, Span<Vector2> ndcCorners)
        {
            Vector3 origin = Vector3.Zero;
            Vector3 extents;
            if (actor.HasVehicleBounds)
            {
                origin = actor.VehicleBoundsOrigin;
                extents = actor.VehicleHalfExtents;
            }
            else if (!FallbackExtents.TryGetValue(actor.ActorType, out extents))
            {
                extents = MediumExtents;
            }

            var rot = actor.Rotation3D;
            GetAxes(rot.X, rot.Y, rot.Z, out var forward, out var right, out var up);

            for (int i = 0; i < 8; i++)
            {
                float lx = origin.X + ((i & 1) == 0 ? -extents.X : extents.X);
                float ly = origin.Y + ((i & 2) == 0 ? -extents.Y : extents.Y);
                float lz = origin.Z + ((i & 4) == 0 ? -extents.Z : extents.Z);

                var corner = new Vector3D(
                    position.X + lx * forward.X + ly * right.X + lz * up.X,
                    position.Y + lx * forward.Y + ly * right.Y + lz * up.Y,
                    position.Z + lx * forward.Z + ly * right.Z + lz * up.Z);

                // A corner behind the camera has no screen position; the box is dropped rather than drawn wrong.
                if (!Camera.TryProject(corner, optic, out var ndc, out _))
                    return false;

                ndcCorners[i] = ndc;
            }
            return true;
        }

        // FRotationMatrix axes for a pitch / yaw / roll in degrees
        private static void GetAxes(double pitchDeg, double yawDeg, double rollDeg, out Vector3 forward, out Vector3 right, out Vector3 up)
        {
            float sp = (float)Math.Sin(pitchDeg * Math.PI / 180.0), cp = (float)Math.Cos(pitchDeg * Math.PI / 180.0);
            float sy = (float)Math.Sin(yawDeg * Math.PI / 180.0), cy = (float)Math.Cos(yawDeg * Math.PI / 180.0);
            float sr = (float)Math.Sin(rollDeg * Math.PI / 180.0), cr = (float)Math.Cos(rollDeg * Math.PI / 180.0);

            forward = new Vector3(cp * cy, cp * sy, sp);
            right = new Vector3(sr * sp * cy - cr * sy, sr * sp * sy + cr * cy, -sr * cp);
            up = new Vector3(-(cr * sp * cy + sr * sy), cy * sr - cr * sp * sy, cr * cp);
        }
    }
}
