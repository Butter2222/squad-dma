using SkiaSharp;
using System.Diagnostics;
using System.Numerics;

namespace squad_dma
{
    public struct Vector3D
    {
        public double X;
        public double Y;
        public double Z;

        public Vector3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vector3D Zero => new Vector3D(0, 0, 0);
    }

    public struct Vector2D
    {
        public double X;
        public double Y;

        public Vector2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static Vector2D Zero => new Vector2D(0, 0);
    }

    /// <summary>
    /// Class containing Game Player Data.
    /// </summary>
    public class UActor
    {
        private readonly object _posLock = new(); // sync access to this.Position (non-atomic)
        private Vector3D _previousPosition = Vector3D.Zero; // Track previous position for movement

        #region PlayerProperties
        public uint NameId { get; set; }
        public string Name { get; set; }
        public float Health { get; set; } = -1;
        public int TeamID { get; set; } = -1;
        public int SquadID { get; set; } = -1;
        public Team Team { get; set; } = Team.Unknown;

        public bool IsFriendly()
        {
            if (TeamID == -1) return false; 

            var localPlayer = Memory.LocalPlayer;
            if (localPlayer == null) return false;

            if (localPlayer.TeamID != -1 && this.TeamID != -1)
                return localPlayer.TeamID == this.TeamID;

            return localPlayer.Team == this.Team;
        }

        public bool IsInMySquad()
        {
            return IsFriendly() &&
                   SquadID != -1 &&
                   SquadID == Memory.LocalPlayer?.SquadID;
        }

        public bool bInThirdPersonView { get; set; } = false;
        public Vector3D CameraOffset { get; set; } = new Vector3D(0, -300, 50); // Default third-person offset
        public double CameraDistance { get; set; } = 300.0;

        // Add this method to control third-person view
        public void UpdateThirdPersonView(ulong pawnPtr)
        {
            try
            {
                if (pawnPtr == 0) return;

                // Read the current third-person status from memory
                bInThirdPersonView = Memory.ReadValue<bool>(pawnPtr + 0x1654);

                // If in third-person, read camera settings
                if (bInThirdPersonView)
                {
                    CameraOffset = Memory.ReadValue<Vector3D>(pawnPtr + 0x21D0);
                    CameraDistance = Memory.ReadValue<double>(pawnPtr + 0x21DC);
                }
            }
            catch { /* Silently handle errors */ }
        }

        public ActorType ActorType { get; set; } = ActorType.Player;
        private Vector3D _pos = new Vector3D(0, 0, 0);
        public Vector3D Position // 192 bits, cannot set atomically
        {
            get
            {
                lock (_posLock)
                {
                    return _pos;
                }
            }
            set
            {
                lock (_posLock)
                {
                    _previousPosition = _pos; // Store the previous position before updating
                    _pos = value;
                }
            }
        }
        public Vector2D ZoomedPosition { get; set; } = new Vector2D(0, 0);
        public Vector2D Rotation { get; set; } = new Vector2D(0, 0); // 128 bits will be atomic
        public Vector3D Rotation3D { get; set; } = new Vector3D(0, 0, 0);
        public int ErrorCount { get; set; } = 0;

        public Vector3D DeathPosition { get; set; } = Vector3D.Zero; 
        public DateTime TimeOfDeath { get; set; } = DateTime.MinValue;

        #endregion

        #region Getters
        public ulong Base { get; }
        public bool IsAlive => Health > 0.0;
        #endregion

        #region Constructor
        public UActor(ulong actorBase)
        {
            Debug.WriteLine("Actor Constructor: Initialization started.");
            this.Base = actorBase;
        }
        #endregion
    }
}
