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
    }

    public struct USceneComponent
    {
        public const uint RelativeLocation = 0x11C;
        public const uint RelativeRotation = 0x128;
        public const uint ComponentToWorld = 0x11C; // Relative Offset Guess
        public const uint RelativeScale3D = 0x134;
        public const uint RelativeLocationComp2World = 0x1D0;
    }

    public struct UPlayer
    {
        public const uint PlayerController = 0x30;
    }

    public struct ULocalPlayer
    {
        public const uint ViewportClient = 0x70;
    }

    public struct PlayerState
    {
        public const uint PlayerName = 0x308;
    }

    public struct Pawn
    {
        public const uint PlayerState = 0x248;
        public const uint Controller = 0x260;
    }

    public struct Character
    {
        public const uint Mesh = 0x288;
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
    }

    public struct Camera
    {
        public const uint PCOwner = 0x228;
        public const uint CameraCache = 0x1AF0; // 0x10 = FMinimalViewInfo
        public const uint CameraLocation = 0x1B00;
        public const uint CameraRotation = 0x1B0C;
        public const uint CameraFov = 0x1B18;
    }

    public struct ASQPlayerState
    {
        public const uint TeamID = 0x400;
    }

    public struct ASQSoldier
    {
        public const uint Health = 0x1DF8;
        public const uint InventoryComponent = 0x2108; // USQPawnInventoryComponent*
    }

    public struct USQPawnInventoryComponent
    {
        public const uint CurrentWeapon = 0x150; // ASQEquipableItem*
    }
    public struct ASQWeapon
    {
        public const uint bAimingDownSights = 0x6fc; // bool
        public const uint CachedPipScope = 0x6f0; // USQPipScopeCaptureComponent*
        public const uint CurrentFOV = 0x7e8; // float
    }

    public struct USQPipScopeCaptureComponent
    {
        public const uint CurrentMagnificationLevel = 0x960; // int32

    }

    public struct SQVehicle
    {
        public const uint Health = 0x868;
        public const uint MaxHealth = 0x86C;
    }

    public struct SQDeployable
    {
        public const uint Health = 0x374;
        public const uint MaxHealth = 0x36C;
    }

    public struct FString
    {
        public const uint Length = 0x8;
    }
}
