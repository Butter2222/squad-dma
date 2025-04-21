namespace Offsets
{
    public struct GameObjects
    {
        public const uint GObjects = 0x7217040;
        public const uint GNames = 0x71daac0;
        public const uint GWorld = 0x7357e80;
    }

    public struct World
    {
        public const uint PersistentLevel = 0x30;
        public const uint AuthorityGameMode = 0x118;
        public const uint GameState = 0x120;
        public const uint Levels = 0x138;
        public const uint OwningGameInstance = 0x180;
        public const uint WorldOrigin = 0x5B8; // 0x5B8 or 0x5C4
    }

    public struct GameInstance
    {
        public const uint LocalPlayers = 0x38;
        public const uint CurrentLayer = 0x568;
    }

    public struct SQLayer {
        public const uint LevelID = 0x70;
    }

    public struct Level
    {
        public const uint Actors = 0x98;
        public const uint MaxPacket = 0xA0;
    }

    public struct Actor
    {
        public const uint Instigator = 0x120;
        public const uint RootComponent = 0x138;
        public const uint ID = 0x18;
        public const uint CustomTimeDilation = 0x98; // float
        public const uint bReplicateMovement = 0x58; // uint8
        public const uint bHidden = 0x58; // uint8
        public const uint bCanBeDamaged = 0x5a; // uint8
        public const uint bActorEnableCollision = 0x5c; // uint8
    }

    public struct USceneComponent
    {
        public const uint RelativeLocation = 0x11C;
        public const uint RelativeRotation = 0x128;
        public const uint ComponentToWorld = 0x1C0; // Relative Offset Guess
        public const uint RelativeScale3D = 0x134;
        public const uint RelativeLocationComp2World = 0x1D0;
    }

    public struct UPrimitiveComponent
    {
        public const uint BodyInstance = 0x2C8; // FBodyInstance
    }

    public struct FBodyInstance
    {
        public const uint CollisionEnabled = 0x20; // uint8
    }

    public struct UPlayer
    {
        public const uint PlayerController = 0x30;
    }

    public struct ULocalPlayer
    {
        public const uint ViewportClient = 0x70;
    }

    public struct Pawn
    {
        public const uint PlayerState = 0x248;
        public const uint Controller = 0x260;
    }

    public struct MeshComponent
    {
        public const uint CachedBoneSpaceTransforms = 0x0740;
    }

    public struct Controller
    {
        public const uint PlayerState = 0x230;
        public const uint Pawn = 0x258;
        public const uint Character = 0x268;
    }

    public struct PlayerController
    {
        public const uint Player = 0x2A0;
        public const uint AcknowledgedPawn = 0x2A8;
        public const uint PlayerCameraManager = 0x2C0;
        public const uint SceneComponentTransform = 0x270;
        public const uint SquadState = 0x5A8; // ASQSquadState*
        public const uint TeamState = 0x590; // ASQTeamState*
    }

    public struct PlayerCameraManager
    {
        public const uint PCOwner = 0x228;
        public const uint DefaultFOV = 0x240;
        public const uint ViewTarget = 0xEA0;
        public const uint CachedCameraShakeMod = 0x2760; // UCameraModifier_CameraShake*
    }

    public struct FTViewTarget
    {
        public const uint PCOwner = 0x228;
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

    public struct ASQGameState
    {
        public const uint TeamStates = 0x308;
    }

    public struct ASQPlayerState
    {
        public const uint TeamID = 0x400; // int32
        public const uint CurrentSeat = 0x750; // USQVehicleSeatComponent*
        public const uint SquadState = 0x760; // ASQSquadState*
        public const uint Soldier = 0x768; // ASQSoldier*
        public const uint PlayerStateData = 0x6D0; // FPlayerStateDataObject
    }

    public struct ASQTeamState
    {
        public const uint Tickets = 0x228;
        public const uint ID = 0x240; // global | Team ID (0, 1, 2)
    }

    public struct ASQSquadState
    {
        public const uint SquadId = 0x2A8; // int32
        public const uint TeamId = 0x2AC; // int32
        public const uint PlayerStates = 0x2B0; // TArray<ASQPlayerState*>
        public const uint LeaderState = 0x2C0; // ASQPlayerState*
        public const uint AuthoritySquad = 0x228;
    }

    public struct FPlayerStateDataObject
    {
        public const uint NumKills = 0x4; // int32
        public const uint NumWoundeds = 0x10; // int32
    }

    public struct ASQSoldier
    {
        public const uint Health = 0x1DF8; // float
        public const uint CachedAnimInstance1p = 0x2260; // USQAnimInstanceSoldier1P*
        public const uint Mesh = 0x288; // USkeletalMeshComponent*
        public const uint UnderSuppressionPercentage = 0x15e4; // float
        public const uint MaxSuppressionPercentage = 0x15e8; // float
        public const uint SuppressionMultiplier = 0x15f0; // float
        public const uint UseInteractDistance = 0x16ec; // float
        public const uint InteractableRadiusMultiplier = 0x1708; // float
        public const uint InventoryComponent = 0x2108; // USQPawnInventoryComponent*
        public const uint CurrentItemStaticInfo = 0x140; // USQItemStaticInfo*
        public const uint bUsableInMainBase = 0x5b0; // bool
        public const uint bIsCameraRecoilActive = 0x2192; // bool
        public const uint WeaponBasedFOV = 0x288; // Float
    }

    public struct USQPawnInventoryComponent
    {
        public const uint CurrentWeapon = 0x150; // ASQEquipableItem*
        public const uint CurrentItemStaticInfo = 0x140; // USQItemStaticInfo*
        public const uint CurrentWeaponSlot = 0x18c; // int32
        public const uint CurrentWeaponOffset = 0x190; // int32
        public const uint Inventory = 0x198; // TArray<FSQWeaponGroupData>
    }

    public struct ASQWeapon
    {
        public const uint WeaponConfig = 0x620; // FSQWeaponData
        public const uint CurrentFireMode = 0x740; // int32
        public const uint bAimingDownSights = 0x6fc; // bool
        public const uint CachedPipScope = 0x6f0; // USQPipScopeCaptureComponent*
        public const uint CurrentFOV = 0x7e8; // float
        public const uint bFireInput = 0x6fd; // bool
        public const uint WeaponStaticInfo = 0x488; // USQWeaponStaticInfo*
        public const uint CurrentState = 0x6e8; // ESQWeaponState
    }

    public struct FSQWeaponData
    {
        public const uint bInfiniteAmmo = 0x0; // bool
        public const uint bInfiniteMags = 0x1; // bool
        public const uint FireModes = 0x10; // TArray<int32>
        public const uint TimeBetweenShots = 0x20; // float
        public const uint TimeBetweenSingleShots = 0x24; // float
    }

    public struct ASQEquipableItem
    {
        public const uint ItemStaticInfo = 0x228; // USQItemStaticInfo*
        public const uint ItemStaticInfoClass = 0x230; // TSubclassOf<USQItemStaticInfo*>
        public const uint DisplayName = 0x270; // FText
        public const uint ItemCount = 0x34c; // int32
        public const uint MaxItemCount = 0x350; // int32
        public const uint EquipDuration = 0x36c; // float
        public const uint UnequipDuration = 0x370; // float
        public const uint CachedEquipDuration = 0x448; // float
        public const uint CachedUnequipDuration = 0x44c; // float
    }

    public struct FSQSwayData
    {
        public const uint UnclampedTotalSway = 0x74; // float
        public const uint TotalSway = 0x78; // float
        public const uint Sway = 0x7c; // FRotator
    }
    public struct USQPipScopeCaptureComponent
    {
        public const uint CurrentMagnificationLevel = 0x960; // int32

    }

    public struct ASQGrenade
    {
        public const uint GrenadeConfig = 0x480; // FSQGrenadeData
        public const uint GrenadeStaticInfo = 0x4e0; // USQGrenadeStaticInfo*
    }

    public struct USQGrenadeStaticInfo
    {
        public const uint WeaponOverhandPinpull1pMontage = 0x5f0; // UAnimMontage*
        public const uint WeaponOverhandPinpull3pMontage = 0x5f8; // UAnimMontage*
        public const uint OverhandPinpull1pMontage = 0x600; // UAnimMontage*
        public const uint OverhandPinpull3pMontage = 0x608; // UAnimMontage*
        public const uint WeaponOverhandThrow1pMontage = 0x610; // UAnimMontage*
        public const uint WeaponOverhandThrow3pMontage = 0x618; // UAnimMontage*
        public const uint OverhandThrow1pMontage = 0x620; // UAnimMontage*
        public const uint OverhandThrow3pMontage = 0x628; // UAnimMontage*
        public const uint WeaponUnderhandPinpull1pMontage = 0x630; // UAnimMontage*
        public const uint WeaponUnderhandPinpull3pMontage = 0x638; // UAnimMontage*
        public const uint UnderhandPinpull1pMontage = 0x640; // UAnimMontage*
        public const uint UnderhandPinpull3pMontage = 0x648; // UAnimMontage*
        public const uint WeaponUnderhandThrow1pMontage = 0x650; // UAnimMontage*
        public const uint WeaponUnderhandThrow3pMontage = 0x658; // UAnimMontage*
        public const uint UnderhandThrow1pMontage = 0x660; // UAnimMontage*
        public const uint UnderhandThrow3pMontage = 0x668; // UAnimMontage*
    }

    public struct FSQGrenadeData
    {
        public const uint bInfiniteAmmo = 0x0; // bool
        public const uint ThrowReadyTime = 0x14; // float
        public const uint OverhandThrowTime = 0x18; // float
        public const uint UnderhandThrowTime = 0x1c; // float
        public const uint OverhandThrowDuration = 0x20; // float
        public const uint UnderhandThrowDuration = 0x24; // float
        public const uint ThrowModeTransitionTime = 0x28; // float
        public const uint ReloadTime = 0x34; // float
    }

    public struct SQVehicle
    {
        public const uint Health = 0x868;
        public const uint MaxHealth = 0x86C;
        public const uint ClaimedBySquad = 0x530;
    }

    public struct SQDeployable
    {
        public const uint Health = 0x374;
        public const uint MaxHealth = 0x36C;
        public const uint Team = 0x250;
    }

    public struct FString
    {
        public const uint Length = 0x8;
    }

    public struct Character
    {
        public const uint CharacterMovement = 0x290; // UCharacterMovementComponent*
        public const uint ReplicatedMovementMode = 0x328; // uint8
    }

    public struct CharacterMovementComponent
    {
        public const uint MovementMode = 0x168; // Engine::EMovementMode
        public const uint MaxFlySpeed = 0x198; // float
        public const uint MaxCustomMovementSpeed = 0x19c; // float
        public const uint MaxAcceleration = 0x1a0; // float
    }

    public struct USQVehicleSeatComponent
    {
        public const uint SeatPawn = 0x270; // ASQVehicleSeat*
    }

    public struct ASQVehicleSeat
    {
        public const uint VehicleInventory = 0x3a8; // USQVehicleInventoryComponent*
    }

    public struct USQVehicleComponent
    {
        public const uint Health = 0x5e0; // float
    }

    public struct USQAnimInstanceSoldier1P
    {
        public const uint WeapRecoilRelLoc = 0x6e8; // FVector
        public const uint MoveRecoilFactor = 0x8cc; // float
        public const uint RecoilCanRelease = 0x8d0; // float
        public const uint FinalRecoilSigma = 0x8d4; // FVector
        public const uint FinalRecoilMean = 0x8e0; // FVector
        public const uint MoveDeviationFactor = 0x898; // float
        public const uint ShotDeviationFactor = 0x89c; // float
        public const uint FinalDeviation = 0x8a0; // FVector4
        public const uint StandRecoilMean = 0xa80; // FVector
        public const uint StandRecoilSigma = 0xa8c; // FVector
        public const uint CrouchRecoilMean = 0xa50; // FVector
        public const uint CrouchRecoilSigma = 0xa5c; // FVector
        public const uint ProneRecoilMean = 0xa20; // FVector
        public const uint ProneRecoilSigma = 0xa2c; // FVector
        public const uint BipodRecoilMean = 0xac8; // FVector
        public const uint BipodRecoilSigma = 0xad4; // FVector
        public const uint ProneTransitionRecoilMean = 0xa98; // FVector
        public const uint ProneTransitionRecoilSigma = 0xaa4; // FVector
        public const uint WeaponPunch = 0xc44; // FRotator
        public const uint MoveSwayFactorMultiplier = 0xc0c; // float
        public const uint SuppressSwayFactorMultiplier = 0xc10; // float
        public const uint WeaponPunchSwayCombinedRotator = 0xc14; // FRotator
        public const uint UnclampedTotalSway = 0xc94; // float
        public const uint SwayData = 0xae0; // FSQSwayData
        public const uint SwayAlignmentData = 0xb74; // FSQSwayData
        public const uint AddMoveDeviation = 0x970; // float
        public const uint MoveDeviationFactorRelease = 0x974; // float
        public const uint MaxMoveDeviationFactor = 0x978; // float
        public const uint MinMoveDeviationFactor = 0x97c; // float
        public const uint FullStaminaDeviationFactor = 0x980; // float
        public const uint LowStaminaDeviationFactor = 0x984; // float
        public const uint AddShotDeviationFactor = 0x988; // float
        public const uint AddShotDeviationFactorAds = 0x98c; // float
        public const uint ShotDeviationFactorRelease = 0x990; // float
        public const uint MinShotDeviationFactor = 0x994; // float
        public const uint MaxShotDeviationFactor = 0x998; // float
        public const uint MinBipodAdsDeviation = 0x9a8; // float
        public const uint MinBipodDeviation = 0x9ac; // float
        public const uint MinProneAdsDeviation = 0x9b0; // float
        public const uint MinProneDeviation = 0x9b4; // float
        public const uint MinCrouchAdsDeviation = 0x9b8; // float
        public const uint MinCrouchDeviation = 0x9bc; // float
        public const uint MinStandAdsDeviation = 0x9c0; // float
        public const uint MinStandDeviation = 0x9c4; // float
        public const uint MinProneTransitionDeviation = 0x9c8; // float
        public const uint FireShake = 0x948; // TSubclassOf<UCameraShakeBase*>
    }

    public struct USQWeaponStaticInfo
    {
        public const uint bRequiresManualBolt = 0xd31; // bool
        public const uint bRequireAdsToShoot = 0xd69; // bool
        public const uint RecoilCameraOffsetFactor = 0x7c4; // float
        public const uint RecoilWeaponRelLocFactor = 0x7dc; // float
        public const uint AddMoveRecoil = 0x7fc; // float
        public const uint MaxMoveRecoilFactor = 0x800; // float
        public const uint StandRecoilMean = 0x8d8; // FVector
        public const uint StandRecoilSigma = 0x8e4; // FVector
        public const uint StandAdsRecoilMean = 0x8c0; // FVector
        public const uint StandAdsRecoilSigma = 0x8cc; // FVector
        public const uint CrouchRecoilMean = 0x8a4; // FVector
        public const uint CrouchRecoilSigma = 0x8b0; // FVector
        public const uint CrouchAdsRecoilMean = 0x88c; // FVector
        public const uint CrouchAdsRecoilSigma = 0x898; // FVector
        public const uint ProneRecoilMean = 0x870; // FVector
        public const uint ProneRecoilSigma = 0x87c; // FVector
        public const uint ProneAdsRecoilMean = 0x858; // FVector
        public const uint ProneAdsRecoilSigma = 0x864; // FVector
        public const uint BipodRecoilMean = 0x924; // FVector
        public const uint BipodRecoilSigma = 0x930; // FVector
        public const uint BipodAdsRecoilMean = 0x90c; // FVector
        public const uint BipodAdsRecoilSigma = 0x918; // FVector
        public const uint ProneTransitionRecoilMean = 0x8f4; // FVector
        public const uint ProneTransitionRecoilSigma = 0x900; // FVector
        public const uint MinShotDeviationFactor = 0x970; // float
        public const uint MaxShotDeviationFactor = 0x974; // float
        public const uint AddShotDeviationFactor = 0x978; // float
        public const uint AddShotDeviationFactorAds = 0x97c; // float
        public const uint ShotDeviationFactorRelease = 0x980; // float
        public const uint LowStaminaDeviationFactor = 0x984; // float
        public const uint FullStaminaDeviationFactor = 0x988; // float
        public const uint MoveDeviationFactorRelease = 0x98c; // float
        public const uint AddMoveDeviation = 0x990; // float
        public const uint MaxMoveDeviationFactor = 0x994; // float
        public const uint MinMoveDeviationFactor = 0x998; // float
        public const uint MinBipodAdsDeviation = 0x99c; // float
        public const uint MinBipodDeviation = 0x9a0; // float
        public const uint MinProneAdsDeviation = 0x9a4; // float
        public const uint MinProneDeviation = 0x9a8; // float
        public const uint MinCrouchAdsDeviation = 0x9ac; // float
        public const uint MinCrouchDeviation = 0x9b0; // float
        public const uint MinStandAdsDeviation = 0x9b4; // float
        public const uint MinStandDeviation = 0x9b8; // float
        public const uint MinProneTransitionDeviation = 0x9bc; // float
        public const uint AddMoveSway = 0xb10; // float
        public const uint MaxMoveSwayFactor = 0xb18; // float
        public const uint SwayData = 0x9c4; // FSQSwayData
        public const uint SwayAlignmentData = 0xa58; // FSQSwayData
    }

    public struct UAnimMontage
    {
        public const uint BlendIn = 0xa8; // FAlphaBlend
        public const uint BlendInTime = 0xd8; // float
        public const uint BlendOut = 0xe0; // FAlphaBlend
        public const uint blendOutTime = 0x110; // float
        public const uint BlendOutTriggerTime = 0x114; // float
        public const uint bEnableAutoBlendOut = 0x17a; // bool
    }

    public struct UAnimSequenceBase
    {
        public const uint SequenceLength = 0x90; // float
        public const uint RateScale = 0x94; // float
    }
}
