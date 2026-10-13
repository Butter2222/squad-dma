namespace Offsets
{
    public struct GameObjects
    {
        public const uint GObjects = 0xb945d30;
        public const uint GNames = 0x0B877C80;
        public const uint GWorld = 0xbad4398;
    }

    public struct World
    {
        public const uint PersistentLevel = 0x30; // ULevel*
        public const uint AuthorityGameMode = 0x1A8; // AGameModeBase*
        public const uint GameState = 0x1B0; // AGameStateBase*
        public const uint Levels = 0x1C8; // TArray<ULevel*>
        public const uint OwningGameInstance = 0x228; // UGameInstance*
        public const uint WorldOrigin = 0x5B8; // FIntVector
    }

    public struct GameInstance
    {
        public const uint LocalPlayers = 0x38; // TArray<ULocalPlayer*>
        public const uint CurrentLayer = 0x6C0; // USQLayer*
    }

    public struct SQLayer
    {
        public const uint LevelID = 0x68; // FString
    }

    public struct Level
    {
        public const uint Actors = 0xA0; // TArray<AActor*> 
    }

    public struct Actor
    {
        public const uint Instigator = 0x1A8; // APawn*
        public const uint RootComponent = 0x1C0; // USceneComponent*
        public const uint ID = 0x18; // int32
        public const uint CustomTimeDilation = 0x68; // float
        public const uint bReplicateMovement = 0x58; // uint8
        public const uint bHidden = 0x58; // uint8
        public const uint bActorEnableCollision = 0x5D; // uint8
    }

    public struct UObject
    {
        public const uint Class = 0x10; // UClass* (ClassPrivate)
        public const uint Name = 0x18; // FName.ComparisonIndex (NamePrivate)
        public const uint Outer = 0x20; // UObject* (OuterPrivate), for a component that's its owning actor
    }

    public struct USceneComponent
    {
        public const uint AttachParent = 0xD0; // USceneComponent*
        public const uint AttachChildren = 0xE8; // TArray<USceneComponent*>
        public const uint RelativeLocation = 0x148; // FVector
        public const uint RelativeRotation = 0x160; // FRotator
        public const uint RelativeScale3D = 0x178; // FVector
        public const uint ComponentToWorld = 0x1A8; // FTransform. Don't rely on it, the translation read back as ~0 for capture zones; walk AttachParent instead
    }

    // Objectives (flags / capture zones).
    public struct ASQUndirectedGraph
    {
        public const uint Nodes = 0x2D0; // TArray<USQGraphNodeComponent*>. 
    }

    public struct USQCaptureZoneComponent
    {
        public const uint CapturingTeam = 0x150; // uint8
        public const uint OwningTeam = 0x151; // uint8
        public const uint Uncappable = 0x153; // bool
        public const uint CapturePercent = 0x1CC; // float
    }

    public struct USphereComponent
    {
        public const uint SphereRadius = 0x568; // float.
    }
    public struct UBoxComponent
    {
        public const uint BoxExtent = 0x570; // FVector (doubles)
    }

    // Ranges that get scanned for fields whose offset moves around between builds.
    public struct ObjectiveScan
    {
        public const uint ActorFieldsStart = 0x2B8; // first field after AActor
        public const uint ActorFieldsLength = 0x200; // component pointers stored directly on the flag actor
        public const uint GraphArrayStart = 0x2B8; // search range for the graph's node TArray
        public const uint GraphArrayEnd = 0x400;
        public const uint SceneComponentArraysStart = 0xC0; // AttachChildren region of a USceneComponent
        public const uint SceneComponentArraysEnd = 0x140;
        public const uint ActorComponentArraysStart = 0x1F0; // InstanceComponents / BlueprintCreatedComponents region of an AActor
        public const uint ActorComponentArraysEnd = 0x2B8;
    }

    public struct UPrimitiveComponent
    {
        public const uint BodyInstance = 0x390; // FBodyInstance
    }

    public struct FBodyInstance
    {
        public const uint CollisionEnabled = 0x17; // uint8
    }

    public struct UPlayer
    {
        public const uint PlayerController = 0x30; // APlayerController*
    }

    public struct ULocalPlayer
    {
        public const uint ViewportClient = 0x78; // UGameViewportClient*
    }

    public struct Pawn
    {
        public const uint PlayerState = 0x2D8; // APlayerState*
        public const uint Controller = 0x2E8; // AController*
    }

    public struct Controller
    {
        public const uint PlayerState = 0x2C0; // APlayerState*
        public const uint Pawn = 0x2F8; // APawn*
        public const uint Character = 0x308; // ACharacter*
    }

    public struct PlayerController
    {
        public const uint Player = 0x358; // UPlayer*
        public const uint AcknowledgedPawn = 0x360; // APawn*
        public const uint PlayerCameraManager = 0x370; // APlayerCameraManager*
    }

    public struct SQPlayerController
    {
        public const uint TeamState = 0x760; // ASQTeamState*
        public const uint SquadState = 0x778; // ASQSquadState*
    }

    public struct PlayerCameraManager
    {
        public const uint PCOwner = 0x2B8; // APlayerController*
        public const uint DefaultFOV = 0x2D0; // float
        public const uint ViewTarget = 0x350; // FTViewTarget
        public const uint ModifierList = 0x27A0; // TArray<UCameraModifier*>
        public const uint CachedCameraShakeMod = 0x2830; // UCameraModifier_CameraShake*
    }

    public struct FTViewTarget
    {
        public const uint POV = 0x10; // FMinimalViewInfo
    }

    public struct AGameStateBase
    {
        public const uint PlayerArray = 0x2D0; // TArray<APlayerState*>
    }

    public struct ASQGameState
    {
        public const uint TeamStates = 0x3C0; // TArray<ASQTeamState*>
        public const uint WinningTeam = 0x3E8; // ASQTeamState*
    }

    public struct APlayerState
    {
        public const uint PawnPrivate = 0x330; // APawn*
    }

    public struct ASQPlayerState
    {
        public const uint TeamID = 0x4B8; // int32
        public const uint SquadState = 0x790; // ASQSquadState*
        public const uint PlayerStateData = 0x6D8; // FPlayerStateDataObject
        public const uint Soldier = 0x7B0; // ASQSoldier*
        public const uint CurrentSeat = 0x770; // USQVehicleSeatComponent*
    }

    public struct ASQTeamState
    {
        public const uint Tickets = 0x2B8; // int32
        public const uint ID = 0x2E0; // int32
    }

    public struct ASQSquadState
    {
        public const uint AuthoritySquad = 0x2B8; // bool
        public const uint SquadId = 0x340; // int32
        public const uint TeamId = 0x344; // int32
        public const uint PlayerStates = 0x348; // TArray<ASQPlayerState*>
        public const uint LeaderState = 0x358; // ASQPlayerState*
    }

    public struct FPlayerStateDataObject
    {
        public const uint NumKills = 0x4; // int32
        public const uint NumWoundeds = 0x10; // int32
    }

    public struct ACharacter
    {
        public const uint Mesh = 0x338; // USkeletalMeshComponent*
    }

    public struct ASQSoldier
    {
        public const uint Health = 0x27C0; // float
        public const uint MaxHealth = 0x2754; // float 
        public const uint SoldierBitFlags = 0x274C; // uint8 bitfield: bit0=bIsDying, bit1=bIsBleeding, bit2=bIsWounded, bit3=bAllowWoundedState - UNVERIFIED (field not found in current SDK dump, value kept from previous list)
        public const uint WoundedThreshold = 0x27C8; // float
        public const uint HealthBleedableThreshold = 0x27CC; // float
        public const uint WoundedHealthLossRate = 0x27D8; // float
        public const uint BleedingHealthLossRate = 0x27E0; // float
        public const uint MaximumSafeFallingSpeed = 0x2854; // float
        public const uint UnderSuppressionPercentage = 0x1F0C; // float
        public const uint MaxSuppressionPercentage = 0x1F10; // float
        public const uint SuppressionMultiplier = 0x1F18; // float
        public const uint UseInteractDistance = 0x20BC; // float
        public const uint InteractableRadiusMultiplier = 0x20D8; // float
        public const uint InventoryComponent = 0x2B98; // USQPawnInventoryComponent*
        public const uint CurrentItemStaticInfo = 0x2BC0; // USQItemStaticInfo*
        public const uint bIsCameraRecoilActive = 0x2C3A; // bool
        public const uint WeaponBasedFOV = 0x7C0; // float
        public const uint CachedAnimInstance1p = 0x2D50; // USQAnimInstanceSoldier1P*
    }

    public struct USQItemStaticInfo
    {
        public const uint bUsableInMainBase = 0x7A0; // bool
    }

    public struct USQPawnInventoryComponent
    {
        public const uint CurrentWeapon = 0x170; // ASQEquipableItem*
        public const uint CurrentItemStaticInfo = 0x160; // USQItemStaticInfo*
        public const uint CurrentWeaponSlot = 0x1AC; // int32
        public const uint CurrentWeaponOffset = 0x1B0; // int32
        public const uint Inventory = 0x1B8; // TArray<FSQWeaponGroupData>
    }

    public struct ASQWeapon
    {
        public const uint WeaponConfig = 0x6D0; // FSQWeaponData
        public const uint CurrentFireMode = 0x7F8; // int32
        public const uint bAimingDownSights = 0x7B4; // bool
        public const uint CachedPipScope = 0x7A8; // USQPipScopeCaptureComponent*
        public const uint CurrentFOV = 0x8AC; // float
        public const uint bFireInput = 0x7B5; // bool
        public const uint WeaponStaticInfo = 0x550; // USQWeaponStaticInfo*
        public const uint CurrentState = 0x7A0; // ESQWeaponState
    }

    public struct FSQWeaponData
    {
        public const uint bInfiniteAmmo = 0x0; // bool
        public const uint bInfiniteMags = 0x1; // bool
        public const uint TimeBetweenShots = 0x20; // float
        public const uint TimeBetweenSingleShots = 0x24; // float
        public const uint TacticalReloadDuration = 0x2C; // float
        public const uint FinishReloadGracePeriod = 0x30; // float
        public const uint DryReloadDuration = 0x34; // float
        public const uint bCreateProjectileOnServer = 0x29; // bool
        public const uint FireModes = 0x10; // TArray<int32>
    }

    public struct ASQEquipableItem
    {
        public const uint ItemStaticInfo = 0x2B8; // USQItemStaticInfo*
        public const uint ItemStaticInfoClass = 0x2C0; // TSubclassOf<USQItemStaticInfo*>
        public const uint DisplayName = 0x320; // FText
        public const uint ItemCount = 0x3F4; // int32
        public const uint MaxItemCount = 0x3F8; // int32
        public const uint EquipDuration = 0x414; // float
        public const uint UnequipDuration = 0x418; // float
        public const uint bCachedIsPullingTrigger = 0x50C; // bool
        public const uint CachedEquipDuration = 0x500; // float
        public const uint CachedUnequipDuration = 0x504; // float
    }

    public struct FSQSwayData
    {
        public const uint UnclampedTotalSway = 0xA0; // float
        public const uint TotalSway = 0xA4; // float
        public const uint Sway = 0xA8; // FRotator
    }

    public struct USQPipScopeCaptureComponent
    {
        public const uint CurrentMagnificationLevel = 0xD48; // int32
    }

    public struct ASQGrenade
    {
        public const uint GrenadeState = 0x540; // ESQGrenadeState (uint8)
        public const uint GrenadeConfig = 0x548; // FSQGrenadeData (embedded struct)
        public const uint GrenadeStaticInfo = 0x5A8; // USQGrenadeStaticInfo*
        public const uint bCachedOverhandPinpull = 0x5B0; // bool
        public const uint bCachedOverhandThrow = 0x5B1; // bool
        public const uint bCachedUnderhandPinpull = 0x5B2; // bool
        public const uint bCachedUnderhandThrow = 0x5B3; // bool
        public const uint bCachedReloading = 0x5B4; // bool
        public const uint ActorWeaponOverhandPinpull1pMontage = 0x600;
        public const uint ActorWeaponOverhandPinpull3pMontage = 0x608;
        public const uint ActorOverhandPinpull1pMontage = 0x610;
        public const uint ActorOverhandPinpull3pMontage = 0x618;
        public const uint ActorWeaponOverhandThrow1pMontage = 0x620;
        public const uint ActorWeaponOverhandThrow3pMontage = 0x628;
        public const uint ActorOverhandThrow1pMontage = 0x630;
        public const uint ActorOverhandThrow3pMontage = 0x638;
        public const uint ActorWeaponUnderhandPinpull1pMontage = 0x640;
        public const uint ActorWeaponUnderhandPinpull3pMontage = 0x648;
        public const uint ActorUnderhandPinpull1pMontage = 0x650;
        public const uint ActorUnderhandPinpull3pMontage = 0x658;
        public const uint ActorWeaponUnderhandThrow1pMontage = 0x660;
        public const uint ActorWeaponUnderhandThrow3pMontage = 0x668;
        public const uint ActorUnderhandThrow1pMontage = 0x670;
        public const uint ActorUnderhandThrow3pMontage = 0x678;
    }

    public struct USQGrenadeStaticInfo
    {
        public const uint WeaponOverhandPinpull1pMontage = 0x7D8; // UAnimMontage*
        public const uint WeaponOverhandPinpull3pMontage = 0x7E0; // UAnimMontage*
        public const uint OverhandPinpull1pMontage = 0x7E8; // UAnimMontage*
        public const uint OverhandPinpull3pMontage = 0x7F0; // UAnimMontage*
        public const uint WeaponOverhandThrow1pMontage = 0x7F8; // UAnimMontage*
        public const uint WeaponOverhandThrow3pMontage = 0x800; // UAnimMontage*
        public const uint OverhandThrow1pMontage = 0x808; // UAnimMontage*
        public const uint OverhandThrow3pMontage = 0x810; // UAnimMontage*
        public const uint WeaponUnderhandPinpull1pMontage = 0x818; // UAnimMontage*
        public const uint WeaponUnderhandPinpull3pMontage = 0x820; // UAnimMontage*
        public const uint UnderhandPinpull1pMontage = 0x828; // UAnimMontage*
        public const uint UnderhandPinpull3pMontage = 0x830; // UAnimMontage*
        public const uint WeaponUnderhandThrow1pMontage = 0x838; // UAnimMontage*
        public const uint WeaponUnderhandThrow3pMontage = 0x840; // UAnimMontage*
        public const uint UnderhandThrow1pMontage = 0x848; // UAnimMontage*
        public const uint UnderhandThrow3pMontage = 0x850; // UAnimMontage*
    }

    public struct FSQGrenadeData
    {
        public const uint bInfiniteAmmo = 0x0; // bool
        public const uint ThrowReadyTime = 0x14; // float
        public const uint OverhandThrowTime = 0x18; // float
        public const uint UnderhandThrowTime = 0x1C; // float
        public const uint OverhandThrowDuration = 0x20; // float
        public const uint UnderhandThrowDuration = 0x24; // float
        public const uint ThrowModeTransitionTime = 0x28; // float
        public const uint ReloadTime = 0x34; // float
    }

    public struct ASQVehicle
    {
        public const uint ClaimInfo = 0x648; // USQVehicleClaim*
        public const uint ClaimedBySquad = 0x650; // ASQSquadState*
        public const uint bClaimable = 0x658; // bool
        public const uint bEnterableWithoutClaim = 0x659; // bool
        public const uint bDrivableWithoutClaim = 0x65A; // bool
        public const uint VehicleType = 0x7C0; // ESQVehicleType
        public const uint DriverSeatConfig = 0x818; // FSQVehicleSeatConfig
        public const uint VehicleSeats = 0x8A8; // TArray<USQVehicleSeatComponent*>
        public const uint Health = 0x960; // float
        public const uint MaxHealth = 0x964; // float
    }

    public struct ASQPawn
    {
        public const uint Team = 0x346; // ESQTeam
    }

    public struct SQDeployable
    {
        public const uint Health = 0x42C; // float
        public const uint MaxHealth = 0x424; // float
        public const uint Team = 0x2E0; // ESQTeam
    }

    public struct FString
    {
        public const uint Length = 0x8;
    }

    public struct Character
    {
        public const uint CharacterMovement = 0x340; // UCharacterMovementComponent*
        public const uint ReplicatedMovementMode = 0x462; // uint8
    }

    public struct CharacterMovementComponent
    {
        public const uint MovementMode = 0x2E1; // EMovementMode
        public const uint MaxFlySpeed = 0x334; // float
        public const uint MaxCustomMovementSpeed = 0x338; // float
        public const uint MaxAcceleration = 0x33C; // float
    }

    public struct USQVehicleSeatComponent
    {
        public const uint SeatPawn = 0x2D8; // ASQVehicleSeat*
    }

    public struct ASQVehicleSeat
    {
        public const uint VehicleInventory = 0x4B0; // USQVehicleInventoryComponent*
    }

    public struct USQAnimInstanceSoldier1P
    {
        public const uint WeapRecoilRelLoc = 0xA38; // FVector
        public const uint MoveRecoilFactor = 0xD90; // float
        public const uint RecoilCanRelease = 0xD94; // float
        public const uint FinalRecoilSigma = 0xD98; // FVector
        public const uint FinalRecoilMean = 0xDB0; // FVector
        public const uint MoveDeviationFactor = 0xD2C; // float
        public const uint ShotDeviationFactor = 0xD30; // float
        public const uint FinalDeviation = 0xD40; // FVector4
        public const uint StandRecoilMean = 0x1020; // FVector
        public const uint StandRecoilSigma = 0x1038; // FVector
        public const uint CrouchRecoilMean = 0xFC0; // FVector
        public const uint CrouchRecoilSigma = 0xFD8; // FVector
        public const uint ProneRecoilMean = 0xF60; // FVector
        public const uint ProneRecoilSigma = 0xF78; // FVector
        public const uint BipodRecoilMean = 0x10B0; // FVector
        public const uint BipodRecoilSigma = 0x10C8; // FVector
        public const uint ProneTransitionRecoilMean = 0x1050; // FVector
        public const uint ProneTransitionRecoilSigma = 0x1068; // FVector
        public const uint WeaponPunch = 0x1300; // FRotator
        public const uint MoveSwayFactorMultiplier = 0x1294; // float
        public const uint SuppressSwayFactorMultiplier = 0x1298; // float
        public const uint WeaponPunchSwayCombinedRotator = 0x12A0; // FRotator
        public const uint UnclampedTotalSway = 0x1390; // float
        public const uint SwayData = 0x10E0; // FSQSwayData
        public const uint SwayAlignmentData = 0x11B8; // FSQSwayData
        public const uint AddMoveDeviation = 0xE98; // float
        public const uint MoveDeviationFactorRelease = 0xE9C; // float
        public const uint MaxMoveDeviationFactor = 0xEA0; // float
        public const uint MinMoveDeviationFactor = 0xEA4; // float
        public const uint FullStaminaDeviationFactor = 0xEA8; // float
        public const uint LowStaminaDeviationFactor = 0xEAC; // float
        public const uint AddShotDeviationFactor = 0xEB0; // float
        public const uint AddShotDeviationFactorAds = 0xEB4; // float
        public const uint ShotDeviationFactorRelease = 0xEB8; // float
        public const uint MinShotDeviationFactor = 0xEBC; // float
        public const uint MaxShotDeviationFactor = 0xEC0; // float
        public const uint MinBipodAdsDeviation = 0xED0; // float
        public const uint MinBipodDeviation = 0xED4; // float
        public const uint MinProneAdsDeviation = 0xED8; // float
        public const uint MinProneDeviation = 0xEDC; // float
        public const uint MinCrouchAdsDeviation = 0xEE0; // float
        public const uint MinCrouchDeviation = 0xEE4; // float
        public const uint MinStandAdsDeviation = 0xEE8; // float
        public const uint MinStandDeviation = 0xEEC; // float
        public const uint MinProneTransitionDeviation = 0xEF0; // float
        public const uint FireShake = 0xE70; // TSubclassOf<UCameraShakeBase*>
    }

    public struct USQWeaponStaticInfo
    {
        public const uint bRequiresManualBolt = 0x1191; // bool
        public const uint bRequireAdsToShoot = 0x11C9; // bool
        public const uint RecoilCameraOffsetFactor = 0x9C8; // float
        public const uint RecoilWeaponRelLocFactor = 0x9E0; // float
        public const uint AddMoveRecoil = 0xA04; // float
        public const uint MaxMoveRecoilFactor = 0xA08; // float
        public const uint StandRecoilMean = 0xB80; // FVector
        public const uint StandRecoilSigma = 0xB98; // FVector
        public const uint StandAdsRecoilMean = 0xB50; // FVector
        public const uint StandAdsRecoilSigma = 0xB68; // FVector
        public const uint CrouchRecoilMean = 0xB18; // FVector
        public const uint CrouchRecoilSigma = 0xB30; // FVector
        public const uint CrouchAdsRecoilMean = 0xAE8; // FVector
        public const uint CrouchAdsRecoilSigma = 0xB00; // FVector
        public const uint ProneRecoilMean = 0xAB0; // FVector
        public const uint ProneRecoilSigma = 0xAC8; // FVector
        public const uint ProneAdsRecoilMean = 0xA80; // FVector
        public const uint ProneAdsRecoilSigma = 0xA98; // FVector
        public const uint BipodRecoilMean = 0xC18; // FVector
        public const uint BipodRecoilSigma = 0xC30; // FVector
        public const uint BipodAdsRecoilMean = 0xBE8; // FVector
        public const uint BipodAdsRecoilSigma = 0xC00; // FVector
        public const uint ProneTransitionRecoilMean = 0xBB8; // FVector
        public const uint ProneTransitionRecoilSigma = 0xBD0; // FVector
        public const uint MinShotDeviationFactor = 0xC9C; // float
        public const uint MaxShotDeviationFactor = 0xCA0; // float
        public const uint AddShotDeviationFactor = 0xCA4; // float
        public const uint AddShotDeviationFactorAds = 0xCA8; // float
        public const uint ShotDeviationFactorRelease = 0xCAC; // float
        public const uint LowStaminaDeviationFactor = 0xCB0; // float
        public const uint FullStaminaDeviationFactor = 0xCB4; // float
        public const uint MoveDeviationFactorRelease = 0xCB8; // float
        public const uint AddMoveDeviation = 0xCBC; // float
        public const uint MaxMoveDeviationFactor = 0xCC0; // float
        public const uint MinMoveDeviationFactor = 0xCC4; // float
        public const uint MinBipodAdsDeviation = 0xCC8; // float
        public const uint MinBipodDeviation = 0xCCC; // float
        public const uint MinProneAdsDeviation = 0xCD0; // float
        public const uint MinProneDeviation = 0xCD4; // float
        public const uint MinCrouchAdsDeviation = 0xCD8; // float
        public const uint MinCrouchDeviation = 0xCDC; // float
        public const uint MinStandAdsDeviation = 0xCE0; // float
        public const uint MinStandDeviation = 0xCE4; // float
        public const uint MinProneTransitionDeviation = 0xCE8; // float
        public const uint AddMoveSway = 0xEC0; // float
        public const uint MaxMoveSwayFactor = 0xEC8; // float
        public const uint SwayData = 0xCF0; // FSQSwayData
        public const uint SwayAlignmentData = 0xDC8; // FSQSwayData
    }

    public struct UAnimMontage
    {
        public const uint BlendInTime = 0xC8; // FAlphaBlend::BlendTime
        public const uint BlendOutTime = 0xF8; // FAlphaBlend::BlendTime 
        public const uint BlendOutTriggerTime = 0x120; // float
        public const uint bEnableAutoBlendOut = 0x172; // bool
    }

    public struct UAnimSequenceBase
    {
        public const uint SequenceLength = 0x90; // float
        public const uint RateScale = 0xA8; // float
    }
}
