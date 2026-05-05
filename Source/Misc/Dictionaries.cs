using SkiaSharp;

namespace squad_dma
{
    public class Names
    {
        #region Vehicle Type Mapping
        // Maps ESQVehicleType enum (from game memory) to ActorType
        public static readonly Dictionary<ESQVehicleType, ActorType> VehicleTypeMap = new()
        {
            { ESQVehicleType.Motorcycle, ActorType.Motorcycle },
            { ESQVehicleType.Jeep, ActorType.JeepTurret },
            { ESQVehicleType.JeepTransport, ActorType.JeepTransport },
            { ESQVehicleType.JeepLogistics, ActorType.JeepLogistics },
            { ESQVehicleType.JeepAntiTank, ActorType.JeepAntitank },
            { ESQVehicleType.JeepArtillery, ActorType.JeepArtillery },
            { ESQVehicleType.TruckTransport, ActorType.TruckTransport },
            { ESQVehicleType.TruckLogistics, ActorType.TruckLogistics },
            { ESQVehicleType.TruckAntiAir, ActorType.TruckAntiAir },
            { ESQVehicleType.APC, ActorType.APC },
            { ESQVehicleType.APCTracked, ActorType.TrackedAPC },
            { ESQVehicleType.AntiAirTracked, ActorType.AntiAir },
            { ESQVehicleType.IFV, ActorType.IFV },
            { ESQVehicleType.IFVTracked, ActorType.TrackedIFV },
            { ESQVehicleType.Tank, ActorType.Tank },
            { ESQVehicleType.HelicopterTransport, ActorType.TransportHelicopter },
            { ESQVehicleType.HelicopterAttack, ActorType.AttackHelicopter },
            { ESQVehicleType.Boat, ActorType.Boat },
        };
        #endregion

