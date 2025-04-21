using System;
using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class InteractionDistances : Manager
    {
        public const string NAME = "InteractionDistances";
        private bool _isEnabled = false;
        
        public bool IsEnabled => _isEnabled;
        
        public InteractionDistances(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid())
            {
                Logger.Error($"[{NAME}] Cannot enable/disable interaction distances - local player is not valid");
                return;
            }
            
            _isEnabled = enable;
            Logger.Debug($"[{NAME}] Interaction distances {(enable ? "enabled" : "disabled")}");
            Apply();
        }
        
        public override void Apply()
        {
            try
            {
                if (!IsLocalPlayerValid())
                {
                    Logger.Error($"[{NAME}] Cannot apply interaction distances - local player is not valid");
                    return;
                }
                
                UpdateCachedPointers();
                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0)
                {
                    Logger.Error($"[{NAME}] Cannot apply interaction distances - soldier actor is not valid");
                    return;
                }

                if (Program.Config.SetInteractionDistances)
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UseInteractDistance, 5000.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier, 70.0f);
                    Logger.Debug($"[{NAME}] Set interaction distances to extended values (5000.0f, 70.0f)");
                }
                else
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UseInteractDistance, 220);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier, 1.2f);
                    Logger.Debug($"[{NAME}] Set interaction distances to default values (220, 1.2f)");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error setting interaction distances: {ex.Message}");
            }
        }
    }
} 