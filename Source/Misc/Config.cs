using System.Text.Json.Serialization;
using System.Text.Json;

namespace squad_dma
{
    public class Config
    {
        #region Json Properties
        [JsonPropertyName("aimviewEnabled")]
        public bool AimviewEnabled { get; set; }

        [JsonPropertyName("defaultZoom")]
        public int DefaultZoom { get; set; }

        [JsonPropertyName("enemyCount")]
        public bool EnemyCount { get; set; }

        [JsonPropertyName("font")]
        public int Font { get; set; }

        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; }

        [JsonPropertyName("paintColors")]
        public Dictionary<string, PaintColor.Colors> PaintColors { get; set; }

        [JsonPropertyName("playerAimLine")]
        public int PlayerAimLineLength { get; set; }

        [JsonPropertyName("showNames")]
        public bool ShowNames { get; set; }

        [JsonPropertyName("showRadarStats")]
        public bool ShowRadarStats { get; set; }

        [JsonPropertyName("uiScale")]
        public int UIScale { get; set; }

        [JsonPropertyName("zoomSensitivity")]
        public int ZoomSensitivity { get; set; }

        [JsonPropertyName("vsync")]
        public bool VSync { get; set; }

        // ESP
        [JsonPropertyName("espFontSize")]
        public int ESPFontSize { get; set; }

        [JsonPropertyName("espShowDistance")]
        public bool EspShowDistance { get; set; }

        [JsonPropertyName("espShowHealth")]
        public bool EspShowHealth { get; set; }

        [JsonPropertyName("espMaxDistance")]
        public float EspMaxDistance { get; set; }

        [JsonPropertyName("espTextColor")]
        public PaintColor.Colors EspTextColor { get; set; }

        [JsonPropertyName("selectedTeam")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Team SelectedTeam { get; set; }
        #endregion

        #region Json Ignore
        [JsonIgnore]
        public Dictionary<string, PaintColor.Colors> DefaultPaintColors = new Dictionary<string, PaintColor.Colors>()
        {
            ["Primary"] = new PaintColor.Colors { A = 255, R = 80, G = 80, B = 80 },
            ["PrimaryDark"] = new PaintColor.Colors { A = 255, R = 50, G = 50, B = 50 },
            ["PrimaryLight"] = new PaintColor.Colors { A = 255, R = 130, G = 130, B = 130 },
            ["Accent"] = new PaintColor.Colors { A = 255, R = 255, G = 128, B = 0 },
            ["EspText"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 255 } // ESP
        };

        [JsonIgnore]
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true
        };

        [JsonIgnore]
        private static readonly object _lock = new();

        [JsonIgnore]
        private const string SettingsDirectory = "Configuration\\";
        #endregion

        public Config()
        {
            AimviewEnabled = false;
            DefaultZoom = 100;
            EnemyCount = false;
            Font = 0;
            FontSize = 13;
            PaintColors = DefaultPaintColors;
            PlayerAimLineLength = 1000;
            ShowNames = false;
            ShowRadarStats = false;
            UIScale = 100;
            ZoomSensitivity = 25;
            VSync = false;

            // ESP

            ESPFontSize = 10;
            EspShowDistance = true;
            EspShowHealth = false;
            EspMaxDistance = 1000f; // 1000M is max
            EspTextColor = DefaultPaintColors["EspText"];
            SelectedTeam = Team.Unknown;
        }

        /// <summary>
        /// Attempt to load Config.json
        /// </summary>
        /// <param name="config">'Config' instance to populate.</param>
        /// <returns></returns>
        public static bool TryLoadConfig(out Config config)
        {
            lock (_lock)
            {
                if (!Directory.Exists(SettingsDirectory))
                    Directory.CreateDirectory(SettingsDirectory);

                try
                {
                    if (!File.Exists($"{SettingsDirectory}Settings.json"))
                        throw new FileNotFoundException("Settings.json does not exist!");

                    var json = File.ReadAllText($"{SettingsDirectory}Settings.json");

                    config = JsonSerializer.Deserialize<Config>(json);
                    return true;
                }
                catch (Exception ex)
                {
                    Program.Log($"TryLoadConfig - {ex.Message}\n{ex.StackTrace}");
                    config = null;
                    return false;
                }
            }
        }
        /// <summary>
        /// Save to Config.json
        /// </summary>
        /// <param name="config">'Config' instance</param>
        public static void SaveConfig(Config config)
        {
            lock (_lock)
            {
                if (!Directory.Exists(SettingsDirectory))
                    Directory.CreateDirectory(SettingsDirectory);

                //Program.Log($"Saving config with SelectedTeam: {config.SelectedTeam}");
                var json = JsonSerializer.Serialize<Config>(config, _jsonOptions);
                //Program.Log($"JSON to save: {json}");
                File.WriteAllText($"{SettingsDirectory}Settings.json", json);
            }
        }
    }
}