        #region Name-Based Actor Detection
        public static readonly Dictionary<string, ActorType> TechNames = new()
        {
            // Helicopters dont have any indentifiers from the game so I hardcoded them
            {"BP_MI8_CAS_C", ActorType.AttackHelicopter},
            {"BP_MI17_MEA_CAS_C", ActorType.AttackHelicopter},
            {"BP_MI8_C", ActorType.TransportHelicopter},
            {"BP_MI17_MEA_C", ActorType.TransportHelicopter},
            {"BP_CH178_C", ActorType.TransportHelicopter},
            {"BP_MI8_VDV_C", ActorType.TransportHelicopter},
            {"BP_MI8_AFU_C", ActorType.TransportHelicopter},
            {"BP_UH60_C", ActorType.TransportHelicopter},
            {"BP_UH60_TLF_PKM_C", ActorType.TransportHelicopter},
            {"BP_UH60_CAS_C", ActorType.AttackHelicopter},
            {"BP_UH60_M134_C", ActorType.TransportHelicopter},
            {"BP_UH60_AUS_C", ActorType.TransportHelicopter},
            {"BP_UH1Y_C", ActorType.TransportHelicopter},
            {"BP_UH1H_Desert_C", ActorType.TransportHelicopter},
            {"BP_UH1H_C", ActorType.TransportHelicopter},
            {"BP_UH1H_GFI_C", ActorType.TransportHelicopter},
            {"BP_SA330_C", ActorType.TransportHelicopter},
            {"BP_MRH90_Mag58_C", ActorType.TransportHelicopter},
            {"BP_MRH90_CAS_C", ActorType.AttackHelicopter},
            {"BP_CH146_C", ActorType.TransportHelicopter},
            {"BP_CH146_CAS_C", ActorType.AttackHelicopter},
            {"BP_CH146_Desert_C", ActorType.TransportHelicopter},
            {"BP_CH146_Desert_CAS_C", ActorType.AttackHelicopter},
            {"BP_Z8G_C", ActorType.TransportHelicopter},
            {"BP_Z8J_C", ActorType.TransportHelicopter},
            {"BP_Z8J_CAS_C", ActorType.AttackHelicopter},
            {"BP_Z9A_C", ActorType.TransportHelicopter},
            {"BP_CH146_Raven_C", ActorType.TransportHelicopter},
            {"BP_Loach_C", ActorType.LoachScout},
            {"BP_Loach_CAS_Small_C", ActorType.LoachCAS},
            {"BP_MI8_VDV_GE_C", ActorType.TransportHelicopter},
            {"BP_MI8_Child_C", ActorType.TransportHelicopter},
            {"BP_GE_MI8_CAS_VDV_C", ActorType.AttackHelicopter},
            {"BP_GE_MI8_CAS_C", ActorType.AttackHelicopter},
            {"BP_UH60_MINIGUN_C", ActorType.TransportHelicopter},
            {"BP_UH60_IDF_MINIGUN_C", ActorType.TransportHelicopter},
            {"BP_Z8J_CAS_GE_C", ActorType.AttackHelicopter},
            {"BP_Z8G_Child_GE_C", ActorType.TransportHelicopter},
            {"BP_SA330_IDF_C", ActorType.TransportHelicopter},
            {"BP_MRH90_CAS_Child_Finland_C", ActorType.TransportHelicopter},
            {"BP_MI8_PLF_Child_C", ActorType.TransportHelicopter},
            {"BP_Loach_CAS_Large_GE_C", ActorType.LoachCAS},
            {"BP_GE_KA27_CAS_C", ActorType.AttackHelicopter},
            {"BP_Loach_Child_GE_C", ActorType.LoachScout},
            {"BP_MI28_C", ActorType.AttackHelicopter},
            {"BP_MH6_C", ActorType.LoachCAS},
            {"BP_MH6_Logi_C", ActorType.LoachScout},
            {"SD_BP_UH60_CAS_C", ActorType.AttackHelicopter},
            {"BP_AH64_C", ActorType.AttackHelicopter},
            {"SD_BP_UH60_M240_C", ActorType.TransportHelicopter},
            {"SD_BP_UH60_M134_C", ActorType.TransportHelicopter},
            {"BP_UH60_RED_C", ActorType.TransportHelicopter},
            {"SD_BP_UH1Y_Mk19_PMC_C", ActorType.TransportHelicopter},
            {"SD_BP_UH1Y_ATGM_PMC_C", ActorType.TransportHelicopter},
            {"SD_BP_UH1Y_PMC_C", ActorType.TransportHelicopter},
            {"SD_BP_UH1Y_Mk19_PMC_Desert_C", ActorType.TransportHelicopter},
            {"SD_BP_UH1Y_ATGM_PMC_Desert_C", ActorType.TransportHelicopter},
            {"SD_BP_UH1Y_PMC_Desert_C", ActorType.TransportHelicopter},
            {"SD_BP_UH60_M134_TSF_Forest_C", ActorType.TransportHelicopter},
            {"BP_MI8_Terminator_C", ActorType.AttackHelicopter},
            {"SD_BP_SA330_C", ActorType.TransportHelicopter},
            {"SD_BP_Z8G_C", ActorType.TransportHelicopter},


            // FOB Radios
            {"BP_FOBRadio_Woodland_C", ActorType.FOBRadio},
            {"BP_FobRadio_INS_C", ActorType.FOBRadio},
            {"BP_FOBRadio_MEA_C", ActorType.FOBRadio},
            {"BP_FOBRadio_IMF_C", ActorType.FOBRadio},
            {"BP_FOBRadio_PLA_C", ActorType.FOBRadio},
            {"BP_FOBRadio_RGF_C", ActorType.FOBRadio},
            {"BP_FOBRadio_TLF_C", ActorType.FOBRadio},
            {"BP_FOBRadio_WPMC_C", ActorType.FOBRadio},
            {"BP_DestroyableObjective_C", ActorType.FOBRadio},
            {"BP_FOBRadio_AFU_C", ActorType.FOBRadio},
            {"GE_RGF_Hab_C", ActorType.Hab},
            {"GE_NATO_Hab_C", ActorType.Hab},
            {"GE_INS_Hab_C", ActorType.Hab},

            // HABs
            {"INS_Hab_C", ActorType.Hab},
            {"MEA_Hab_C", ActorType.Hab},
            {"IMF_hab_C", ActorType.Hab},
            {"RGF_Hab_Desert_C", ActorType.Hab},
            {"RGF_Hab_Snow_C", ActorType.Hab},
            {"RGF_Hab_Woodland_C", ActorType.Hab},
            {"US_Hab_Desert_C", ActorType.Hab},
            {"US_Hab_Forest_C", ActorType.Hab},
            {"US_Hab_Snow_C", ActorType.Hab},
            {"ADF_Hab_Desert_C", ActorType.Hab},
            {"ADF_Hab_Snow_C", ActorType.Hab},
            {"ADF_Hab_Woodland_C", ActorType.Hab},
            {"BAF_Hab_C", ActorType.Hab},
            {"PLA_Hab_Desert_C", ActorType.Hab},
            {"PLA_Hab_Winter_C", ActorType.Hab},
            {"PLA_Hab_C", ActorType.Hab},
            {"WPMC_Hab_Forest_C", ActorType.Hab},
            {"WPMC_Hab_Desert_C", ActorType.Hab},
            {"AFU_Hab_Woodland_C", ActorType.Hab},
            {"AFU_Hab_Desert_C", ActorType.Hab},

            // Deployable Mortars
            {"BP_2b14podnosmortar_Deployable_C", ActorType.DeployableMortars},
            {"BP_81mmMortar_Deployable_C", ActorType.DeployableMortars},
            {"BP_m1937mortar_Deployable_C", ActorType.DeployableMortars},
            {"BP_L16mortar_Deployable_C", ActorType.DeployableMortars},
            {"BP_m252mortar_Deployable_C", ActorType.DeployableMortars},
            {"BP_PP87Mortar_Deployable_C", ActorType.DeployableMortars},
            {"SD_BP_2b14podnosmortar_Deployable_C", ActorType.DeployableMortars},
            {"SD_BP_81mmMortar_Deployable_C", ActorType.DeployableMortars},
            {"SD_BP_m1937mortar_Deployable_C", ActorType.DeployableMortars},
            {"SD_BP_L16mortar_Deployable_C", ActorType.DeployableMortars},
            {"SD_BP_m252mortar_Deployable_C", ActorType.DeployableMortars},
            {"SD_BP_PP87Mortar_Deployable_C", ActorType.DeployableMortars},
            {"SD_BP_120mortar_Deployable_C", ActorType.DeployableMortars},

            // Deployable Anti-Tank
            {"BP_BGM71TOW_Tripod_USA_C", ActorType.DeployableAntitank},
            {"BP_HJ-8ATGM_Deployable_C", ActorType.DeployableAntitank},
            {"BP_Kornet_Tripod_MEA_C", ActorType.DeployableAntitank},
            {"BP_Kornet_Tripod_Rus_C", ActorType.DeployableAntitank},
            {"SD_BP_BGM71TOW_Tripod_USA_C", ActorType.DeployableAntitank},
            {"SD_BP_HJ-8ATGM_Deployable_C", ActorType.DeployableAntitank},
            {"SD_BP_Kornet_Tripod_MEA_C", ActorType.DeployableAntitank},
            {"SD_BP_Kornet_Tripod_Rus_C", ActorType.DeployableAntitank},
            {"BP_Stinger_Base_Deployable_C", ActorType.DeployableAntitank},
            {"BP_Strela_Base_Deployable_C", ActorType.DeployableAntitank},

            // Deployable Anti-Tank Guns
            {"BP_SPG9_Tripod_C", ActorType.DeployableAntitankGun},

            // Deployable HMGs
            {"BP_DShK_C", ActorType.DeployableHMG},
            {"BP_DShK_Shielded_C", ActorType.DeployableHMG},
            {"BP_Kord_MEA_Bunker_C", ActorType.DeployableHMG},
            {"BP_Kord_RU_Bunker_C", ActorType.DeployableHMG},
            {"BP_Kord_RU_DE_Bunker_C", ActorType.DeployableHMG},
            {"BP_Kord_Tripod_C", ActorType.DeployableHMG},
            {"BP_M2_Tripod_C", ActorType.DeployableHMG},
            {"BP_M2_US_Bunker_C", ActorType.DeployableHMG},

            // Deployable GMGs
            {"BP_EmplacedC16_Tripod_Deployable_C", ActorType.DeployableGMG},
            {"BP_EmplacedL134A1_Tripod_Deployable_C", ActorType.DeployableGMG},
            {"BP_EmplacedMk19_Tripod_Deployable_C", ActorType.DeployableGMG},
            {"BP_EmplacedMk19_Tripod_Bunker_Deployable_C", ActorType.DeployableGMG},

            // Deployable Hell Cannons
            {"BP_Emplaced_HellCannon_Deployable_C", ActorType.DeployableHellCannon},

            // Deployable Rockets
            {"BP_EmplacedUB32_Deployable_C", ActorType.DeployableRockets},

            // Deployable Anti-Air
            {"BP_ZU-23_Emplacement_C", ActorType.DeployableAntiAir},
            {"BP_ZU-23_Emplacement_Ins_C", ActorType.DeployableAntiAir},

            // Mines
            {"BP_Deployable_M15Mine_C", ActorType.Mine},
            {"BP_Deployable_Type72Mine_C", ActorType.Mine},
            {"BP_Deployable_TM62Mine_C", ActorType.Mine},

            // Projectiles
            {"BP_Mortarround4_C", ActorType.Projectile},
            {"BP_Mortarround_SMOKE2_C", ActorType.Projectile},
            {"BP_Projectile_Hell_Cannon_C", ActorType.Projectile},
            {"BP_S5_Proj2_C", ActorType.Projectile},
            {"BP_BM21_Rocket_Proj2_C", ActorType.Projectile},
            {"BP_40MM_MK19_Proj_C", ActorType.Projectile},
            {"BP_Mortarround_120mm_C", ActorType.Projectile},
            {"BP_SPG9_pg9v_Heat_Proj2_C", ActorType.Projectile},
            {"BP_Mortarround_120mm_Airburst_C", ActorType.Projectile},
            {"BP_HIMARS_Rocket_Proj2_C", ActorType.Projectile},
            {"BP_Projectile_155_29th_C", ActorType.Projectile},
            {"BP_BM21_Rocket_Napalm_C", ActorType.Projectile},
            {"BP_Projectile_155_29th_Smoke_C", ActorType.Projectile},

            // AA Projectiles
            {"BP_Projectile_GuidedAA_Stinger_C", ActorType.ProjectileAA},
            {"BP_Projectile_GuidedAA_Strela_C", ActorType.ProjectileAA},
            {"BP_Projectile_GuidedAA_Parent_C", ActorType.ProjectileAA},

            // Small Projectiles
            {"BP_Hydra70_Proj2_C", ActorType.ProjectileSmall},
            {"BP_RPG7_Heat_Proj2_C", ActorType.ProjectileSmall},
            {"BP_RPG7_Frag_Proj2_C", ActorType.ProjectileSmall},
            {"BP_40MM_VOG_Proj2_C", ActorType.ProjectileSmall},
            {"BP_RPG28_Tandem_Proj_C", ActorType.ProjectileSmall},

            // Rally Points
            {"BP_SquadRallyPoint_C", ActorType.RallyPoint},

            // Admin
            {"BP_DeveloperAdminCam_C", ActorType.Admin},

            // Artillery Vehicles
            {"BP_HIMARS_C", ActorType.TruckArtillery},
            {"SD_BP_M1064A3_M121_Woodland_C", ActorType.TrackedAPCArtillery},
            {"SD_BP_M1064_M121_TSF_Woodland_C", ActorType.TrackedAPCArtillery},
            {"BP_M109_Turret_C", ActorType.TrackedAPCArtillery},
            {"SD_BP_M1064_M121_TSF_Desert_C", ActorType.TrackedAPCArtillery},
            {"BP_M109A6_D_C", ActorType.TrackedAPCArtillery},

            // Drones
            {"BP_FlyingDrone_THWK_C", ActorType.Drone},
            {"BP_FlyingDrone_SOF_C", ActorType.Drone},
        };
        #endregion

