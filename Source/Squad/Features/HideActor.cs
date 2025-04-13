using Offsets;

namespace squad_dma.Source.Squad.Features
{
    // Delete later
    // Merge all my shit into AirStuck.
    // This feature is useless

    public class HideActor : Manager
    {
        public bool _isHideActorEnabled = false;
        
        public HideActor(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
                
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isHideActorEnabled = enable;
            Apply();
        }
        
        public override void Apply()
        {
            try
            {
                if (!IsLocalPlayerValid()) return;
                
                UpdateCachedPointers();
                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0) return;

                if (_isHideActorEnabled)
                {
                    Memory.WriteValue<byte>(soldierActor + 0x5b, 0);
                    Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, 0);
                    Memory.WriteValue<byte>(soldierActor + Actor.bHidden, 1);
                }
                else
                {
                    Memory.WriteValue<byte>(soldierActor + 0x5b, 1);
                    Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, 16);
                    Memory.WriteValue<byte>(soldierActor + Actor.bHidden, 16);
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting hide actor: {ex.Message}");
            }
        }
    }
} 