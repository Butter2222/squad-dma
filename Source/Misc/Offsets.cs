namespace Offsets
{
    public struct GameObjects
    {
        public const uint GObjects = 0xC7FEAE0;
        public const uint GNames = 0xC71AB80;
        public const uint GWorld = 0xC987EE8;
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
        public const uint CurrentLayer = 0x628;
    }

    public struct SQLayer {
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

    public struct ASQGameState
    {
        public const uint TeamStates = 0x3E0;
    }

    public struct ASQPlayerState
    {
        public const uint TeamID = 0x4E8; // per player
        public const uint SquadState = 0x7C8; // ASQSquadState*
        public const uint PlayerStateData = 0x710;
        public const uint Soldier = 0x7D0; // ASQSoldier*
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
        public const uint Health = 0x26E0; // float
        public const uint UnderSuppressionPercentage = 0x1D44; // float
        public const uint MaxSuppressionPercentage = 0x1D48; // float
        public const uint SuppressionMultiplier = 0x1D50; // float
        public const uint UseInteractDistance = 0x1EEC; // float
        public const uint InteractableRadiusMultiplier = 0x1F08; // float
        public const uint InventoryComponent = 0x2A40; // USQPawnInventoryComponent*
        public const uint CurrentItemStaticInfo = 0x2A68; // USQItemStaticInfo*
        public const uint bUsableInMainBase = 0x788; // bool
        public const uint bIsCameraRecoilActive = 0x2192; // bool
        public const uint WeaponBasedFOV = 0x288; // Float
        public const uint CachedAnimInstance1p = 0x2260; // USQAnimInstanceSoldier1P*
        public const uint Mesh = 0x288; // USkeletalMeshComponent*
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
        public const uint Health = 0x9A0;
        public const uint MaxHealth = 0x9A4;
        public const uint ClaimedBySquad = 0x660;
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
        public const uint ReplicatedMovementMode = 0x390; // uint8
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
        public const uint SeatPawn = 0x2C0; // ASQVehicleSeat*
    }

    public struct ASQVehicleSeat
    {
        public const uint VehicleInventory = 0x4B0; // USQVehicleInventoryComponent*
    }

    public struct USQVehicleComponent
    {
        public const uint Health = 0x708; // float
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