        #region Icon Bitmaps
        public static readonly Dictionary<ActorType, SKBitmap> BitMaps = new()
        {
            {ActorType.FOBRadio, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.FOBRadio)},
            {ActorType.Hab, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.Hab)},
            {ActorType.AntiAir, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.AntiAir)},
            {ActorType.APC, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.APC)},
            {ActorType.AttackHelicopter, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.AttackHelicopter)},
            {ActorType.LoachCAS, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.LoachCAS)},
            {ActorType.LoachScout, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.LoachScout)},
            {ActorType.Boat, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.Boat)},
            {ActorType.BoatLogistics, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.BoatLogistics)},
            {ActorType.DeployableAntiAir, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.DeployableAntiAir)},
            {ActorType.DeployableAntitank, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.DeployableAntitank)},
            {ActorType.DeployableAntitankGun, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.DeployableAntitankGun)},
            {ActorType.DeployableGMG, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.DeployableGMG)},
            {ActorType.DeployableHellCannon, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.DeployableHellCannon)},
            {ActorType.DeployableHMG, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.DeployableHMG)},
            {ActorType.DeployableMortars, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.DeployableMortars)},
            {ActorType.DeployableRockets, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.DeployableRockets)},
            {ActorType.Drone, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.Drone)},
            {ActorType.IFV, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.IFV)},
            {ActorType.JeepAntiAir, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.JeepAntiAir)},
            {ActorType.JeepAntitank, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.JeepAntitank)},
            {ActorType.JeepArtillery, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.JeepArtillery)},
            {ActorType.JeepLogistics, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.JeepLogistics)},
            {ActorType.JeepTransport, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.JeepTransport)},
            {ActorType.JeepRWSTurret, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.JeepRWSTurret)},
            {ActorType.JeepTurret, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.JeepTurret)},
            {ActorType.Mine, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.Mine)},
            {ActorType.Motorcycle, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.Motorcycle)},
            {ActorType.RallyPoint, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.RallyPoint)},
            {ActorType.Tank, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.Tank)},
            {ActorType.TankMGS, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.TankMGS)},
            {ActorType.TrackedAPC, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.TrackedAPC)},
            {ActorType.TrackedLogistics, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.TrackedLogistics)},
            {ActorType.TrackedAPCArtillery, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.TrackedAPCArtillery)},
            {ActorType.TrackedIFV, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.TrackedIFV)},
            {ActorType.TrackedJeep, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.TrackedJeep)},
            {ActorType.TransportHelicopter, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.TransportHelicopter)},
            {ActorType.TruckAntiAir, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.TruckAntiAir)},
            {ActorType.TruckArtillery, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.TruckArtillery)},
            {ActorType.TruckLogistics, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.TruckLogistics)},
            {ActorType.TruckTransport, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.TruckTransport)},
            {ActorType.TruckTransportArmed, SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(Properties.Resources.TruckTransportArmed)},
        };
        #endregion

        #region Actor Type Categories
        // Deployables that need 45-degree rotation
        public static readonly HashSet<ActorType> RotateBy45Degrees = [
            ActorType.DeployableAntiAir,
            ActorType.DeployableAntitank,
            ActorType.DeployableAntitankGun,
            ActorType.DeployableGMG,
            ActorType.DeployableHellCannon,
            ActorType.DeployableHMG,
            ActorType.DeployableMortars,
            ActorType.DeployableRockets,
        ];

        // Static deployables that don't rotate
        public static readonly HashSet<ActorType> DoNotRotate = [
            ActorType.Hab,
            ActorType.FOBRadio,
            ActorType.Mine,
            ActorType.RallyPoint
        ];

        // All deployable types
        public static readonly HashSet<ActorType> Deployables = [
            ActorType.DeployableAntiAir,
            ActorType.DeployableAntitank,
            ActorType.DeployableAntitankGun,
            ActorType.DeployableGMG,
            ActorType.DeployableHellCannon,
            ActorType.DeployableHMG,
            ActorType.DeployableMortars,
            ActorType.DeployableRockets,
            ActorType.Hab,
            ActorType.FOBRadio,
            ActorType.Mine,
            ActorType.RallyPoint
        ];

        // All vehicle types
        public static readonly HashSet<ActorType> Vehicles = [
            ActorType.Motorcycle,
            ActorType.JeepTransport,
            ActorType.JeepLogistics,
            ActorType.JeepTurret,
            ActorType.JeepArtillery,
            ActorType.JeepAntitank,
            ActorType.JeepAntiAir,
            ActorType.JeepRWSTurret,
            ActorType.TruckTransport,
            ActorType.TruckLogistics,
            ActorType.TruckAntiAir,
            ActorType.TruckArtillery,
            ActorType.TruckTransportArmed,
            ActorType.APC,
            ActorType.TrackedAPC,
            ActorType.TrackedJeep,
            ActorType.TrackedLogistics,
            ActorType.TrackedAPCArtillery,
            ActorType.IFV,
            ActorType.TrackedIFV,
            ActorType.Tank,
            ActorType.TankMGS,
            ActorType.AntiAir,
            ActorType.TransportHelicopter,
            ActorType.AttackHelicopter,
            ActorType.LoachCAS,
            ActorType.LoachScout,
            ActorType.Boat,
            ActorType.BoatLogistics,
        ];
        #endregion
    }
}
