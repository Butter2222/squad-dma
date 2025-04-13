namespace Offsets
{
    public struct GameObjects
    {
        public const uint GObjects = 0xa41d120;
        public const uint GNames = 0xa376640;
        public const uint GWorld = 0xa596938;
    }

    public struct World
    {
        public const uint PersistentLevel = 0x30;
        public const uint AuthorityGameMode = 0x150;
        public const uint GameState = 0x158;
        public const uint Levels = 0x170;
        public const uint OwningGameInstance = 0x1B8;
        public const uint WorldOrigin = 0x5B8; // 0x5B8 or 0x5C4
    }

    public struct GameInstance
    {
        public const uint LocalPlayers = 0x38;
        public const uint CurrentLayer = 0x618;
    }

    public struct SQLayer {
        public const uint LevelID = 0x70;
    }

    public struct Level // UNetConnection
    {
        public const uint Actors = 0x98; // OwningActor
        public const uint MaxPacket = 0xA0;
    }

    public struct Actor
    {
        public const uint Instigator = 0x190;
        public const uint RootComponent = 0x1A8;
        public const uint ID = 0x18; // Object ID
        public const uint CustomTimeDilation = 0x64; // float
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
        public const uint BodyInstance = 0x3B0; // FBodyInstance
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
        public const uint ViewportClient = 0x78;
    }

    public struct Pawn
    {
        public const uint PlayerState = 0x2C0;
        public const uint Controller = 0x2D8;
    }

    public struct Controller
    {
        public const uint PlayerState = 0x2A8;
        public const uint Pawn = 0x2F0;
        public const uint Character = 0x300;
    }

    public struct PlayerController
    {
        public const uint Player = 0x358;
        public const uint AcknowledgedPawn = 0x360;
        public const uint PlayerCameraManager = 0x370;
    }

    public struct SQPlayerController
    {
        public const uint TeamState = 0x890; // ASQTeamState*
        public const uint SquadState = 0x8A8; // ASQSquadState*
    }

    public struct PlayerCameraManager
    {
        public const uint PCOwner = 0x2A0;
        public const uint DefaultFOV = 0x2B8;
        public const uint ViewTarget = 0x330;
    }

    public struct FTViewTarget
    {
        public const uint POV = 0x10; // FMinimalViewInfo
    }

    public struct ASQGameState
    {
        public const uint TeamStates = 0x3A8;
    }

    public struct ASQPlayerState
    {
        public const uint TeamID = 0x4C0; // per player
        public const uint SquadState = 0x778; // ASQSquadState*
        public const uint PlayerStateData = 0x6E8;
        public const uint Soldier = 0x780; // ASQSoldier*
    }

    public struct ASQTeamState
    {
        public const uint Tickets = 0x2A0;
        public const uint ID = 0x2D0; // global | Team ID (0, 1, 2)
    }

    public struct ASQSquadState
    {
        public const uint SquadId = 0x320; // int32
        public const uint TeamId = 0x324; // int32
        public const uint PlayerStates = 0x328; // TArray<ASQPlayerState*>
        public const uint LeaderState = 0x338; // ASQPlayerState*
        public const uint AuthoritySquad = 0x2A0;
    }

    public struct FPlayerStateDataObject
    {
        public const uint NumKills = 0x4; // int32
        public const uint NumWoundeds = 0x10; // int32
    }

    public struct ASQSoldier
    {
        public const uint Health = 0x2648; // float
        public const uint UnderSuppressionPercentage = 0x1D04; // float
        public const uint MaxSuppressionPercentage = 0x1D08; // float
        public const uint SuppressionMultiplier = 0x1D10; // float
        public const uint UseInteractDistance = 0x1E94; // float
        public const uint InteractableRadiusMultiplier = 0x1EB0; // float
        public const uint InventoryComponent = 0x2998; // USQPawnInventoryComponent*
        public const uint CurrentItemStaticInfo = 0x29C0; // USQItemStaticInfo*
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
        public const uint ItemStaticInfo = 0x2A0; // USQItemStaticInfo*
        public const uint ItemStaticInfoClass = 0x2A8; // TSubclassOf<USQItemStaticInfo*>
        public const uint DisplayName = 0x330; // FText
        public const uint ItemCount = 0x40C; // int32
        public const uint MaxItemCount = 0x410; // int32
        public const uint EquipDuration = 0x42C; // float
        public const uint UnequipDuration = 0x430; // float
        public const uint CachedEquipDuration = 0x548; // float
        public const uint CachedUnequipDuration = 0x54C; // float
    }

    public struct SQVehicle
    {
        public const uint Health = 0x9F0;
        public const uint MaxHealth = 0x9F4;
    }

    public struct SQDeployable
    {
        public const uint Health = 0x41C;
        public const uint MaxHealth = 0x414;
    }

    public struct FString
    {
        public const uint Length = 0x8;
    }

    public struct Character
    {
        public const uint CharacterMovement = 0x330; // UCharacterMovementComponent*
        public const uint ReplicatedMovementMode = 0x428; // uint8
    }

    public struct CharacterMovementComponent
    {
        public const uint MovementMode = 0x211; // Engine::EMovementMode
        public const uint MaxFlySpeed = 0x264; // float
        public const uint MaxCustomMovementSpeed = 0x268; // float
        public const uint MaxAcceleration = 0x26C; // float
    }
}
