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
        // Source SVG (relative to Icons/default) per actor type. SVGs are white-filled so the radar can tint them per team.
        private static readonly Dictionary<ActorType, string> IconSvgs = new()
        {
            {ActorType.FOBRadio, "deployables/deployable_fob.svg"},
            {ActorType.Hab, "deployables/deployable_hab.svg"},
            {ActorType.AntiAir, "vehicles/map_antiair.svg"},
            {ActorType.APC, "vehicles/map_apc.svg"},
            {ActorType.AttackHelicopter, "vehicles/map_attackhelo.svg"},
            {ActorType.LoachCAS, "vehicles/T_map_helicopter_lightcas.svg"},
            {ActorType.LoachScout, "vehicles/T_map_helicopter_scout.svg"},
            {ActorType.Boat, "vehicles/map_boat.svg"},
            {ActorType.BoatLogistics, "vehicles/T_map_boat_logistics.svg"},
            {ActorType.DeployableAntiAir, "deployables/deployable_AntiAirGun.svg"},
            {ActorType.DeployableAntitank, "deployables/deployable_anti_tank.svg"},
            {ActorType.DeployableAntitankGun, "deployables/deployable_anti_tank_gun.svg"},
            {ActorType.DeployableGMG, "deployables/deployable_GMG.svg"},
            {ActorType.DeployableHellCannon, "deployables/deployable_hellcannon.svg"},
            {ActorType.DeployableHMG, "deployables/deployable_HMG.svg"},
            {ActorType.DeployableMortars, "deployables/deployable_mortars.svg"},
            {ActorType.DeployableRockets, "deployables/deployable_ub32rockets.svg"},
            {ActorType.Drone, "vehicles/map_handhelddrone.svg"},
            {ActorType.IFV, "vehicles/map_ifv.svg"},
            {ActorType.JeepAntiAir, "vehicles/T_map_jeep_antiair.svg"},
            {ActorType.JeepAntitank, "vehicles/map_jeep_antitank.svg"},
            {ActorType.JeepArtillery, "vehicles/map_jeep_artillery.svg"},
            {ActorType.JeepLogistics, "vehicles/map_jeep_logistics.svg"},
            {ActorType.JeepTransport, "vehicles/map_jeep_transport.svg"},
            {ActorType.JeepRWSTurret, "vehicles/map_jeep_turret.svg"},
            {ActorType.JeepTurret, "vehicles/map_jeep_turret.svg"},
            {ActorType.Mine, "deployables/map_mine.svg"},
            {ActorType.Motorcycle, "vehicles/map_motorcycle.svg"},
            {ActorType.RallyPoint, "deployables/rallypoint.svg"},
            {ActorType.Tank, "vehicles/map_tank.svg"},
            {ActorType.TankMGS, "vehicles/T_map_mgs.svg"},
            {ActorType.TrackedAPC, "vehicles/map_trackedapc.svg"},
            {ActorType.TrackedLogistics, "vehicles/T_map_trackedapc_logistics.svg"},
            {ActorType.TrackedAPCArtillery, "vehicles/T_map_trackedapc_artillery.svg"},
            {ActorType.TrackedIFV, "vehicles/map_trackedifv.svg"},
            {ActorType.TrackedJeep, "vehicles/map_trackedjeep.svg"},
            {ActorType.TransportHelicopter, "vehicles/map_transporthelo.svg"},
            {ActorType.TruckAntiAir, "vehicles/map_truck_antiair.svg"},
            {ActorType.TruckArtillery, "vehicles/T_map_truck_artillery.svg"},
            {ActorType.TruckLogistics, "vehicles/map_truck_logistics.svg"},
            {ActorType.TruckTransport, "vehicles/map_truck_transport.svg"},
            {ActorType.TruckTransportArmed, "vehicles/map_truck_transport_armed.svg"},
        };

        public static readonly Dictionary<ActorType, SKBitmap> BitMaps = BuildIconBitmaps();

        private static Dictionary<ActorType, SKBitmap> BuildIconBitmaps()
        {
            var icons = new Dictionary<ActorType, SKBitmap>(IconSvgs.Count);
            foreach (var (type, path) in IconSvgs)
            {
                SKBitmap bitmap = null;
                try { bitmap = SvgIcons.Load(path); } catch { }

                // Fall back to the legacy PNG resource (named after the ActorType) if the SVG can't be rendered.
                if (bitmap == null && Properties.Resources.ResourceManager.GetObject(type.ToString()) is System.Drawing.Bitmap png)
                    bitmap = SkiaSharp.Views.Desktop.Extensions.ToSKBitmap(png);

                if (bitmap != null)
                    icons[type] = bitmap;
            }
            return icons;
        }
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
