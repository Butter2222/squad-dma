using SkiaSharp;

namespace squad_dma
{
    /// <summary>
    /// Per-frame, render-owned copy of everything the radar needs from a <see cref="UActor"/>.
    /// Built once at the start of a frame so no draw routine touches live actor state.
    /// </summary>
    internal struct ActorFrameData
    {
        public UActor Actor;
        public ActorType Type;
        public Vector3D Position;   // raw (pre-AbsoluteLocation) position, single locked read
        public double RotationX;
        public float Health;
        public int TeamID;
        public bool IsAlive;
        public bool IsFriendly;
        public bool IsEnemy;
        public bool IsInMySquad;
        public bool IsVehicle;
        public SKColor MarkerColor; // resolved squad / friendly / enemy color
    }

    /// <summary>
    /// Reusable per-frame snapshot of all actors. One enumeration of the live actor dictionary
    /// per frame; every pass (radar, aimview) then reads from this buffer.
    /// </summary>
    internal sealed class ActorFrameSnapshot
    {
        // Reused every frame: eliminates the per-frame List/array allocations the old multi-pass LINQ produced.
        private readonly List<ActorFrameData> _items = new(512);

        public List<ActorFrameData> Items => _items;
        public UActor LocalPlayer { get; private set; }

        public void Build(IEnumerable<KeyValuePair<ulong, UActor>> actors, UActor localPlayer)
        {
            _items.Clear();
            LocalPlayer = localPlayer;
            if (actors is null)
                return;

            int localSquad = localPlayer?.SquadID ?? -1;

            foreach (var kvp in actors)
            {
                var actor = kvp.Value;
                if (actor is null)
                    continue;

                // Position takes a lock and copies a 24-byte struct per access; read it exactly once per frame.
                var pos = actor.Position;
                bool friendly = actor.IsFriendly();
                bool enemy = actor.IsEnemy();
                bool squad = friendly && actor.SquadID != -1 && actor.SquadID == localSquad;
                var type = actor.ActorType;

                _items.Add(new ActorFrameData
                {
                    Actor = actor,
                    Type = type,
                    Position = pos,
                    RotationX = actor.Rotation.X,
                    Health = actor.Health,
                    TeamID = actor.TeamID,
                    IsAlive = actor.IsAlive,
                    IsFriendly = friendly,
                    IsEnemy = enemy,
                    IsInMySquad = squad,
                    IsVehicle = Names.Vehicles.Contains(type),
                    // Same precedence as Extensions.GetActorPaint, resolved once instead of cloning a paint per draw.
                    MarkerColor = squad ? SKPaints.Squad
                                : friendly ? SKPaints.Friendly
                                : type == ActorType.Player ? SKPaints.EnemyPlayer
                                : SKPaints.Enemy
                });
            }
        }
    }

    /// <summary>
    /// "NNNm" labels keyed by whole meters. A label only changes when the rounded distance changes,
    /// so the string is built once per distinct value instead of once per entity per frame.
    /// </summary>
    internal static class DistanceLabels
    {
        private const int MaxCached = 4096;
        private static readonly string[] _cache = new string[MaxCached];

        public static string ForMeters(int meters)
        {
            if ((uint)meters >= MaxCached)
                return $"{meters}m";
            return _cache[meters] ??= $"{meters}m";
        }
    }
}
