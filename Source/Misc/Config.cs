using SkiaSharp;
using squad_dma.Source.Misc;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace squad_dma
{
    /// <summary>
    /// Configuration class for managing application settings and feature states.
    /// Handles loading and saving settings to disk in JSON format.
    /// </summary>
    public class Config
    {
        #region UI Settings
        [JsonPropertyName("defaultZoom")]
        public int DefaultZoom { get; set; } = 100;

        [JsonPropertyName("enemyCount")]
        public bool EnemyCount { get; set; } = false;

        [JsonPropertyName("font")]
        public int Font { get; set; } = 0;

        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; } = 13;

        [JsonPropertyName("paintColors")]
        public Dictionary<string, PaintColor.Colors> PaintColors { get; set; }

        [JsonPropertyName("playerAimLine")]
        public int PlayerAimLineLength { get; set; } = 1000;

        [JsonPropertyName("uiScale")]
        public int UIScale { get; set; } = 100;

        [JsonPropertyName("techMarkerScale")]
        public int TechMarkerScale { get; set; } = 100;

        [JsonPropertyName("vsync")]
        public bool VSync { get; set; } = false;

        [JsonPropertyName("showEnemyDistance")]
        public bool ShowEnemyDistance { get; set; } = true;

        [JsonPropertyName("enableAimview")]
        public bool EnableAimview { get; set; } = true;

        [JsonPropertyName("highAlert")]
        public bool HighAlert { get; set; } = true;

        #endregion

        #region Zoom Settings
        [JsonPropertyName("zoomInKey")]
        public Keys ZoomInKey { get; set; } = Keys.Up;

        [JsonPropertyName("zoomOutKey")]
        public Keys ZoomOutKey { get; set; } = Keys.Down;

        [JsonPropertyName("zoomStep")]
        public int ZoomStep { get; set; } = 1;
        #endregion

        #region ESP Settings
        [JsonPropertyName("enableEsp")]
        public bool EnableEsp { get; set; } = true;

        [JsonPropertyName("espFontSize")]
        public float ESPFontSize { get; set; } = 10f;

        [JsonPropertyName("espShowDistance")]
        public bool EspShowDistance { get; set; } = true;

        [JsonPropertyName("espShowHealth")]
        public bool EspShowHealth { get; set; } = false;

        [JsonPropertyName("espShowBox")]
        public bool EspShowBox { get; set; } = true;

        [JsonPropertyName("espShowNames")]
        public bool EspShowNames { get; set; } = false;

        [JsonPropertyName("espMaxDistance")]
        public float EspMaxDistance { get; set; } = 1000f;

        [JsonPropertyName("espVehicleMaxDistance")]
        public float EspVehicleMaxDistance { get; set; } = 2000f;

        [JsonPropertyName("espShowVehicles")]
        public bool EspShowVehicles { get; set; } = true;

        [JsonPropertyName("espTextColor")]
        public PaintColor.Colors EspTextColor { get; set; }

        [JsonPropertyName("espBones")]
        public bool EspBones { get; set; } = true;

        [JsonPropertyName("espShowAllies")]
        public bool EspShowAllies { get; set; } = false;

        [JsonPropertyName("aimviewPanelX")]
        public int AimviewPanelX { get; set; } = -1; // -1 means use default position

        [JsonPropertyName("aimviewPanelY")]
        public int AimviewPanelY { get; set; } = -1; // -1 means use default position

        [JsonPropertyName("aimviewPanelWidth")]
        public int AimviewPanelWidth { get; set; } = 640; // Default mini 2560x1440 width

        [JsonPropertyName("aimviewPanelHeight")]
        public int AimviewPanelHeight { get; set; } = 360; // Default mini 2560x1440 height

        [JsonPropertyName("firstScopeMagnification")]
        public float FirstScopeMagnification { get; set; } = 4.0f;

        [JsonPropertyName("secondScopeMagnification")]
        public float SecondScopeMagnification { get; set; } = 6.0f;

        [JsonPropertyName("thirdScopeMagnification")]
        public float ThirdScopeMagnification { get; set; } = 12.0f;
        #endregion

        #region Radar Color Settings
        [JsonPropertyName("radarColors")]
        public RadarColors RadarColors { get; set; } = new RadarColors();
        #endregion

        #region Feature States
        [JsonPropertyName("disableSuppression")]
        public bool DisableSuppression { get; set; } = false;

        [JsonPropertyName("setInteractionDistances")]
        public bool SetInteractionDistances { get; set; } = false;

        [JsonPropertyName("allowShootingInMainBase")]
        public bool AllowShootingInMainBase { get; set; } = false;

        [JsonPropertyName("setSpeedHack")]
        public bool SetSpeedHack { get; set; } = false;

        [JsonPropertyName("setAirStuck")]
        public bool SetAirStuck { get; set; } = false;

        [JsonPropertyName("airStuckEnabled")]
        public bool AirStuckEnabled { get; set; } = false;

        [JsonPropertyName("disableCollision")]
        public bool DisableCollision { get; set; } = false;

        [JsonPropertyName("quickZoom")]
        public bool QuickZoom { get; set; } = false;

        [JsonPropertyName("rapidFire")]
        public bool RapidFire { get; set; } = false;

        [JsonPropertyName("infiniteAmmo")]
        public bool InfiniteAmmo { get; set; } = false;

        [JsonPropertyName("quickSwap")]
        public bool QuickSwap { get; set; } = false;

        [JsonPropertyName("forceFullAuto")]
        public bool ForceFullAuto { get; set; } = false;

        [JsonPropertyName("noRecoil")]
        public bool NoRecoil { get; set; } = false;

        [JsonPropertyName("noSpread")]
        public bool NoSpread { get; set; } = false;

        [JsonPropertyName("noSway")]
        public bool NoSway { get; set; } = false;

        [JsonPropertyName("noCameraShake")]
        public bool NoCameraShake { get; set; } = false;

        [JsonPropertyName("instantGrenade")]
        public bool InstantGrenade { get; set; } = false;
        #endregion

        #region Feature Cache
        [JsonPropertyName("originalFov")]
        public float OriginalFov { get; set; } = 0.0f;

        [JsonPropertyName("originalSuppressionPercentage")]
        public float OriginalSuppressionPercentage { get; set; } = 0.0f;

        [JsonPropertyName("originalMaxSuppression")]
        public float OriginalMaxSuppression { get; set; } = -1.0f;

        [JsonPropertyName("originalSuppressionMultiplier")]
        public float OriginalSuppressionMultiplier { get; set; } = 1.0f;

        [JsonPropertyName("originalCameraRecoil")]
        public bool OriginalCameraRecoil { get; set; } = true;

        [JsonPropertyName("originalNoRecoilAnimValues")]
        public Dictionary<string, float> OriginalNoRecoilAnimValues { get; set; } = new Dictionary<string, float>();

        [JsonPropertyName("originalNoRecoilWeaponValues")]
        public Dictionary<string, float> OriginalNoRecoilWeaponValues { get; set; } = new Dictionary<string, float>();

        [JsonPropertyName("originalNoSpreadAnimValues")]
        public Dictionary<string, float> OriginalNoSpreadAnimValues { get; set; } = new Dictionary<string, float>();

        [JsonPropertyName("originalNoSpreadWeaponValues")]
        public Dictionary<string, float> OriginalNoSpreadWeaponValues { get; set; } = new Dictionary<string, float>();

        [JsonPropertyName("originalNoSwayAnimValues")]
        public Dictionary<string, float> OriginalNoSwayAnimValues { get; set; } = new Dictionary<string, float>();

        [JsonPropertyName("originalNoSwayWeaponValues")]
        public Dictionary<string, float> OriginalNoSwayWeaponValues { get; set; } = new Dictionary<string, float>();

        [JsonPropertyName("originalTimeBetweenShots")]
        public float OriginalTimeBetweenShots { get; set; } = 0.0f;

        [JsonPropertyName("originalTimeBetweenSingleShots")]
        public float OriginalTimeBetweenSingleShots { get; set; } = 0.0f;

        [JsonPropertyName("originalVehicleTimeBetweenShots")]
        public float OriginalVehicleTimeBetweenShots { get; set; } = 0.0f;

        [JsonPropertyName("originalVehicleTimeBetweenSingleShots")]
        public float OriginalVehicleTimeBetweenSingleShots { get; set; } = 0.0f;

        [JsonPropertyName("originalMovementMode")]
        public byte OriginalMovementMode { get; set; } = 1; // MOVE_Walking

        [JsonPropertyName("originalReplicatedMovementMode")]
        public byte OriginalReplicatedMovementMode { get; set; } = 1; // MOVE_Walking

        [JsonPropertyName("originalReplicateMovement")]
        public byte OriginalReplicateMovement { get; set; } = 16;

        [JsonPropertyName("originalMaxFlySpeed")]
        public float OriginalMaxFlySpeed { get; set; } = 200.0f;

        [JsonPropertyName("originalMaxCustomMovementSpeed")]
        public float OriginalMaxCustomMovementSpeed { get; set; } = 600.0f;

        [JsonPropertyName("originalMaxAcceleration")]
        public float OriginalMaxAcceleration { get; set; } = 500.0f;

        [JsonPropertyName("originalCollisionEnabled")]
        public byte OriginalCollisionEnabled { get; set; } = 1; // QueryOnly

        [JsonPropertyName("originalFireModes")]
        public int[] OriginalFireModes { get; set; } = null;

        [JsonPropertyName("originalManualBolt")]
        public bool OriginalManualBolt { get; set; } = false;

        [JsonPropertyName("originalRequireAdsToShoot")]
        public bool OriginalRequireAdsToShoot { get; set; } = false;

        [JsonPropertyName("originalGrenadeValues")]
        public Dictionary<string, float> OriginalGrenadeValues { get; set; } = new Dictionary<string, float>();

        [JsonPropertyName("originalGrenadeAnimValues")]
        public Dictionary<string, float> OriginalGrenadeAnimValues { get; set; } = new Dictionary<string, float>();

        [JsonPropertyName("originalGrenadeItemValues")]
        public Dictionary<string, byte> OriginalGrenadeItemValues { get; set; } = new Dictionary<string, byte>();

        /// <summary>
        /// Clears all cached feature values to ensure a clean state on game/app restart.
        /// This should be called when the game closes or the application is terminated.
        /// </summary>
        public void ClearFeatureCaches()
        {
            OriginalFov = 0.0f;
            OriginalSuppressionPercentage = 0.0f;
            OriginalMaxSuppression = -1.0f;
            OriginalSuppressionMultiplier = 1.0f;
            OriginalCameraRecoil = true;
            OriginalNoRecoilAnimValues = new Dictionary<string, float>();
            OriginalNoRecoilWeaponValues = new Dictionary<string, float>();
            OriginalNoSpreadAnimValues = new Dictionary<string, float>();
            OriginalNoSpreadWeaponValues = new Dictionary<string, float>();
            OriginalNoSwayAnimValues = new Dictionary<string, float>();
            OriginalNoSwayWeaponValues = new Dictionary<string, float>();
            OriginalGrenadeValues = new Dictionary<string, float>();
            OriginalGrenadeAnimValues = new Dictionary<string, float>();
            OriginalGrenadeItemValues = new Dictionary<string, byte>();
            OriginalTimeBetweenShots = 0.0f;
            OriginalTimeBetweenSingleShots = 0.0f;
            OriginalVehicleTimeBetweenShots = 0.0f;
            OriginalVehicleTimeBetweenSingleShots = 0.0f;
            OriginalMovementMode = 1;
            OriginalReplicatedMovementMode = 1;
            OriginalReplicateMovement = 16;
            OriginalMaxFlySpeed = 200.0f;
            OriginalMaxCustomMovementSpeed = 600.0f;
            OriginalMaxAcceleration = 500.0f;
            OriginalCollisionEnabled = 1;
            OriginalFireModes = null;
            OriginalManualBolt = false;
            OriginalRequireAdsToShoot = false;
        }
        #endregion

        #region Keybinds
        [JsonPropertyName("keybindSpeedHack")]
        public Keys KeybindSpeedHack { get; set; } = Keys.None;

        [JsonPropertyName("keybindAirStuck")]
        public Keys KeybindAirStuck { get; set; } = Keys.None;

        [JsonPropertyName("keybindQuickZoom")]
        public Keys KeybindQuickZoom { get; set; } = Keys.None;

        [JsonPropertyName("keybindToggleEnemyDistance")]
        public Keys KeybindToggleEnemyDistance { get; set; } = Keys.None;

        [JsonPropertyName("keybindToggleMap")]
        public Keys KeybindToggleMap { get; set; } = Keys.None;

        [JsonPropertyName("keybindToggleFullscreen")]
        public Keys KeybindToggleFullscreen { get; set; } = Keys.None;

        [JsonPropertyName("keybindDumpNames")]
        public Keys KeybindDumpNames { get; set; } = Keys.None;

        [JsonPropertyName("keybindZoomIn")]
        public Keys KeybindZoomIn { get; set; } = Keys.Up;

        [JsonPropertyName("keybindZoomOut")]
        public Keys KeybindZoomOut { get; set; } = Keys.Down;
        #endregion

        #region Private Fields
        [JsonIgnore]
        public Dictionary<string, PaintColor.Colors> DefaultPaintColors = new Dictionary<string, PaintColor.Colors>()
        {
            ["EspText"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 255 }
        };

        [JsonIgnore]
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonKeyEnumConverter() },
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        [JsonIgnore]
        private static readonly object _lock = new();

        [JsonIgnore]
        private const string SettingsDirectory = "Configuration";
        #endregion

        #region Public Methods
        public Config()
        {
            HighAlert = true;
            ShowEnemyDistance = true;
            EnableAimview = true;
            DefaultZoom = 100;
            EnemyCount = false;
            Font = 0;
            FontSize = 13;
            PaintColors = DefaultPaintColors;
            PlayerAimLineLength = 1500;
            EspShowNames = false;
            UIScale = 100;
            VSync = false;

            // Initialize ESP settings
            EnableEsp = false;
            ESPFontSize = 10f;
            EspShowDistance = true;
            EspShowHealth = false;
            EspShowBox = true;
            EspMaxDistance = 1000f;
            EspVehicleMaxDistance = 2000f;
            EspShowVehicles = true;
            EspTextColor = DefaultPaintColors["EspText"];
            EspBones = true;
            EspShowAllies = false;
            AimviewPanelX = -1;
            AimviewPanelY = -1;
            FirstScopeMagnification = 4.0f;
            SecondScopeMagnification = 6.0f;
            ThirdScopeMagnification = 12.0f;
            
            // Initialize radar colors with defaults
            RadarColors = new RadarColors();
        }

        /// <summary>
        /// Attempts to load the configuration from disk.
        /// </summary>
        /// <param name="config">The loaded configuration if successful, or a new instance if not.</param>
        /// <returns>True if the configuration was successfully loaded, false otherwise.</returns>
        public static bool TryLoadConfig(out Config config)
        {
            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(SettingsDirectory);
                    var path = Path.Combine(SettingsDirectory, "Settings.json");

                    if (!File.Exists(path))
                    {
                        config = new Config();
                        SaveConfig(config);
                        return true;
                    }

                    config = JsonSerializer.Deserialize<Config>(File.ReadAllText(path), _jsonOptions);
                    return true;
                }
                catch
                {
                    config = new Config();
                    return false;
                }
            }
        }

        /// <summary>
        /// Saves the configuration to disk.
        /// </summary>
        /// <param name="config">The configuration to save.</param>
        public static void SaveConfig(Config config)
        {
            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(SettingsDirectory);
                    var settingsPath = Path.Combine(SettingsDirectory, "Settings.json");
                    var json = JsonSerializer.Serialize(config, _jsonOptions);
                    File.WriteAllText(settingsPath, json);
                    Logger.Info($"Successfully saved config to {settingsPath}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to save config: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Clears all feature caches and saves the config.
        /// This should be called when the game closes or the application is terminated.
        /// </summary>
        public static void ClearCache()
        {
            if (TryLoadConfig(out var config))
            {
                config.ClearFeatureCaches();
                Logger.Info("Cleared caches");
                SaveConfig(config);
            }
        }
        #endregion
    }

    /// <summary>
    /// Configuration class for radar color settings.
    /// </summary>
    public class RadarColors
    {
        [JsonPropertyName("squadMembers")]
        public ColorConfig SquadMembers { get; set; } = new ColorConfig(0, 128, 0); // Green 

        [JsonPropertyName("friendlyPlayers")]
        public ColorConfig FriendlyPlayers { get; set; } = new ColorConfig(0, 187, 254); // Light Blue

        [JsonPropertyName("enemyPlayers")]
        public ColorConfig EnemyPlayers { get; set; } = new ColorConfig(255, 107, 107); // Light Red

        [JsonPropertyName("unknownPlayers")]
        public ColorConfig UnknownPlayers { get; set; } = new ColorConfig(128, 0, 128); // Purple

        [JsonPropertyName("friendlyVehicles")]
        public ColorConfig FriendlyVehicles { get; set; } = new ColorConfig(255, 255, 255); // White

        [JsonPropertyName("enemyVehicles")]
        public ColorConfig EnemyVehicles { get; set; } = new ColorConfig(255, 107, 107); // Light Red

        [JsonPropertyName("unclaimedVehicles")]
        public ColorConfig UnclaimedVehicles { get; set; } = new ColorConfig(255, 255, 255); // White

        [JsonPropertyName("regularProjectiles")]
        public ColorConfig RegularProjectiles { get; set; } = new ColorConfig(255, 165, 0); // Orange

        [JsonPropertyName("aaProjectiles")]
        public ColorConfig AAProjectiles { get; set; } = new ColorConfig(255, 165, 0); // Orange

        [JsonPropertyName("smallProjectiles")]
        public ColorConfig SmallProjectiles { get; set; } = new ColorConfig(255, 0, 255); // Magenta

        [JsonPropertyName("enemyPlayerDistanceText")]
        public ColorConfig EnemyPlayerDistanceText { get; set; } = new ColorConfig(255, 107, 107); // Light Red

        [JsonPropertyName("vehicleDistanceText")]
        public ColorConfig VehicleDistanceText { get; set; } = new ColorConfig(255, 255, 255); // White

        [JsonPropertyName("deadMarkers")]
        public ColorConfig DeadMarkers { get; set; } = new ColorConfig(255, 255, 255); // White

        [JsonPropertyName("adminMarkers")]
        public ColorConfig AdminMarkers { get; set; } = new ColorConfig(255, 0, 128); // Bright Magenta (#FF0080)
    }

    /// <summary>
    /// Simple color configuration class with RGB values.
    /// </summary>
    public class ColorConfig
    {
        [JsonPropertyName("r")]
        public byte R { get; set; }

        [JsonPropertyName("g")]
        public byte G { get; set; }

        [JsonPropertyName("b")]
        public byte B { get; set; }

        public ColorConfig() { }

        public ColorConfig(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        /// <summary>
        /// Converts to SKColor for use in rendering.
        /// </summary>
        public SKColor ToSKColor() => new SKColor(R, G, B);

        /// <summary>
        /// Converts to System.Drawing.Color for use in UI controls.
        /// </summary>
        public System.Drawing.Color ToDrawingColor() => System.Drawing.Color.FromArgb(R, G, B);
    }

    /// <summary>
    /// JSON converter for the Keys enum to ensure proper serialization.
    /// </summary>
    public class JsonKeyEnumConverter : JsonConverter<Keys>
    {
        /// <summary>
        /// Reads a Keys enum value from JSON.
        /// </summary>
        public override Keys Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => (Keys)reader.GetInt32();

        /// <summary>
        /// Writes a Keys enum value to JSON.
        /// </summary>
        public override void Write(Utf8JsonWriter writer, Keys value, JsonSerializerOptions options)
            => writer.WriteNumberValue((int)value);
    }
}