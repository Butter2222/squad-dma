namespace squad_dma
{
    public enum ActorType
    {
        Player,
        Admin,
        FOBRadio,
        Hab,
        AntiAir,
        APC,
        AttackHelicopter,
        LoachCAS,
        LoachScout,
        Boat,
        BoatLogistics,
        DeployableAntiAir,
        DeployableAntitank,
        DeployableAntitankGun,
        DeployableGMG,
        DeployableHellCannon,
        DeployableHMG,
        DeployableMortars,
        DeployableRockets,
        Drone,
        IFV,
        JeepAntiAir,
        JeepAntitank,
        JeepArtillery,
        JeepLogistics,
        JeepTransport,
        JeepRWSTurret,
        JeepTurret,
        Mine,
        Motorcycle,
        Projectile,
        ProjectileAA,
        ProjectileSmall,
        RallyPoint,
        Tank,
        TankMGS,
        TrackedAPC,
        TrackedLogistics,
        TrackedAPCArtillery,
        TrackedIFV,
        TrackedJeep,
        TransportHelicopter,
        TruckAntiAir,
        TruckArtillery,
        TruckLogistics,
        TruckTransport,
        TruckTransportArmed
    }

    public enum Team
    {
        Unknown
    }

    public enum GameStatus
    {
        NotFound,
        Menu,
        InGame,
    }

    public enum PlayerState
    {
        Unknown,
        MainMenu,
        CommandMenu,
        Alive,
        Dead
    }

    public enum ESQVehicleType : byte
    {
        None = 0,
        Motorcycle = 1,
        Jeep = 2,
        JeepTransport = 3,
        JeepLogistics = 4,
        JeepAntiTank = 5,
        JeepArtillery = 6,
        TruckTransport = 7,
        TruckLogistics = 8,
        TruckAntiAir = 9,
        APC = 10,
        APCTracked = 11,
        AntiAirTracked = 12,
        IFV = 13,
        IFVTracked = 14,
        Tank = 15,
        HelicopterTransport = 16,
        HelicopterAttack = 17,
        Boat = 18,
        MAX = 19
    }

    public enum ESQTeam : byte
    {
        Team_Neutral = 0,
        Team_One = 1,
        Team_Two = 2,
        Team_Max = 3
    }
}
