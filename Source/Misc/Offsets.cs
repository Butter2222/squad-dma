namespace Offsets
{
    public struct GameObjects
    {
        public const uint GObjects = 0xc8bb960;
        public const uint GNames = 0xC7D7A00;
        public const uint GWorld = 0xCA44D68;
    }

    public struct World
    {
        public const uint PersistentLevel = 0x30;
        public const uint AuthorityGameMode = 0x158;
        public const uint GameState = 0x160;
        public const uint Levels = 0x178;
        public const uint OwningGameInstance = 0x1E0;
        public const uint WorldOrigin = 0x5B8; // 0x5B8 or 0x5C4
    }

    public struct GameInstance
    {
        public const uint LocalPlayers = 0x38;
        public const uint CurrentLayer = 0x640;
    }

    public struct SQLayer
    {
        public const uint LevelID = 0x68;
    }

    public struct Level // ULevel
    {
        public const uint Actors = 0xA0; // TArray<AActor*>
    }

    public struct Actor
    {
        public const uint Instigator = 0x1A8;
        public const uint RootComponent = 0x1C0;
        public const uint ID = 0x18; // Object ID
        public const uint CustomTimeDilation = 0x68; // float
        public const uint bReplicateMovement = 0x58; // uint8
        public const uint bHidden = 0x58; // uint8
        public const uint bActorEnableCollision = 0x5c; // uint8
    }

    public struct USceneComponent
    {
        public const uint RelativeLocation = 0x128;
        public const uint RelativeRotation = 0x140;
        public const uint RelativeScale3D = 0x158;
        public const uint ComponentToWorld = 0x1E0;
    }

    public struct UPrimitiveComponent
    {
        public const uint BodyInstance = 0x358; // FBodyInstance
    }

    public struct FBodyInstance
    {
        public const uint CollisionEnabled = 0x17; // uint8
    }

    public struct UPlayer
    {
        public const uint PlayerController = 0x30;
    }

    public struct ULocalPlayer
    {
        public const uint ViewportClient = 0x78;
    }

    public struct Pawn
    {
        public const uint PlayerState = 0x2D0;
        public const uint Controller = 0x2E0;
    }

    public struct Controller
    {
        public const uint PlayerState = 0x2B8;
        public const uint Pawn = 0x300;
        public const uint Character = 0x310;
    }

    public struct PlayerController
    {
        public const uint Player = 0x368;
        public const uint AcknowledgedPawn = 0x370;
        public const uint PlayerCameraManager = 0x380;
    }

    public struct SQPlayerController
    {
        public const uint TeamState = 0x890; // ASQTeamState*
        public const uint SquadState = 0x8A8; // ASQSquadState*
    }

    public struct PlayerCameraManager
    {
        public const uint PCOwner = 0x2B0;
        public const uint DefaultFOV = 0x2C8;
        public const uint ViewTarget = 0x340;
        public const uint ModifierList = 0x2520; // TArray<UCameraModifier*>
        public const uint CachedCameraShakeMod = 0x25B8; // UCameraModifier_CameraShake*
    }

    public struct FTViewTarget
    {
        public const uint POV = 0x10; // FMinimalViewInfo
    }

    public struct UCameraShakeBase
    {
        public const uint bSingleInstance = 0x28; // bool
        public const uint ShakeScale = 0x2c; // float
        public const uint RootShakePattern = 0x30; // UCameraShakePattern*
        public const uint CameraManager = 0x38; // APlayerCameraManager*
    }
    public struct UCameraModifier_CameraShake
    {
        public const uint ActiveShakes = 0x48; // TArray<FActiveCameraShakeInfo>
        public const uint ExpiredPooledShakesMap = 0x58; // TMap<TSubclassOf<UCameraShakeBase*>, FPooledCameraShakes>
        public const uint SplitScreenShakeScale = 0xa8; // float
    }

    public struct AGameStateBase
    {
        public const uint PlayerArray = 0x2C8;
    }

    public struct ASQGameState
    {
        public const uint TeamStates = 0x3E0;
        public const uint WinningTeam = 0x408; // ASQTeamState*
    }

    public struct APlayerState
    {
        public const uint PawnPrivate = 0x330;
    }

    public struct ASQPlayerState
    {
        public const uint TeamID = 0x4E8; // per player
        public const uint SquadState = 0x7C8; // ASQSquadState*
        public const uint PlayerStateData = 0x710;
        public const uint Soldier = 0x7E8; // ASQSoldier*
        public const uint CurrentSeat = 0x7A8; // USQVehicleSeatComponent*
    }

    public struct ASQTeamState
    {
        public const uint Tickets = 0x2B0;
        public const uint ID = 0x2E0; // global | Team ID (0, 1, 2)
    }

    public struct ASQSquadState
    {
        public const uint SquadId = 0x320; // int32
        public const uint TeamId = 0x324; // int32
        public const uint PlayerStates = 0x328; // TArray<ASQPlayerState*>
        public const uint LeaderState = 0x338; // ASQPlayerState*
        public const uint AuthoritySquad = 0x2B0;
    }

    public struct FPlayerStateDataObject
    {
        public const uint NumKills = 0x4; // int32
        public const uint NumWoundeds = 0x10; // int32
    }

    public struct ASQSoldier
    {
        public const uint Health = 0x26D0; // float
        public const uint UnderSuppressionPercentage = 0x1D44; // float
        public const uint MaxSuppressionPercentage = 0x1D48; // float
        public const uint SuppressionMultiplier = 0x1D50; // float
        public const uint UseInteractDistance = 0x1EEC; // float
        public const uint InteractableRadiusMultiplier = 0x1F08; // float
        public const uint InventoryComponent = 0x2A30; // USQPawnInventoryComponent*
        public const uint CurrentItemStaticInfo = 0x2A58; // USQItemStaticInfo*
        public const uint bUsableInMainBase = 0x788; // bool
        public const uint bIsCameraRecoilActive = 0x2ADA; // bool
        public const uint WeaponBasedFOV = 0x7B0; // Float
        public const uint CachedAnimInstance1p = 0x2BF0; // USQAnimInstanceSoldier1P*
        public const uint Mesh = 0x330; // USkeletalMeshComponent* ACharacter
    }

    public struct USQPawnInventoryComponent
    {
        public const uint CurrentWeapon = 0x1A0; // ASQEquipableItem*
        public const uint CurrentItemStaticInfo = 0x190; // USQItemStaticInfo*
        public const uint CurrentWeaponSlot = 0x1E4; // int32
        public const uint CurrentWeaponOffset = 0x1E8; // int32
        public const uint Inventory = 0x1F0; // TArray<FSQWeaponGroupData>
    }

    public struct ASQWeapon
    {
        public const uint WeaponConfig = 0x750; // FSQWeaponData
        public const uint CurrentFireMode = 0x878; // int32
        public const uint bAimingDownSights = 0x834; // bool
        public const uint CachedPipScope = 0x828; // USQPipScopeCaptureComponent*
        public const uint CurrentFOV = 0x92C; // float
        public const uint bFireInput = 0x835; // bool
        public const uint WeaponStaticInfo = 0x590; // USQWeaponStaticInfo*
        public const uint CurrentState = 0x820; // ESQWeaponState
    }

    public struct FSQWeaponData
    {
        public const uint bInfiniteAmmo = 0x0; // bool
        public const uint bInfiniteMags = 0x1; // bool
        public const uint TimeBetweenShots = 0x20; // float
        public const uint TimeBetweenSingleShots = 0x24; // float
        public const uint bCreateProjectileOnServer = 0x29; // bool
        public const uint FireModes = 0x10; // TArray<int32>
    }

    public struct ASQEquipableItem
    {
        public const uint ItemStaticInfo = 0x2B0; // USQItemStaticInfo*
        public const uint ItemStaticInfoClass = 0x2B8; // TSubclassOf<USQItemStaticInfo*>
        public const uint DisplayName = 0x340; // FText
        public const uint ItemCount = 0x40C; // int32
        public const uint MaxItemCount = 0x410; // int32
        public const uint EquipDuration = 0x42C; // float
        public const uint UnequipDuration = 0x430; // float
        public const uint CachedEquipDuration = 0x548; // float
        public const uint CachedUnequipDuration = 0x54C; // float
    }

    public struct FSQSwayData
    {
        public const uint UnclampedTotalSway = 0x80; // float
        public const uint TotalSway = 0x84; // float
        public const uint Sway = 0x88; // FRotator
    }
    public struct USQPipScopeCaptureComponent
    {
        public const uint CurrentMagnificationLevel = 0x960; // int32

    }

    public struct ASQGrenade
    {
        public const uint GrenadeConfig = 0x580; // FSQGrenadeData
        public const uint GrenadeStaticInfo = 0x5E0; // USQGrenadeStaticInfo*
    }

    public struct USQGrenadeStaticInfo
    {
        public const uint WeaponOverhandPinpull1pMontage = 0x7C0; // UAnimMontage*
        public const uint WeaponOverhandPinpull3pMontage = 0x7C8; // UAnimMontage*
        public const uint OverhandPinpull1pMontage = 0x7D0; // UAnimMontage*
        public const uint OverhandPinpull3pMontage = 0x7D8; // UAnimMontage*
        public const uint WeaponOverhandThrow1pMontage = 0x7E0; // UAnimMontage*
        public const uint WeaponOverhandThrow3pMontage = 0x7E8; // UAnimMontage*
        public const uint OverhandThrow1pMontage = 0x7F0; // UAnimMontage*
        public const uint OverhandThrow3pMontage = 0x7F8; // UAnimMontage*
        public const uint WeaponUnderhandPinpull1pMontage = 0x800; // UAnimMontage*
        public const uint WeaponUnderhandPinpull3pMontage = 0x808; // UAnimMontage*
        public const uint UnderhandPinpull1pMontage = 0x810; // UAnimMontage*
        public const uint UnderhandPinpull3pMontage = 0x818; // UAnimMontage*
        public const uint WeaponUnderhandThrow1pMontage = 0x820; // UAnimMontage*
        public const uint WeaponUnderhandThrow3pMontage = 0x828; // UAnimMontage*
        public const uint UnderhandThrow1pMontage = 0x830; // UAnimMontage*
        public const uint UnderhandThrow3pMontage = 0x838; // UAnimMontage*
    }

    public struct FSQGrenadeData
    {
        public const uint bInfiniteAmmo = 0x0;  // bool
        public const uint ThrowReadyTime = 0x14; // float
        public const uint OverhandThrowTime = 0x18; // float
        public const uint UnderhandThrowTime = 0x1C; // float
        public const uint OverhandThrowDuration = 0x20; // float
        public const uint UnderhandThrowDuration = 0x24; // float
        public const uint ThrowModeTransitionTime = 0x28; // float
        public const uint ReloadTime = 0x34; // float
    }

    public struct SQVehicle
    {
        public const uint ClaimedBySquad = 0x660; // ASQSquadState*
        public const uint Health = 0x9A0; // float
        public const uint MaxHealth = 0x9A4; // float

    }

    public struct FSQUsableData
    {
        public const uint DisplayName = 0x0; // FText
        public const uint InteractionData = 0x10; // TArray<FSQUsableWidgetData>
        public const uint InteractWidgetClass = 0x20; // TSubclassOf<USQInteractableWidgetList*>
        public const uint DrawLocationOffset = 0x28; // FVector
        public const uint InteractVisiblity = 0x40; // ESQInteractVisibility
    }

    public struct SQDeployable
    {
        public const uint Health = 0x424;
        public const uint MaxHealth = 0x41C;
        public const uint Team = 0x2D8;
    }

    public struct FString
    {
        public const uint Length = 0x8;
    }

    public struct Character
    {
        public const uint CharacterMovement = 0x338; // UCharacterMovementComponent*
        public const uint ReplicatedMovementMode = 0x452; // uint8
    }

    public struct CharacterMovementComponent
    {
        public const uint MovementMode = 0x2C1; // Engine::EMovementMode
        public const uint MaxFlySpeed = 0x314; // float
        public const uint MaxCustomMovementSpeed = 0x318; // float
        public const uint MaxAcceleration = 0x31C; // float
    }

    public struct USQVehicleSeatComponent
    {
        public const uint SeatConfig = 0x240; // FSQVehicleSeatConfig
        public const uint AnimationSeatState = 0x2B8; // int32
        public const uint SeatPawn = 0x2C0; // ASQVehicleSeat*
        public const uint SeatedPlayer = 0x2C8; // ASQPlayerState*
        public const uint SeatedSoldier = 0x2D0; // ASQSoldier*
    }

    public struct ASQVehicleSeat
    {
        public const uint VehicleInventory = 0x4B0; // USQVehicleInventoryComponent*
    }

    public struct USQAnimInstanceSoldier1P
    {
        public const uint WeapRecoilRelLoc = 0xA20; // FVector
        public const uint MoveRecoilFactor = 0xD70; // float
        public const uint RecoilCanRelease = 0xD74; // float
        public const uint FinalRecoilSigma = 0xD78; // FVector
        public const uint FinalRecoilMean = 0xD90; // FVector
        public const uint MoveDeviationFactor = 0xD14; // float
        public const uint ShotDeviationFactor = 0xD18; // float
        public const uint FinalDeviation = 0xD20; // FVector4
        public const uint StandRecoilMean = 0x1000; // FVector
        public const uint StandRecoilSigma = 0x1018; // FVector
        public const uint CrouchRecoilMean = 0xFA0; // FVector
        public const uint CrouchRecoilSigma = 0xFB8; // FVector
        public const uint ProneRecoilMean = 0xF40; // FVector
        public const uint ProneRecoilSigma = 0xF58; // FVector
        public const uint BipodRecoilMean = 0x1090; // FVector
        public const uint BipodRecoilSigma = 0x10A8; // FVector
        public const uint ProneTransitionRecoilMean = 0x1030; // FVector
        public const uint ProneTransitionRecoilSigma = 0x1048; // FVector
        public const uint WeaponPunch = 0x12A0; // FRotator
        public const uint MoveSwayFactorMultiplier = 0x1234; // float
        public const uint SuppressSwayFactorMultiplier = 0x1238; // float
        public const uint WeaponPunchSwayCombinedRotator = 0x1240; // FRotator
        public const uint UnclampedTotalSway = 0x1330; // float
        public const uint SwayData = 0x10C0; // FSQSwayData
        public const uint SwayAlignmentData = 0x1178; // FSQSwayData
        public const uint AddMoveDeviation = 0xE78; // float
        public const uint MoveDeviationFactorRelease = 0xE7C; // float
        public const uint MaxMoveDeviationFactor = 0xE80; // float
        public const uint MinMoveDeviationFactor = 0xE84; // float
        public const uint FullStaminaDeviationFactor = 0xE88; // float
        public const uint LowStaminaDeviationFactor = 0xE8C; // float
        public const uint AddShotDeviationFactor = 0xE90; // float
        public const uint AddShotDeviationFactorAds = 0xE94; // float
        public const uint ShotDeviationFactorRelease = 0xE98; // float
        public const uint MinShotDeviationFactor = 0xE9C; // float
        public const uint MaxShotDeviationFactor = 0xEA0; // float
        public const uint MinBipodAdsDeviation = 0xEB0; // float
        public const uint MinBipodDeviation = 0xEB4; // float
        public const uint MinProneAdsDeviation = 0xEB8; // float
        public const uint MinProneDeviation = 0xEBC; // float
        public const uint MinCrouchAdsDeviation = 0xEC0; // float
        public const uint MinCrouchDeviation = 0xEC4; // float
        public const uint MinStandAdsDeviation = 0xEC8; // float
        public const uint MinStandDeviation = 0xECC; // float
        public const uint MinProneTransitionDeviation = 0xED0; // float
        public const uint FireShake = 0xE50; // TSubclassOf<UCameraShakeBase*>
    }

    public struct USQWeaponStaticInfo
    {
        public const uint bRequiresManualBolt = 0x10D1; // bool
        public const uint bRequireAdsToShoot = 0x1109; // bool
        public const uint RecoilCameraOffsetFactor = 0x9B0;  // float
        public const uint RecoilWeaponRelLocFactor = 0x9C8;  // float
        public const uint AddMoveRecoil = 0x9EC;  // float
        public const uint MaxMoveRecoilFactor = 0x9F0;  // float
        public const uint StandRecoilMean = 0xB68;  // FVector
        public const uint StandRecoilSigma = 0xB80;  // FVector
        public const uint StandAdsRecoilMean = 0xB38;  // FVector
        public const uint StandAdsRecoilSigma = 0xB50;  // FVector
        public const uint CrouchRecoilMean = 0xB00;  // FVector
        public const uint CrouchRecoilSigma = 0xB18;  // FVector
        public const uint CrouchAdsRecoilMean = 0xAD0;  // FVector
        public const uint CrouchAdsRecoilSigma = 0xAE8;  // FVector
        public const uint ProneRecoilMean = 0xA98;  // FVector
        public const uint ProneRecoilSigma = 0xAB0;  // FVector
        public const uint ProneAdsRecoilMean = 0xA68;  // FVector
        public const uint ProneAdsRecoilSigma = 0xA80;  // FVector
        public const uint BipodRecoilMean = 0xC00;  // FVector
        public const uint BipodRecoilSigma = 0xC18;  // FVector
        public const uint BipodAdsRecoilMean = 0xBD0;  // FVector
        public const uint BipodAdsRecoilSigma = 0xBE8;  // FVector
        public const uint ProneTransitionRecoilMean = 0xBA0;  // FVector
        public const uint ProneTransitionRecoilSigma = 0xBB8;  // FVector
        public const uint MinShotDeviationFactor = 0xC84;  // float
        public const uint MaxShotDeviationFactor = 0xC88;  // float
        public const uint AddShotDeviationFactor = 0xC8C;  // float
        public const uint AddShotDeviationFactorAds = 0xC90;  // float
        public const uint ShotDeviationFactorRelease = 0xC94;  // float
        public const uint LowStaminaDeviationFactor = 0xC98;  // float
        public const uint FullStaminaDeviationFactor = 0xC9C;  // float
        public const uint MoveDeviationFactorRelease = 0xCA0;  // float
        public const uint AddMoveDeviation = 0xCA4;  // float
        public const uint MaxMoveDeviationFactor = 0xCA8;  // float
        public const uint MinMoveDeviationFactor = 0xCAC;  // float
        public const uint MinBipodAdsDeviation = 0xCB0;  // float
        public const uint MinBipodDeviation = 0xCB4;  // float
        public const uint MinProneAdsDeviation = 0xCB8;  // float
        public const uint MinProneDeviation = 0xCBC;  // float
        public const uint MinCrouchAdsDeviation = 0xCC0;  // float
        public const uint MinCrouchDeviation = 0xCC4;  // float
        public const uint MinStandAdsDeviation = 0xCC8;  // float
        public const uint MinStandDeviation = 0xCCC;  // float
        public const uint MinProneTransitionDeviation = 0xCD0;  // float
        public const uint AddMoveSway = 0xE68;  // float
        public const uint MaxMoveSwayFactor = 0xE70;  // float
        public const uint SwayData = 0xCD8;  // FSQSwayData
        public const uint SwayAlignmentData = 0xD90;  // FSQSwayData
    }

    public struct UAnimMontage
    {
        public const uint BlendIn = 0xC0;  // FAlphaBlend
        public const uint BlendInTime = 0x0;   // float (no direct match in updated dump)
        public const uint BlendOut = 0xF0;  // FAlphaBlend
        public const uint blendOutTime = 0x0;   // float (no direct match in updated dump)
        public const uint BlendOutTriggerTime = 0x120; // float
        public const uint bEnableAutoBlendOut = 0x172; // bool
    }

    public struct UAnimSequenceBase
    {
        public const uint SequenceLength = 0x90; // float
        public const uint RateScale = 0xA8; // float
    }
}
