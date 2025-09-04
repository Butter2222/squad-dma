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
    }

    public struct FTViewTarget
    {
        public const uint POV = 0x10; // FMinimalViewInfo
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
    }

    public struct FSQWeaponData
    {
        public const uint bInfiniteAmmo = 0x0; // bool
        public const uint bInfiniteMags = 0x1; // bool
        public const uint TimeBetweenShots = 0x20; // float
        public const uint TimeBetweenSingleShots = 0x24; // float
        public const uint bCreateProjectileOnServer = 0x29; // bool
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

    public struct SQVehicle
    {
        public const uint Health = 0x9A0;
        public const uint MaxHealth = 0x9A4;
    }

    public struct SQDeployable
    {
        public const uint Health = 0x424;
        public const uint MaxHealth = 0x41C;
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
}
