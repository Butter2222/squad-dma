namespace Offsets
{
    public struct GameObjects
    {
        public const uint GObjects = 0x0D08C1B0;
        public const uint GNames = 0x0CFBDFC0;
        public const uint GWorld = 0x0D222EB8;
    }

    public struct World
    {
        public const uint PersistentLevel = 0x30;
        public const uint AuthorityGameMode = 0x1A8;
        public const uint GameState = 0x1B0;
        public const uint Levels = 0x1C8;
        public const uint OwningGameInstance = 0x230;
        public const uint WorldOrigin = 0x5B8; // 0x5B8 or 0x5C4
    }

    public struct GameInstance
    {
        public const uint LocalPlayers = 0x38;
        public const uint CurrentLayer = 0x670;
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
        public const uint bActorEnableCollision = 0x5d; // uint8
    }

    public struct USceneComponent
    {
        public const uint RelativeLocation = 0x148;
        public const uint RelativeRotation = 0x160;
        public const uint RelativeScale3D = 0x178;
        public const uint ComponentToWorld = 0x1A8;
    }

    public struct UPrimitiveComponent
    {
        public const uint BodyInstance = 0x398; // FBodyInstance
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
        public const uint PlayerState = 0x2D8;
        public const uint Controller = 0x2E8;
    }

    public struct Controller
    {
        public const uint PlayerState = 0x2C0;
        public const uint Pawn = 0x308;
        public const uint Character = 0x318;
    }

    public struct PlayerController
    {
        public const uint Player = 0x370;
        public const uint AcknowledgedPawn = 0x378;
        public const uint PlayerCameraManager = 0x388;
    }

    public struct SQPlayerController
    {
        public const uint TeamState = 0x778; // ASQTeamState*
        public const uint SquadState = 0x790; // ASQSquadState*
    }

    public struct PlayerCameraManager
    {
        public const uint PCOwner = 0x2B8;
        public const uint DefaultFOV = 0x2D0;
        public const uint ViewTarget = 0x350;
        public const uint ModifierList = 0x27B0; // TArray<UCameraModifier*>
        public const uint CachedCameraShakeMod = 0x2848; // UCameraModifier_CameraShake*
    }

    public struct FTViewTarget
    {
        public const uint POV = 0x10; // FMinimalViewInfo
    }

    public struct AGameStateBase
    {
        public const uint PlayerArray = 0x2D0;
    }

    public struct ASQGameState
    {
        public const uint TeamStates = 0x3E8;
        public const uint WinningTeam = 0x410; // ASQTeamState*
    }

    public struct APlayerState
    {
        public const uint PawnPrivate = 0x338;
    }

    public struct ASQPlayerState
    {
        public const uint TeamID = 0x508; // per player
        public const uint SquadState = 0x7E8; // ASQSquadState*
        public const uint PlayerStateData = 0x730;
        public const uint Soldier = 0x7F0; // ASQSoldier*
        public const uint CurrentSeat = 0x7C8; // USQVehicleSeatComponent*
    }

    public struct ASQTeamState
    {
        public const uint Tickets = 0x2B8;
        public const uint ID = 0x2E8; // global | Team ID (0, 1, 2)
    }

    public struct ASQSquadState
    {
        public const uint AuthoritySquad = 0x2b8;
        public const uint SquadId = 0x348; // int32
        public const uint TeamId = 0x34c; // int32
        public const uint PlayerStates = 0x350; // TArray<ASQPlayerState*>
        public const uint LeaderState = 0x360; // ASQPlayerState*
    }

    public struct FPlayerStateDataObject
    {
        public const uint NumKills = 0x4; // int32
        public const uint NumWoundeds = 0x14; // int32
    }

    public struct ACharacter
    {
        public const uint Mesh = 0x338; // USkeletalMeshComponent* ACharacter
    }

    public struct ASQSoldier
    {
        public const uint Health = 0x2740; // float
        public const uint UnderSuppressionPercentage = 0x1eb4; // float
        public const uint MaxSuppressionPercentage = 0x1eb8; // float
        public const uint SuppressionMultiplier = 0x1ec0; // float
        public const uint UseInteractDistance = 0x2054; // float
        public const uint InteractableRadiusMultiplier = 0x2070; // float
        public const uint InventoryComponent = 0x2ab0; // USQPawnInventoryComponent*
        public const uint CurrentItemStaticInfo = 0x2ad8; // USQItemStaticInfo*
        public const uint bIsCameraRecoilActive = 0x2b5a; // bool
        public const uint WeaponBasedFOV = 0x7d8; // float
        public const uint CachedAnimInstance1p = 0x2c70; // USQAnimInstanceSoldier1P*
    }

    public struct USQItemStaticInfo
    {
        public const uint bUsableInMainBase = 0x7A0; // bool 
    }

    public struct USQPawnInventoryComponent
    {
        public const uint CurrentWeapon = 0x1c0; // ASQEquipableItem*
        public const uint CurrentItemStaticInfo = 0x1b0; // USQItemStaticInfo*
        public const uint CurrentWeaponSlot = 0x204; // int32
        public const uint CurrentWeaponOffset = 0x208; // int32
        public const uint Inventory = 0x210; // TArray<FSQWeaponGroupData>
    }

    public struct ASQWeapon
    {
        public const uint WeaponConfig = 0x720; // FSQWeaponData
        public const uint CurrentFireMode = 0x848; // int32
        public const uint bAimingDownSights = 0x804; // bool
        public const uint CachedPipScope = 0x7f8; // USQPipScopeCaptureComponent*
        public const uint CurrentFOV = 0x8fc; // float
        public const uint bFireInput = 0x805; // bool
        public const uint WeaponStaticInfo = 0x5a0; // USQWeaponStaticInfo*
        public const uint CurrentState = 0x7f0; // ESQWeaponState
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
        public const uint ItemStaticInfo = 0x2b8; // USQItemStaticInfo*
        public const uint ItemStaticInfoClass = 0x2c0; // TSubclassOf<USQItemStaticInfo*>
        public const uint DisplayName = 0x348; // FText
        public const uint ItemCount = 0x41c; // int32
        public const uint MaxItemCount = 0x420; // int32
        public const uint EquipDuration = 0x43c; // float
        public const uint UnequipDuration = 0x440; // float
        public const uint CachedEquipDuration = 0x558; // float
        public const uint CachedUnequipDuration = 0x55c; // float
    }

    public struct FSQSwayData
    {
        public const uint UnclampedTotalSway = 0x80; // float
        public const uint TotalSway = 0x84; // float
        public const uint Sway = 0x88; // FRotator
    }
    public struct USQPipScopeCaptureComponent
    {
        public const uint CurrentMagnificationLevel = 0xD58; // int32

    }

    public struct ASQGrenade
    {
        public const uint GrenadeConfig = 0x590; // FSQGrenadeData
        public const uint GrenadeStaticInfo = 0x5F0; // USQGrenadeStaticInfo*
    }

    public struct USQGrenadeStaticInfo
    {
        public const uint WeaponOverhandPinpull1pMontage = 0x7d8; // UAnimMontage*
        public const uint WeaponOverhandPinpull3pMontage = 0x7e0; // UAnimMontage*
        public const uint OverhandPinpull1pMontage = 0x7e8; // UAnimMontage*
        public const uint OverhandPinpull3pMontage = 0x7f0; // UAnimMontage*
        public const uint WeaponOverhandThrow1pMontage = 0x7f8; // UAnimMontage*
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
        public const uint bInfiniteAmmo = 0x0;  // bool
        public const uint ThrowReadyTime = 0x14; // float
        public const uint OverhandThrowTime = 0x18; // float
        public const uint UnderhandThrowTime = 0x1c; // float
        public const uint OverhandThrowDuration = 0x20; // float
        public const uint UnderhandThrowDuration = 0x24; // float
        public const uint ThrowModeTransitionTime = 0x28; // float
        public const uint ReloadTime = 0x34; // float
    }

    public struct ASQVehicle
    {
        public const uint ClaimInfo = 0x668; // USQVehicleClaim*
        public const uint ClaimedBySquad = 0x670; // ASQSquadState*
        public const uint bClaimable = 0x678; // bool
        public const uint bEnterableWithoutClaim = 0x679; // bool
        public const uint bDrivableWithoutClaim = 0x67a; // bool
        public const uint VehicleType = 0x7e0; // ESQVehicleType
        public const uint DriverSeatConfig = 0x838; // FSQVehicleSeatConfig
        public const uint VehicleSeats = 0x8c8; // TArray<USQVehicleSeatComponent*>
        public const uint Health = 0x9b8; // float
        public const uint MaxHealth = 0x9bc; // float
    }

    public struct ASQPawn
    {
        public const uint Team = 0x346; // ESQTeam
    }

    public struct SQDeployable
    {
        public const uint Health = 0x42C;
        public const uint MaxHealth = 0x424;
        public const uint Team = 0x2E0;
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
        public const uint MovementMode = 0x2E1; // Engine::EMovementMode
        public const uint MaxFlySpeed = 0x334; // float
        public const uint MaxCustomMovementSpeed = 0x338; // float
        public const uint MaxAcceleration = 0x33C; // float
    }

    public struct USQVehicleSeatComponent
    {
	    public const uint SeatPawn = 0x2e8; // ASQVehicleSeat*
}

    public struct ASQVehicleSeat
    {
        public const uint VehicleInventory = 0x4C0; // USQVehicleInventoryComponent*
    }

    public struct USQAnimInstanceSoldier1P
    {
        public const uint WeapRecoilRelLoc = 0xa70; // FVector
        public const uint MoveRecoilFactor = 0xdc0; // float
        public const uint RecoilCanRelease = 0xdc4; // float
        public const uint FinalRecoilSigma = 0xdc8; // FVector
        public const uint FinalRecoilMean = 0xde0; // FVector
        public const uint MoveDeviationFactor = 0xd64; // float
        public const uint ShotDeviationFactor = 0xd68; // float
        public const uint FinalDeviation = 0xd70; // FVector4
        public const uint StandRecoilMean = 0x1050; // FVector
        public const uint StandRecoilSigma = 0x1068; // FVector
        public const uint CrouchRecoilMean = 0xff0; // FVector
        public const uint CrouchRecoilSigma = 0x1008; // FVector
        public const uint ProneRecoilMean = 0xf90; // FVector
        public const uint ProneRecoilSigma = 0xfa8; // FVector
        public const uint BipodRecoilMean = 0x10e0; // FVector
        public const uint BipodRecoilSigma = 0x10f8; // FVector
        public const uint ProneTransitionRecoilMean = 0x1080; // FVector
        public const uint ProneTransitionRecoilSigma = 0x1098; // FVector
        public const uint WeaponPunch = 0x12f0; // FRotator
        public const uint MoveSwayFactorMultiplier = 0x1284; // float
        public const uint SuppressSwayFactorMultiplier = 0x1288; // float
        public const uint WeaponPunchSwayCombinedRotator = 0x1290; // FRotator
        public const uint UnclampedTotalSway = 0x1380; // float
        public const uint SwayData = 0x1110; // FSQSwayData
        public const uint SwayAlignmentData = 0x11c8; // FSQSwayData
        public const uint AddMoveDeviation = 0xec8; // float
        public const uint MoveDeviationFactorRelease = 0xecc; // float
        public const uint MaxMoveDeviationFactor = 0xed0; // float
        public const uint MinMoveDeviationFactor = 0xed4; // float
        public const uint FullStaminaDeviationFactor = 0xed8; // float
        public const uint LowStaminaDeviationFactor = 0xedc; // float
        public const uint AddShotDeviationFactor = 0xee0; // float
        public const uint AddShotDeviationFactorAds = 0xee4; // float
        public const uint ShotDeviationFactorRelease = 0xee8; // float
        public const uint MinShotDeviationFactor = 0xeec; // float
        public const uint MaxShotDeviationFactor = 0xef0; // float
        public const uint MinBipodAdsDeviation = 0xf00; // float
        public const uint MinBipodDeviation = 0xf04; // float
        public const uint MinProneAdsDeviation = 0xf08; // float
        public const uint MinProneDeviation = 0xf0c; // float
        public const uint MinCrouchAdsDeviation = 0xf10; // float
        public const uint MinCrouchDeviation = 0xf14; // float
        public const uint MinStandAdsDeviation = 0xf18; // float
        public const uint MinStandDeviation = 0xf1c; // float
        public const uint MinProneTransitionDeviation = 0xf20; // float
        public const uint FireShake = 0xea0; // TSubclassOf<UCameraShakeBase*>
    }

    public struct USQWeaponStaticInfo
    {
        public const uint bRequiresManualBolt = 0x1111; // bool
        public const uint bRequireAdsToShoot = 0x1149; // bool
        public const uint RecoilCameraOffsetFactor = 0x9c8; // float
        public const uint RecoilWeaponRelLocFactor = 0x9e0; // float
        public const uint AddMoveRecoil = 0xa04; // float
        public const uint MaxMoveRecoilFactor = 0xa08; // float
        public const uint StandRecoilMean = 0xb80; // FVector
        public const uint StandRecoilSigma = 0xb98; // FVector
        public const uint StandAdsRecoilMean = 0xb50; // FVector
        public const uint StandAdsRecoilSigma = 0xb68; // FVector
        public const uint CrouchRecoilMean = 0xb18; // FVector
        public const uint CrouchRecoilSigma = 0xb30; // FVector
        public const uint CrouchAdsRecoilMean = 0xae8; // FVector
        public const uint CrouchAdsRecoilSigma = 0xb00; // FVector
        public const uint ProneRecoilMean = 0xab0; // FVector
        public const uint ProneRecoilSigma = 0xac8; // FVector
        public const uint ProneAdsRecoilMean = 0xa80; // FVector
        public const uint ProneAdsRecoilSigma = 0xa98; // FVector
        public const uint BipodRecoilMean = 0xc18; // FVector
        public const uint BipodRecoilSigma = 0xc30; // FVector
        public const uint BipodAdsRecoilMean = 0xbe8; // FVector
        public const uint BipodAdsRecoilSigma = 0xc00; // FVector
        public const uint ProneTransitionRecoilMean = 0xbb8; // FVector
        public const uint ProneTransitionRecoilSigma = 0xbd0; // FVector
        public const uint MinShotDeviationFactor = 0xc9c; // float
        public const uint MaxShotDeviationFactor = 0xca0; // float
        public const uint AddShotDeviationFactor = 0xca4; // float
        public const uint AddShotDeviationFactorAds = 0xca8; // float
        public const uint ShotDeviationFactorRelease = 0xcac; // float
        public const uint LowStaminaDeviationFactor = 0xcb0; // float
        public const uint FullStaminaDeviationFactor = 0xcb4; // float
        public const uint MoveDeviationFactorRelease = 0xcb8; // float
        public const uint AddMoveDeviation = 0xcbc; // float
        public const uint MaxMoveDeviationFactor = 0xcc0; // float
        public const uint MinMoveDeviationFactor = 0xcc4; // float
        public const uint MinBipodAdsDeviation = 0xcc8; // float
        public const uint MinBipodDeviation = 0xccc; // float
        public const uint MinProneAdsDeviation = 0xcd0; // float
        public const uint MinProneDeviation = 0xcd4; // float
        public const uint MinCrouchAdsDeviation = 0xcd8; // float
        public const uint MinCrouchDeviation = 0xcdc; // float
        public const uint MinStandAdsDeviation = 0xce0; // float
        public const uint MinStandDeviation = 0xce4; // float
        public const uint MinProneTransitionDeviation = 0xce8; // float
        public const uint AddMoveSway = 0xe80; // float
        public const uint MaxMoveSwayFactor = 0xe88; // float
        public const uint SwayData = 0xcf0; // FSQSwayData
        public const uint SwayAlignmentData = 0xda8; // FSQSwayData
    }

    public struct UAnimMontage
    {
        public const uint BlendIn = 0xc0; // FAlphaBlend
        public const uint BlendInTime = 0x0; // float (no direct match in updated dump)
        public const uint BlendOut = 0xf0; // FAlphaBlend
        public const uint blendOutTime = 0x0; // float (no direct match in updated dump)
        public const uint BlendOutTriggerTime = 0x120; // float
        public const uint bEnableAutoBlendOut = 0x172; // bool
    }

    public struct UAnimSequenceBase
    {
        public const uint SequenceLength = 0x90; // float
        public const uint RateScale = 0xA8; // float
    }
}
