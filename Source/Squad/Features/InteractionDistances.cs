using System;
using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    public class InteractionDistances : StateAwareFeature
    {
        public const string NAME = "InteractionDistances";

        private float _originalUseInteractDistance = 0.0f;
        private float _originalInteractableRadiusMultiplier = 0.0f;

        public InteractionDistances(ulong playerController, bool inGame, Game game = null)
            : base(playerController, inGame, game, NAME)
        {
        }

        protected override bool ShouldApplyModifications()
        {
            return Program.Config.SetInteractionDistances;
        }

        protected override void LoadOriginalValues()
        {
            SafeLoadOriginals(() =>
            {
                _originalUseInteractDistance = Memory.ReadValue<float>(_cachedSoldierActor + ASQSoldier.UseInteractDistance);
                _originalInteractableRadiusMultiplier = Memory.ReadValue<float>(_cachedSoldierActor + ASQSoldier.InteractableRadiusMultiplier);
                Logger.Debug($"[{_featureName}] Original UseInteractDistance={_originalUseInteractDistance}, InteractableRadiusMultiplier={_originalInteractableRadiusMultiplier}");
            }, "interaction distances");
        }

        protected override void ApplyModifications()
        {
            SafeApplyModifications(() =>
            {
                Memory.WriteValue<float>(_cachedSoldierActor + ASQSoldier.UseInteractDistance, 5000.0f);
                Memory.WriteValue<float>(_cachedSoldierActor + ASQSoldier.InteractableRadiusMultiplier, 1000.0f);
            }, "interaction distances");
        }

        protected override void RestoreOriginalValues()
        {
            SafeRestoreValues(() =>
            {
                Memory.WriteValue<float>(_cachedSoldierActor + ASQSoldier.UseInteractDistance, _originalUseInteractDistance);
                Memory.WriteValue<float>(_cachedSoldierActor + ASQSoldier.InteractableRadiusMultiplier, _originalInteractableRadiusMultiplier);
            }, "interaction distances");
        }
    }
}
