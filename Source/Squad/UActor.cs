using SkiaSharp;
using System.Diagnostics;
using System.Numerics;

namespace squad_dma
{
    /// <summary>
    /// Class containing Game Player Data.
    /// </summary>
    public class UActor
    {
        private readonly object _posLock = new(); // sync access to this.Position (non-atomic)
        private Vector3 _previousPosition = Vector3.Zero; // Track previous position for movement

        #region PlayerProperties
        public uint NameId { get; set; }
        public string Name { get; set; }
        public float Health { get; set; } = -1;
        public int TeamID { get; set; } = -1;
        public int SquadID { get; set; } = -1;
        public List<UActor> MySquadMembers { get; } = new List<UActor>();
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

        public ActorType ActorType { get; set; } = ActorType.Player;
        private Vector3 _pos = new Vector3(0, 0, 0);
        public Vector3 Position // 96 bits, cannot set atomically
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
        public Vector2 ZoomedPosition { get; set; } = new();
        public Vector2 Rotation { get; set; } = new Vector2(0, 0); // 64 bits will be atomic
        public Vector3 Rotation3D { get; set; } = new Vector3(0, 0, 0);
        public int ErrorCount { get; set; } = 0;

        public Vector3 DeathPosition { get; set; } = Vector3.Zero; 
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
