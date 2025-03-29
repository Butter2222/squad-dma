using DarkModeForms;
using MaterialSkin.Controls;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;

namespace squad_dma
{
    public partial class MainForm : Form
    {
        private Game _game;
        private readonly Config _config;
        private readonly SKGLControl _mapCanvas;
        private readonly Stopwatch _fpsWatch = new();
        private readonly object _renderLock = new();
        private readonly object _loadMapBitmapsLock = new();
        private readonly System.Timers.Timer _mapChangeTimer = new(100);
        private readonly List<Map> _maps = new(); // Contains all maps from \\Maps folder
        private readonly DarkModeCS _darkmode;
        private bool _isFreeMapToggled = false;
        private float _uiScale = 1.0f;
        private UActor _closestPlayerToMouse = null;
        private bool _isDragging = false;
        private Point _lastMousePosition = Point.Empty;
        private int _fps = 0;
        private int _mapSelectionIndex = 0;
        private Map _selectedMap;
        private SKBitmap[] _loadedBitmaps;
        private MapPosition _mapPanPosition = new();
        private readonly List<PointOfInterest> _pointsOfInterest = new();
        private PointOfInterest _hoveredPoi;
        private const float DRAG_SENSITIVITY = 3.5f;
        private const double PAN_SMOOTHNESS = 0.1;
        private const int PAN_INTERVAL = 10;
        private SKPoint targetPanPosition;
        private System.Windows.Forms.Timer panTimer;
        private int _lastFriendlyTickets = -1;
        private int _lastEnemyTickets = -1;
        public int ZoomStep { get; set; } = 5; 
        public float ZoomSensitivity { get; set; } = 1.0f;

        private EspOverlay _espOverlay;

        #region Getters
        private bool Ready
        {
            get => Memory.Ready;
        }

        private bool InGame
        {
            get => Memory.InGame;
        }
        private string MapName
        {
            get => Memory.MapName;
        }

        private UActor LocalPlayer
        {
            get => Memory.LocalPlayer;
        }

        private ReadOnlyDictionary<ulong, UActor> AllActors
        {
            get => Memory.Actors;
        }

        private Vector3 AbsoluteLocation
        {
            get => Memory.AbsoluteLocation;
        }

        public void AddPointOfInterest(Vector3 position, string name)
        {
            _pointsOfInterest.Add(new PointOfInterest(position, name));
        }

        public void RemovePointOfInterest(string name)
        {
            var poi = _pointsOfInterest.FirstOrDefault(p => p.Name == name);
            if (poi != null)
            {
                _pointsOfInterest.Remove(poi);
            }
        }

        public void ClearPointsOfInterest()
        {
            _pointsOfInterest.Clear();
            _mapCanvas.Invalidate();
        }

        #endregion

        #region Constructor
        public MainForm(Game game)
        {
            _config = Program.Config;
            _game = game;
            InputManager.SetVmmInstance(Memory.vmmInstance);
            InputManager.InitInputManager();
            if (!InputManager.InitInputManager())
            {
                Console.WriteLine("Failed to initialize input manager!");
            }

            InitializeComponent();

            if (_config.EnableEsp)
            {
                _espOverlay = new EspOverlay();
                _espOverlay.Show();
            }

            LoadConfig();
            SetDarkMode(ref _darkmode);
            this.Size = new Size(1280, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Normal;

            _mapCanvas = new SKGLControl()
            {
                Size = new Size(50, 50),
                Dock = DockStyle.Fill,
                VSync = false
            };
            tabRadar.Controls.Add(_mapCanvas);
            chkMapFree.Parent = _mapCanvas;

            LoadMaps();

            _mapChangeTimer.AutoReset = false;
            _mapChangeTimer.Elapsed += MapChangeTimer_Elapsed;

            this.DoubleBuffered = true;
            this.Shown += frmMain_Shown;

            _mapCanvas.PaintSurface += skMapCanvas_PaintSurface;
            ticketsPanel.Paint += ticketsPanel_Paint;
            _mapCanvas.MouseMove += skMapCanvas_MouseMove;
            _mapCanvas.MouseDown += skMapCanvas_MouseDown;
            _mapCanvas.MouseDoubleClick += skMapCanvas_MouseDoubleClick;
            _mapCanvas.MouseUp += skMapCanvas_MouseUp;

            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            _fpsWatch.Start();

            panTimer = new System.Windows.Forms.Timer();
            panTimer.Interval = PAN_INTERVAL;
            panTimer.Tick += PanTimer_Tick;

            var inputTimer = new System.Windows.Forms.Timer { Interval = 10 };
            inputTimer.Tick += InputUpdate_Tick;
            inputTimer.Start();

            var _ticketUpdateTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _ticketUpdateTimer.Tick += (s, e) => UpdateTicketsDisplay();
            _ticketUpdateTimer.Start();
        }

        #endregion

        #region Overrides
        private void SetDarkMode(ref DarkModeCS darkmode)
        {
            darkmode = new DarkModeCS(this);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Enabled = false;

            Program.Log("Closing form");
            if (_espOverlay != null && !_espOverlay.IsDisposed)
            {
                _espOverlay.Close();
                _espOverlay = null;
            }

            CleanupLoadedBitmaps();
            Config.SaveConfig(_config); 
            Memory.Shutdown(); 
            e.Cancel = false; 
            base.OnFormClosing(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) => keyData switch
        {
            Keys.F5 => ToggleMap(),
#if DEBUG
            Keys.F6 => DumpNames(),
#endif         
            Keys.F11 => ToggleFullscreen(FormBorderStyle is FormBorderStyle.Sizable),
            _ => base.ProcessCmdKey(ref msg, keyData),
        };

        private void InputUpdate_Tick(object sender, EventArgs e)
        {
            if (!InputManager.IsManagerLoaded)
                return;

            InputManager.UpdateKeys();

            // Handle Zoom
            if (InputManager.IsKeyDown(_config.ZoomInKey))
                ZoomIn(_config.ZoomStep);
            else if (InputManager.IsKeyDown(_config.ZoomOutKey))
                ZoomOut(_config.ZoomStep);

            // Handle Functions
            if (InputManager.IsKeyDown(Keys.F5))
                ToggleMap();
#if DEBUG
            if (InputManager.IsKeyDown(Keys.F6))
                DumpNames();
#endif        
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (tabControl.SelectedIndex == 0)
            {
                int zoomStep = 3;
                if (e.Delta < 0)
                    ZoomOut(zoomStep); 
                else if (e.Delta > 0)
                    ZoomIn(zoomStep); 

                if (this._isFreeMapToggled && e.Delta < 0)
                {
                    var mousePos = this._mapCanvas.PointToClient(Cursor.Position);
                    var mapParams = GetMapLocation();
                    var mapMousePos = new SKPoint(
                        mapParams.Bounds.Left + mousePos.X / mapParams.XScale,
                        mapParams.Bounds.Top + mousePos.Y / mapParams.YScale
                    );

                    this.targetPanPosition = mapMousePos;
                    if (!this.panTimer.Enabled)
                        this.panTimer.Start();
                }
                return;
            }
            base.OnMouseWheel(e);
        }
        #endregion

        #region GUI Events / Functions
        #region General Helper Functions
        private bool ToggleMap()
        {
            if (!btnToggleMap.Enabled)
                return false;

            if (_mapSelectionIndex == _maps.Count - 1)
                _mapSelectionIndex = 0; // Start over when end of maps reached
            else
                _mapSelectionIndex++; // Move onto next map

            tabRadar.Text = $"Radar ({_maps[_mapSelectionIndex].Name})";
            _mapChangeTimer.Restart(); // Start delay
            ClearPointsOfInterest();
            Program.Log("Toggled Map");

            return true;
        }

        private void InitiateUIScaling()
        {
            _uiScale = (.01f * _config.UIScale);

            #region Update Paints/Text
            SKPaints.TextBaseOutline.StrokeWidth = 2 * _uiScale;
            SKPaints.TextRadarStatus.TextSize = 48 * _uiScale;
            SKPaints.PaintBase.StrokeWidth = 3 * _uiScale;
            SKPaints.PaintTransparentBacker.StrokeWidth = 1 * _uiScale;
            #endregion

            InitiateFontSize();
        }

        private void InitiateFont()
        {
            var fontToUse = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
            SKPaints.TextBase.Typeface = fontToUse;
            SKPaints.TextBaseOutline.Typeface = fontToUse;
            SKPaints.TextRadarStatus.Typeface = fontToUse;
        }

        private void InitiateFontSize()
        {
            SKPaints.TextBase.TextSize = _config.FontSize * _uiScale;
            SKPaints.TextBaseOutline.TextSize = _config.FontSize * _uiScale;
        }

        private DialogResult ShowErrorDialog(string message)
        {
            return new MaterialDialog(this, "Error", message, "OK", false, "", true).ShowDialog(this);
        }

        private void LoadMaps()
        {
            var dir = new DirectoryInfo($"{Environment.CurrentDirectory}\\Maps");
            if (!dir.Exists)
                dir.Create();

            var configs = dir.GetFiles("*.json");
            if (configs.Length == 0)
                throw new IOException("No .json map configs found!");

            foreach (var config in configs)
            {
                var name = Path.GetFileNameWithoutExtension(config.Name);
                var mapConfig = MapConfig.LoadFromFile(config.FullName);
                var map = new Map(name.ToUpper(), mapConfig, config.FullName);

                map.ConfigFile.MapLayers = map.ConfigFile
                    .MapLayers
                    .OrderBy(x => x.MinHeight)
                    .ToList();

                _maps.Add(map);
            }
        }
        private void LoadConfig()
        {
            #region Settings
            #region General
            // User Interface
            chkShowEnemyDistance.Checked = _config.ShowEnemyDistance;
            chkShowEnemyDistance.CheckedChanged += ChkShowEnemyDistance_CheckedChanged;
            trkAimLength.Value = _config.PlayerAimLineLength;
            trkUIScale.Value = _config.UIScale;

            // ESP
            chkEnableEsp.Checked = _config.EnableEsp;
            trkEspMaxDistance.Value = (int)_config.EspMaxDistance;
            lblEspMaxDistance.Text = $"Max Distance: {_config.EspMaxDistance}m";
            chkShowAllies.Checked = _config.EspShowAllies;
            chkEspShowNames.Checked = _config.ShowNames;
            chkEspShowDistance.Checked = _config.EspShowDistance;
            chkEspShowHealth.Checked = _config.EspShowHealth;
            txtEspFontSize.Text = _config.ESPFontSize.ToString();
            txtEspColorA.Text = _config.EspTextColor.A.ToString();
            txtEspColorR.Text = _config.EspTextColor.R.ToString();
            txtEspColorG.Text = _config.EspTextColor.G.ToString();
            txtEspColorB.Text = _config.EspTextColor.B.ToString();
            #endregion
            #endregion
            InitiateFont();
            InitiateUIScaling();
        }

        private bool ToggleFullscreen(bool toFullscreen)
        {
            var screen = Screen.FromControl(this);

            if (toFullscreen)
            {
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                Location = new Point(screen.Bounds.Left, screen.Bounds.Top);
                Width = screen.Bounds.Width;
                Height = screen.Bounds.Height;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                WindowState = FormWindowState.Normal;
                Width = 1280;
                Height = 720;
                CenterToScreen();
            }

            return true;
        }
        #endregion


        #region General Event Handlers
        private async void frmMain_Shown(object sender, EventArgs e)
        {
            while (_mapCanvas.GRContext is null)
                await Task.Delay(1);

            _mapCanvas.GRContext.SetResourceCacheLimit(1610612736); // Fixes low FPS on big maps

            while (true)
            {
                await Task.Run(() => Thread.SpinWait(25000)); // High performance async delay
                _mapCanvas.Refresh(); // draw next frame
            }
        }

        private void MapChangeTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.BeginInvoke(
                new MethodInvoker(
                    delegate
                    {
                        btnToggleMap.Enabled = false;
                        btnToggleMap.Text = "Loading...";
                    }
                )
            );

            lock (_renderLock)
            {
                try
                {
                    _selectedMap = _maps[_mapSelectionIndex]; // Swap map

                    if (_loadedBitmaps is not null)
                    {
                        foreach (var bitmap in _loadedBitmaps)
                            bitmap?.Dispose(); // Cleanup resources
                    }

                    _loadedBitmaps = new SKBitmap[_selectedMap.ConfigFile.MapLayers.Count];

                    for (int i = 0; i < _loadedBitmaps.Length; i++)
                    {
                        using (
                            var stream = File.Open(
                                _selectedMap.ConfigFile.MapLayers[i].Filename,
                                FileMode.Open,
                                FileAccess.Read))
                        {
                            _loadedBitmaps[i] = SKBitmap.Decode(stream); // Load new bitmap(s)
                            _loadedBitmaps[i].SetImmutable();
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"ERROR loading {_selectedMap.ConfigFile.MapLayers[0].Filename}: {ex}"
                    );
                }
                finally
                {
                    this.BeginInvoke(
                        new MethodInvoker(
                            delegate
                            {
                                btnToggleMap.Enabled = true;
                                btnToggleMap.Text = "Toggle Map (F5)";
                            }
                        )
                    );
                }
            }
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        { }
        #endregion

        #region Radar Tab
        #region Helper Functions
        private void UpdateWindowTitle()
        {
            bool inGame = this.InGame;
            var localPlayer = this.LocalPlayer;

            if (inGame && localPlayer is not null)
            {
                UpdateSelectedMap();

                if (_fpsWatch.ElapsedMilliseconds >= 1000) 
                {
                    // Purge resources to mitigate memory leak
                    _mapCanvas.GRContext.PurgeResources();

                    var fps = _fps;
                    var memTicks = Memory.Ticks;

                    this.Invoke((MethodInvoker)delegate
                    {
                        this.Text = $"Squad DMA ({fps} fps)";
                    });

                    _fpsWatch.Restart(); 
                    _fps = 0; 
                }
                else
                {
                    _fps++; 
                }
            }
        }

        private void UpdateTicketsDisplay()
        {
            if (!InGame || _game == null)
            {
                ticketsPanel.Visible = false;
                return;
            }

            var (friendly, enemy) = _game.GetTeamTickets();

            if (friendly != _lastFriendlyTickets || enemy != _lastEnemyTickets)
            {
                this.Invoke((MethodInvoker)delegate {
                    ticketsPanel.Visible = true;
                    _lastFriendlyTickets = friendly;
                    _lastEnemyTickets = enemy;
                    ticketsPanel.Invalidate();
                });
            }
        }

        private void UpdateSelectedMap()
        {
            string currentMap = this.MapName;

            if (_selectedMap is null || !_selectedMap.ConfigFile.MapID.Any(id => id.Equals(currentMap, StringComparison.OrdinalIgnoreCase)))
            {
                var selectedMap = _maps.FirstOrDefault(x => x.ConfigFile.MapID.Any(id => id.Equals(currentMap, StringComparison.OrdinalIgnoreCase)));

                if (selectedMap is not null)
                {
                    _selectedMap = selectedMap;

                    CleanupLoadedBitmaps();
                    ClearPointsOfInterest();
                    LoadMapBitmaps();
                }
                else
                {
                    Console.WriteLine($"No matching map found for: {currentMap}"); // Debug logging
                }
            }
        }

        private void CleanupLoadedBitmaps()
        {
            if (_loadedBitmaps is not null)
            {
                Parallel.ForEach(_loadedBitmaps, bitmap =>
                {
                    bitmap?.Dispose();
                });

                _loadedBitmaps = null;
            }
        }

        private void LoadMapBitmaps()
        {
            var mapLayers = _selectedMap.ConfigFile.MapLayers;
            _loadedBitmaps = new SKBitmap[mapLayers.Count];

            Parallel.ForEach(mapLayers, (mapLayer, _, _) =>
            {
                lock (_loadMapBitmapsLock)
                {
                    try
                    {
                        using (var stream = File.Open(mapLayer.Filename, FileMode.Open, FileAccess.Read))
                        {
                            _loadedBitmaps[mapLayers.IndexOf(mapLayer)] = SKBitmap.Decode(stream);
                            _loadedBitmaps[mapLayers.IndexOf(mapLayer)].SetImmutable();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading map layer: {ex.Message}");
                    }
                }
            });
        }

        private bool IsReadyToRender()
        {
            bool isReady = this.Ready;
            bool inGame = this.InGame;
            bool localPlayerExists = this.LocalPlayer is not null;
            bool selectedMapLoaded = this._selectedMap is not null;

            if (!isReady)
                return false; // Game process not running

            if (!inGame)
                return false; // Waiting for game start

            if (!localPlayerExists)
                return false; // Cannot find local player

            if (!selectedMapLoaded)
                return false; // Map not loaded

            return true; // Ready to render
        }

        private int GetMapLayerIndex(float playerHeight)
        {
            for (int i = _loadedBitmaps.Length - 1; i >= 0; i--)
            {
                if (playerHeight > _selectedMap.ConfigFile.MapLayers[i].MinHeight)
                {
                    return i;
                }
            }

            return 0; // Default to the first layer if no match is found
        }

        private MapParameters GetMapParameters(MapPosition localPlayerPos)
        {
            int mapLayerIndex = GetMapLayerIndex(localPlayerPos.Height);

            var bitmap = _loadedBitmaps[mapLayerIndex];
            float zoomFactor = 0.01f * _config.DefaultZoom;
            float zoomWidth = bitmap.Width * zoomFactor;
            float zoomHeight = bitmap.Height * zoomFactor;

            var bounds = new SKRect(
                Math.Max(Math.Min(localPlayerPos.X, bitmap.Width - zoomWidth / 2) - zoomWidth / 2, 0),
                Math.Max(Math.Min(localPlayerPos.Y, bitmap.Height - zoomHeight / 2) - zoomHeight / 2, 0),
                Math.Min(Math.Max(localPlayerPos.X, zoomWidth / 2) + zoomWidth / 2, bitmap.Width),
                Math.Min(Math.Max(localPlayerPos.Y, zoomHeight / 2) + zoomHeight / 2, bitmap.Height)
            ).AspectFill(_mapCanvas.CanvasSize);

            return new MapParameters
            {
                UIScale = _uiScale,
                MapLayerIndex = mapLayerIndex,
                Bounds = bounds,
                XScale = (float)_mapCanvas.Width / bounds.Width, // Set scale for this frame
                YScale = (float)_mapCanvas.Height / bounds.Height // Set scale for this frame
            };
        }

        private MapParameters GetMapLocation()
        {
            var localPlayer = this.LocalPlayer;
            if (localPlayer is not null)
            {
                var localPlayerPos = localPlayer.Position + AbsoluteLocation;
                var localPlayerMapPos = localPlayerPos.ToMapPos(_selectedMap);

                if (_isFreeMapToggled)
                {
                    _mapPanPosition.Height = localPlayerMapPos.Height;
                    return GetMapParameters(_mapPanPosition);
                }
                else
                    return GetMapParameters(localPlayerMapPos);
            }
            else
            {
                return GetMapParameters(_mapPanPosition);
            }
        }

        private void DrawMap(SKCanvas canvas)
        {
            if (grpMapSetup.Visible) // Print coordinates (to make it easy to setup JSON configs)
            {
                var localPlayer = this.LocalPlayer;
                var localPlayerPos = localPlayer.Position + AbsoluteLocation;
                grpMapSetup.Text = $"Map Setup - X,Y,Z: {localPlayerPos.X}, {localPlayerPos.Y}, {localPlayerPos.Z}";
            }
            else if (grpMapSetup.Text != "Map Setup" && !grpMapSetup.Visible)
            {
                grpMapSetup.Text = "Map Setup";
            }

            // Prepare to draw Game Map
            var mapParams = GetMapLocation();

            var mapCanvasBounds = new SKRect() // Drawing Destination
            {
                Left = _mapCanvas.Left,
                Right = _mapCanvas.Right,
                Top = _mapCanvas.Top,
                Bottom = _mapCanvas.Bottom
            };

            // Draw Game Map
            canvas.DrawBitmap(
                _loadedBitmaps[mapParams.MapLayerIndex],
                mapParams.Bounds,
                mapCanvasBounds,
                SKPaints.PaintBitmap
            );
        }

        private void DrawActors(SKCanvas canvas, List<SKPoint> deadMarkers, List<UActor> projectileAAs)
        {
            var localPlayer = this.LocalPlayer;

            if (this.InGame && localPlayer is not null)
            {
                var allPlayers = this.AllActors?.Select(x => x.Value);

                if (allPlayers is not null)
                {
                    var activeProjectiles = allPlayers
                        .Where(a => a.ActorType == ActorType.ProjectileAA)
                        .ToList();

                    var removedProjectiles = _aaProjectileOrigins.Keys
                        .Except(activeProjectiles)
                        .ToList();

                    foreach (var projectile in removedProjectiles)
                    {
                        _aaProjectileOrigins.Remove(projectile);
                    }

                    var localPlayerPos = localPlayer.Position + AbsoluteLocation;
                    var localPlayerMapPos = localPlayerPos.ToMapPos(_selectedMap);
                    var mapParams = GetMapLocation();

                    var localPlayerZoomedPos = localPlayerMapPos.ToZoomedPos(mapParams);
                    localPlayerZoomedPos.DrawPlayerMarker(canvas, localPlayer, trkAimLength.Value);

                    foreach (var actor in allPlayers)
                    {
                        var actorPos = actor.Position + AbsoluteLocation;

                        if (Math.Abs(actorPos.X - AbsoluteLocation.X) + Math.Abs(actorPos.Y - AbsoluteLocation.Y) + Math.Abs(actorPos.Z - AbsoluteLocation.Z) < 1.0)
                            continue;

                        var actorMapPos = actorPos.ToMapPos(_selectedMap);
                        var actorZoomedPos = actorMapPos.ToZoomedPos(mapParams);

                        actor.ZoomedPosition = new Vector2()
                        {
                            X = actorZoomedPos.X,
                            Y = actorZoomedPos.Y
                        };

                        if (actor.ActorType == ActorType.Player && !actor.IsAlive)
                        {
                            if (actor.DeathPosition != Vector3.Zero)
                            {
                                var timeSinceDeath = DateTime.Now - actor.TimeOfDeath;
                                if (timeSinceDeath.TotalSeconds <= 8)
                                {
                                    var deathPosAdjusted = actor.DeathPosition + AbsoluteLocation;
                                    var deathPosMap = deathPosAdjusted.ToMapPos(_selectedMap);
                                    var deathZoomedPos = deathPosMap.ToZoomedPos(mapParams);
                                    deadMarkers.Add(deathZoomedPos.GetPoint());
                                }
                                else
                                {
                                    actor.DeathPosition = Vector3.Zero;
                                    actor.TimeOfDeath = DateTime.MinValue;
                                }
                            }
                            continue;
                        }

                        if (actor.ActorType != ActorType.ProjectileAA)
                        {
                            int aimlineLength = actor == localPlayer ? 0 : 15;
                            DrawActor(canvas, actor, actorZoomedPos, aimlineLength, localPlayerMapPos);
                        }

                        if (actor.ActorType == ActorType.ProjectileAA)
                        {
                            projectileAAs.Add(actor);
                        }
                    }
                }
            }
        }

        private Dictionary<UActor, Vector3> _aaProjectileOrigins = new Dictionary<UActor, Vector3>();

        private void DrawActor(SKCanvas canvas, UActor actor, MapPosition actorZoomedPos, int aimlineLength, MapPosition localPlayerMapPos)
        {
            if (this.InGame && this.LocalPlayer is not null)
            {
                string[] lines = null;
                var height = actorZoomedPos.Height - localPlayerMapPos.Height;

                if (actor.ActorType == ActorType.Player)
                {
                    actorZoomedPos.DrawPlayerMarker(
                        canvas,
                        actor,
                        aimlineLength
                    );

                    if (!actor.IsFriendly() && _config.ShowEnemyDistance)
                    {
                        var dist = Vector3.Distance(LocalPlayer.Position, actor.Position);
                        if (dist > 50 * 100)
                        {
                            lines = new string[1] { $"{(int)Math.Round(dist / 100)}m" };
                            actorZoomedPos.DrawActorText(canvas, actor, lines);
                        }
                    }
                }
                else if (actor.ActorType == ActorType.Projectile)
                {
                    actorZoomedPos.DrawProjectile(canvas, actor);
                }
                else if (actor.ActorType == ActorType.ProjectileAA)
                {
                    if (!_aaProjectileOrigins.ContainsKey(actor))
                        _aaProjectileOrigins[actor] = actor.Position + AbsoluteLocation;

                    actorZoomedPos.DrawProjectileAA(canvas, actor);

                    if (_aaProjectileOrigins.TryGetValue(actor, out var startPos))
                    {
                        var startPosMap = startPos.ToMapPos(_selectedMap);
                        var startZoomedPos = startPosMap.ToZoomedPos(GetMapLocation());
                        DrawAAStartMarker(canvas, startZoomedPos);
                    }
                }
                else if (actor.ActorType == ActorType.Admin)
                {
                    DrawAdmin(canvas, actor, actorZoomedPos);
                }
                else
                {
                    var vehicleTypes = new HashSet<ActorType>
                    {
                        ActorType.TruckTransport,
                        ActorType.TruckLogistics,
                        ActorType.TruckAntiAir,
                        ActorType.TruckArtillery,
                        ActorType.TruckTransportArmed,
                        ActorType.JeepTransport,
                        ActorType.JeepLogistics,
                        ActorType.JeepTurret,
                        ActorType.JeepArtillery,
                        ActorType.JeepAntitank,
                        ActorType.JeepRWSTurret,
                        ActorType.APC,
                        ActorType.IFV,
                        ActorType.TrackedAPC,
                        ActorType.TrackedIFV,
                        ActorType.TrackedJeep,
                        ActorType.Tank,
                        ActorType.TankMGS,
                        ActorType.TransportHelicopter,
                        ActorType.AttackHelicopter,
                        ActorType.Boat,
                        ActorType.BoatLogistics,
                        ActorType.Motorcycle,
                        ActorType.AntiAir,
                        ActorType.TrackedLogistics,
                        ActorType.LoachCAS,
                        ActorType.LoachScout
                    };

                    if (vehicleTypes.Contains(actor.ActorType))
                    {
                        var dist = Vector3.Distance(this.LocalPlayer.Position, actor.Position);
                        if (dist > 50 * 100)
                        {
                            lines = new string[1] { $"{(int)Math.Round(dist / 100)}m" };

                            using var textPaint = new SKPaint
                            {
                                Color = SKColors.White,
                                TextSize = 12 * _uiScale,
                                IsAntialias = true
                            };

                            actorZoomedPos.DrawActorText(canvas, actor, lines);
                        }
                    }
                    actorZoomedPos.DrawTechMarker(canvas, actor);
                }
            }
        }

        private void DrawAdmin(SKCanvas canvas, UActor admin, MapPosition position)
        {
            int adminAimlineLength = 20;

            position.DrawPlayerMarker(canvas, admin, adminAimlineLength, SKPaints.DefaultTextColor);

            string adminText = "ADMIN";
            float textSize = 12 * _uiScale;
            float textOffset = 15 * _uiScale;

            using (var textFill = new SKPaint
            {
                Color = SKPaints.DefaultTextColor,
                TextSize = textSize,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName("Arial")
            })
            using (var textOutline = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = textSize,
                StrokeWidth = 2 * _uiScale,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                Typeface = SKTypeface.FromFamilyName("Arial")
            })
            {
                SKRect textBounds = new SKRect();
                textFill.MeasureText(adminText, ref textBounds);

                float textX = position.X - (textBounds.Width / 2);
                float textY = position.Y + textOffset + (textBounds.Height / 2);

                canvas.DrawText(adminText, textX, textY, textOutline);
                canvas.DrawText(adminText, textX, textY, textFill);
            }
        }

        private void DrawTopMost(SKCanvas canvas, List<SKPoint> deadMarkers, List<UActor> projectileAAs)
        {
            foreach (var pos in deadMarkers)
            {
                DrawDead(canvas, pos, SKColors.Black, SKColors.White, 5 * _uiScale);
            }

            foreach (var projectile in projectileAAs)
            {
                var actorPos = projectile.Position + AbsoluteLocation;
                var actorMapPos = actorPos.ToMapPos(_selectedMap);
                var mapParams = GetMapLocation();
                var actorZoomedPos = actorMapPos.ToZoomedPos(mapParams);

                actorZoomedPos.DrawProjectileAA(canvas, projectile);

                if (_aaProjectileOrigins.TryGetValue(projectile, out var startPos))
                {
                    var startMapPos = startPos.ToMapPos(_selectedMap);
                    var startZoomedPos = startMapPos.ToZoomedPos(mapParams);
                    DrawAAStartMarker(canvas, startZoomedPos);
                }
            }
        }

        private void DrawAAStartMarker(SKCanvas canvas, MapPosition startPos)
        {
            float size = 8 * _uiScale;
            float thickness = 2 * _uiScale;

            using (var outlinePaint = new SKPaint
            {
                Color = SKColors.Black,
                StrokeWidth = thickness + 2 * _uiScale,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round
            })
            using (var xPaint = new SKPaint
            {
                Color = SKColors.Cyan,
                StrokeWidth = thickness,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round
            })
            {
                canvas.DrawLine(
                    startPos.X - size, startPos.Y - size,
                    startPos.X + size, startPos.Y + size,
                    outlinePaint
                );
                canvas.DrawLine(
                    startPos.X + size, startPos.Y - size,
                    startPos.X - size, startPos.Y + size,
                    outlinePaint
                );

                canvas.DrawLine(
                    startPos.X - size, startPos.Y - size,
                    startPos.X + size, startPos.Y + size,
                    xPaint
                );
                canvas.DrawLine(
                    startPos.X + size, startPos.Y - size,
                    startPos.X - size, startPos.Y + size,
                    xPaint
                );
            }

            string text = "AA";
            float textSize = 12 * _uiScale;
            float textOffset = size + 4 * _uiScale;

            using (var outlinePaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = textSize,
                IsAntialias = true,
                TextAlign = SKTextAlign.Left,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2 * _uiScale,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            })
            using (var textPaint = new SKPaint
            {
                Color = SKColors.Cyan,
                TextSize = textSize,
                IsAntialias = true,
                TextAlign = SKTextAlign.Left,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            })
            {
                SKRect textBounds = new SKRect();
                textPaint.MeasureText(text, ref textBounds);

                float textX = startPos.X + textOffset;
                float textY = startPos.Y + (textBounds.Height / 2);

                canvas.DrawText(text, textX, textY, outlinePaint);
                canvas.DrawText(text, textX, textY, textPaint);
            }
        }

        private void DrawDead(SKCanvas canvas, SKPoint position, SKColor outlineColor, SKColor fillColor, float size)
        {
            using var outlinePaint = new SKPaint
            {
                Color = outlineColor,
                StrokeWidth = 4 * _uiScale, 
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round 
            };

            using var fillPaint = new SKPaint
            {
                Color = fillColor,
                StrokeWidth = 2 * _uiScale, 
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round 
            };

            float x1 = position.X - size;
            float y1 = position.Y - size;
            float x2 = position.X + size;
            float y2 = position.Y + size;

            canvas.DrawLine(x1, y1, x2, y2, outlinePaint); 
            canvas.DrawLine(x2, y1, x1, y2, outlinePaint); 

            canvas.DrawLine(x1, y1, x2, y2, fillPaint); 
            canvas.DrawLine(x2, y1, x1, y2, fillPaint); 
        }

        private void DrawPOIs(SKCanvas canvas)
        {
            if (!IsReadyToRender()) return;

            var mapParams = GetMapLocation();
            var localPlayerPos = LocalPlayer.Position + AbsoluteLocation;

            using var crosshairPaint = new SKPaint
            {
                Color = SKColors.Red,
                StrokeWidth = 1.5f,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            foreach (var poi in _pointsOfInterest)
            {
                var poiMapPos = poi.Position.ToMapPos(_selectedMap);
                var poiZoomedPos = poiMapPos.ToZoomedPos(mapParams);

                var distance = Vector3.Distance(localPlayerPos, poi.Position);
                int distanceMeters = (int)Math.Round(distance / 100);
                double milliradians = MetersToMilliradians(distanceMeters);

                var center = poiZoomedPos.GetPoint();
                float crossSize = 8 * _uiScale;

                canvas.DrawLine(
                    center.X - crossSize, center.Y - crossSize,
                    center.X + crossSize, center.Y + crossSize,
                    crosshairPaint);

                canvas.DrawLine(
                    center.X + crossSize, center.Y - crossSize,
                    center.X - crossSize, center.Y + crossSize,
                    crosshairPaint);

                float bearing = CalculateBearing(localPlayerPos, poi.Position);
                DrawPOIText(canvas, poiZoomedPos, distance, bearing, crossSize);
            }
        }
        public double MetersToMilliradians(double meters)
        {
            // Define the data points
            double[] distances = { 50, 100, 150, 200, 250, 300, 350, 400, 450, 500, 550, 600, 650, 700, 750, 800, 850, 900, 950, 1000, 1050, 1100, 1150, 1200, 1250 };
            double[] milliradians = { 1579, 1558, 1538, 1517, 1496, 1475, 1453, 1431, 1409, 1387, 1364, 1341, 1317, 1292, 1267, 1240, 1212, 1183, 1152, 1118, 1081, 1039, 988, 918, 800 };

            // Clamp the input to the valid range
            if (meters <= 50)
                return 1579.0; // Minimum limit at 50m
            if (meters >= 1250)
                return 800.0;  // Maximum limit at 1250m

            // Find the two points to interpolate between
            for (int i = 0; i < distances.Length - 1; i++)
            {
                if (meters >= distances[i] && meters <= distances[i + 1])
                {
                    // Linear interpolation
                    double rate = (milliradians[i + 1] - milliradians[i]) / (distances[i + 1] - distances[i]);
                    double result = milliradians[i] + rate * (meters - distances[i]);
                    return Math.Round(result, 1); // Round to 1 decimal place
                }
                
            }
            return 800;
        }
        private void DrawPOIText(SKCanvas canvas, MapPosition position, float distance, float bearing, float crosshairSize)
        {
            int distanceMeters = (int)Math.Round(distance / 100);
            double milliradians = MetersToMilliradians(distanceMeters);
            bool isOutOfRange = distanceMeters < 50 || distanceMeters > 1250;

            string[] lines =
            {
                isOutOfRange ? "—" : $"{milliradians:F1}",
                $"{bearing:F1}°",
                $"{distanceMeters}m"
            };

            SKPaint textPaint = SKPaints.TextBase;
            SKPaint outlinePaint = SKPaints.TextBaseOutline;

            var basePosition = position.GetPoint(crosshairSize + 6 * _uiScale, 0);
            float verticalSpacing = 12 * _uiScale;

            foreach (var line in lines)
            {
                canvas.DrawText(line, basePosition.X, basePosition.Y, outlinePaint);
                canvas.DrawText(line, basePosition.X, basePosition.Y, textPaint);
                basePosition.Y += verticalSpacing;
            }
        }
        private float CalculateBearing(Vector3 playerPos, Vector3 poiPos)
        {
            float deltaX = poiPos.X - playerPos.X;
            float deltaY = playerPos.Y - poiPos.Y;

            float radians = (float)Math.Atan2(deltaY, deltaX);
            float degrees = radians * (180f / (float)Math.PI);
            degrees = 90f - degrees;

            if (degrees < 0) degrees += 360f;

            return degrees;
        }

        private void DrawToolTips(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            var mapParams = GetMapLocation();

            if (localPlayer is not null)
            {
                if (_closestPlayerToMouse is not null)
                {
                    var localPlayerPos = localPlayer.Position + AbsoluteLocation;
                    var hoveredPlayerPos = _closestPlayerToMouse.Position + AbsoluteLocation;
                    var distance = Vector3.Distance(localPlayerPos, hoveredPlayerPos);

                    var distanceText = $"{(int)Math.Round(distance / 100)}m";

                    var playerZoomedPos = (_closestPlayerToMouse
                        .Position + AbsoluteLocation)
                        .ToMapPos(_selectedMap)
                        .ToZoomedPos(mapParams);

                    playerZoomedPos.DrawToolTip(canvas, _closestPlayerToMouse, distanceText);
                }
            }
        }

        private void btnAddPOI_Click(object sender, EventArgs e)
        {
            var localPlayer = this.LocalPlayer;
            if (localPlayer is not null)
            {
                var position = localPlayer.Position + AbsoluteLocation;
                AddPointOfInterest(position, "POI 1");

                _mapCanvas.Invalidate();
            }
        }


        private void DrawStatusText(SKCanvas canvas)
        {
            bool isReady = this.Ready;
            bool inGame = this.InGame;
            var localPlayer = this.LocalPlayer;
            var selectedMap = this._selectedMap;

            string statusText;
            if (!isReady)
            {
                statusText = "Game Process Not Running";
            }
            else if (!inGame)
            {
                statusText = "Waiting for Game Start...";

                if (selectedMap is not null)
                {
                    this._selectedMap = null;
                }
            }
            else if (localPlayer is null)
            {
                statusText = "Cannot find LocalPlayer";
            }
            else if (selectedMap is null)
            {
                statusText = "Loading Map";
            }
            else
            {
                return; // No status text to draw
            }

            var centerX = _mapCanvas.Width / 2;
            var centerY = _mapCanvas.Height / 2;

            canvas.DrawText(statusText, centerX, centerY, SKPaints.TextRadarStatus);
        }

        private void ClearPlayerRefs()
        {
            _closestPlayerToMouse = null;
        }

        private T FindClosestObject<T>(IEnumerable<T> objects, Vector2 position, Func<T, Vector2> positionSelector, float threshold)
            where T : class
        {
            if (objects is null || !objects.Any())
                return null;

            var closestObject = objects.Aggregate(
                (x1, x2) =>
                    x2 == null || Vector2.Distance(positionSelector(x1), position)
                    < Vector2.Distance(positionSelector(x2), position)
                        ? x1
                        : x2
            );

            if (closestObject is not null && Vector2.Distance(positionSelector(closestObject), position) < threshold)
                return closestObject;

            return null;
        }

        private void PanTimer_Tick(object sender, EventArgs e)
        {
            var panDifference = new SKPoint(
                this.targetPanPosition.X - this._mapPanPosition.X,
                this.targetPanPosition.Y - this._mapPanPosition.Y
            );

            if (panDifference.Length > 0.1)
            {
                this._mapPanPosition.X += (float)(panDifference.X * PAN_SMOOTHNESS);
                this._mapPanPosition.Y += (float)(panDifference.Y * PAN_SMOOTHNESS);
            }
            else
            {
                this.panTimer.Stop();
            }
        }

        private bool ZoomIn(int step = 1)
        {
            _config.DefaultZoom = Math.Max(10, _config.DefaultZoom - step);
            _mapCanvas.Invalidate();
            return true;
        }

        private bool ZoomOut(int step = 1)
        {
            _config.DefaultZoom = Math.Min(200, _config.DefaultZoom + step);
            _mapCanvas.Invalidate();
            return true;
        }
        #endregion

        #region Event Handlers
        private void chkMapFree_CheckedChanged(object sender, EventArgs e)
        {
            if (_isFreeMapToggled)
            {
                chkMapFree.Text = "Map Follow";
                _isFreeMapToggled = false;

                lock (_renderLock)
                {
                    var localPlayer = this.LocalPlayer;
                    if (localPlayer is not null)
                    {
                        var localPlayerMapPos = (localPlayer.Position + AbsoluteLocation).ToMapPos(_selectedMap);
                        _mapPanPosition = new MapPosition()
                        {
                            X = localPlayerMapPos.X,
                            Y = localPlayerMapPos.Y,
                            Height = localPlayerMapPos.Height
                        };
                    }
                }
            }
            else
            {
                chkMapFree.Text = "Map Free";
                _isFreeMapToggled = true;
            }
        }

        private void btnApplyMapScale_Click(object sender, EventArgs e)
        {
            if (float.TryParse(txtMapSetupX.Text, out float x)
                && float.TryParse(txtMapSetupY.Text, out float y)
                && float.TryParse(txtMapSetupScale.Text, out float scale))
            {
                lock (_renderLock)
                {
                    if (_selectedMap is not null)
                    {
                        _selectedMap.ConfigFile.X = x;
                        _selectedMap.ConfigFile.Y = y;
                        _selectedMap.ConfigFile.Scale = scale;
                        _selectedMap.ConfigFile.Save(_selectedMap);
                    }
                }
            }
            else
            {
                ShowErrorDialog("Invalid value(s) provided in the map setup textboxes.");
            }
        }

        private void HandleMapClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && InGame)
            {
                var mapParams = GetMapLocation();
                var mouseX = (e.X / mapParams.XScale) + mapParams.Bounds.Left;
                var mouseY = (e.Y / mapParams.YScale) + mapParams.Bounds.Top;

                var worldX = (mouseX - _selectedMap.ConfigFile.X) / _selectedMap.ConfigFile.Scale;
                var worldY = (mouseY - _selectedMap.ConfigFile.Y) / _selectedMap.ConfigFile.Scale;
                var worldPos = new Vector3(worldX, worldY, LocalPlayer.Position.Z);

                _pointsOfInterest.Add(new PointOfInterest(worldPos, "POI"));
                _mapCanvas.Invalidate();
            }
            else if (e.Button == MouseButtons.Right && _hoveredPoi != null)
            {
                _pointsOfInterest.Remove(_hoveredPoi);
                _mapCanvas.Invalidate();
            }
        }

        public class PointOfInterest
        {
            public Vector3 Position { get; }
            public string Name { get; }

            public PointOfInterest(Vector3 position, string name)
            {
                Position = position;
                Name = name;
            }
        }

        private void skMapCanvas_MouseMove(object sender, MouseEventArgs e)
        {

            // Handle player hover and dragging
            if (this.InGame && this.LocalPlayer is not null)
            {
                var mouse = new Vector2(e.X, e.Y);

                // Find the closest player to the mouse cursor
                var players = this.AllActors?.Select(x => x.Value);
                _closestPlayerToMouse = FindClosestObject(players, mouse, x => x.ZoomedPosition, 12 * _uiScale);
            }
            else if (this.InGame)
            {
                ClearPlayerRefs();
            }

            // Handle map dragging
            if (this._isDragging && this._isFreeMapToggled)
            {
                if (!this._lastMousePosition.IsEmpty)
                {
                    int dx = e.X - this._lastMousePosition.X;
                    int dy = e.Y - this._lastMousePosition.Y;

                    dx = (int)(dx * DRAG_SENSITIVITY);
                    dy = (int)(dy * DRAG_SENSITIVITY);

                    this.targetPanPosition.X -= dx;
                    this.targetPanPosition.Y -= dy;

                    if (!this.panTimer.Enabled)
                        this.panTimer.Start();
                }

                this._lastMousePosition = e.Location;
            }

            // Handle POI hover
            _hoveredPoi = null;
            if (InGame && _pointsOfInterest.Count > 0)
            {
                var mapParams = GetMapLocation();
                var mousePos = new SKPoint(e.X, e.Y);

                foreach (var poi in _pointsOfInterest)
                {
                    var poiPos = poi.Position.ToMapPos(_selectedMap).ToZoomedPos(mapParams).GetPoint();
                    if (SKPoint.Distance(mousePos, poiPos) < 20 * _uiScale) // 20px hover threshold
                    {
                        _hoveredPoi = poi;
                        break;
                    }
                }
            }

            _mapCanvas.Invalidate();
        }

        private void skMapCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && this._isFreeMapToggled)
            {
                this._isDragging = true;
                this._lastMousePosition = e.Location;
            }

            // Remove POI on right-click (hover-based removal)
            if (e.Button == MouseButtons.Right && _hoveredPoi != null)
            {
                _pointsOfInterest.Remove(_hoveredPoi);
                _hoveredPoi = null;
                _mapCanvas.Invalidate();
            }
        }

        private void skMapCanvas_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && this.InGame && this.LocalPlayer is not null)
            {
                var mapParams = GetMapLocation();
                var mouseX = e.X / mapParams.XScale + mapParams.Bounds.Left;
                var mouseY = e.Y / mapParams.YScale + mapParams.Bounds.Top;

                var worldX = (mouseX - _selectedMap.ConfigFile.X) / _selectedMap.ConfigFile.Scale;
                var worldY = (mouseY - _selectedMap.ConfigFile.Y) / _selectedMap.ConfigFile.Scale;
                var worldZ = this.LocalPlayer.Position.Z;

                var poiPosition = new Vector3(worldX, worldY, worldZ);

                AddPointOfInterest(poiPosition, "POI");

                _mapCanvas.Invalidate();
            }
        }

        private void skMapCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (this._isDragging)
            {
                this._isDragging = false;
                this._lastMousePosition = e.Location;
            }
        }

        private void skMapCanvas_PaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
        {
            try
            {
                SKCanvas canvas = e.Surface.Canvas;
                canvas.Clear();

                UpdateWindowTitle();

                if (IsReadyToRender())
                {
                    lock (_renderLock)
                    {
                        List<SKPoint> deadMarkers = new();
                        List<UActor> projectileAAs = new();

                        DrawMap(canvas);
                        DrawActors(canvas, deadMarkers, projectileAAs); 
                        DrawPOIs(canvas);
                        DrawToolTips(canvas);
                        DrawTopMost(canvas, deadMarkers, projectileAAs);
                    }
                }
                else
                {
                    DrawStatusText(canvas);
                }

                canvas.Flush();
            }
            catch { }
        }

        private void ticketsPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            string ticketText = $"Friendly: {_lastFriendlyTickets}  |  Enemy: {_lastEnemyTickets}";

            using (var font = new Font("Arial", 9f, FontStyle.Bold))
            {
                SizeF textSize = TextRenderer.MeasureText(g, ticketText, font);

                int x = (ticketsPanel.Width - (int)textSize.Width) / 2;
                int y = (ticketsPanel.Height - (int)textSize.Height) / 2;

                x += 1; 

                TextRenderer.DrawText(
                    g,
                    ticketText,
                    font,
                    new Point(x, y),
                    Color.FromArgb(240, 240, 240)
                );
            }
        }
        private void btnToggleMap_Click(object sender, EventArgs e)
        {
            ToggleMap();
        }
        #endregion
        #endregion

        #region Settings
        #region General
        #region Event Handlers
        private void chkShowMapSetup_CheckedChanged(object sender, EventArgs e)
        {
            if (chkShowMapSetup.Checked)
            {
                grpMapSetup.Visible = true;
                txtMapSetupX.Text = _selectedMap?.ConfigFile.X.ToString() ?? "0";
                txtMapSetupY.Text = _selectedMap?.ConfigFile.Y.ToString() ?? "0";
                txtMapSetupScale.Text = _selectedMap?.ConfigFile.Scale.ToString() ?? "0";
            }
            else
                grpMapSetup.Visible = false;
        }

        private void btnRestartRadar_Click(object sender, EventArgs e)
        {
            Memory.Restart();
        }

        private bool DumpNames()
        {
            _game.LogVehicles(force: true);
            return true;
        }

        private void btnDumpNames_Click(object sender, EventArgs e)
        {
            DumpNames();
        }

        private void ChkShowEnemyDistance_CheckedChanged(object sender, EventArgs e)
        {
            _config.ShowEnemyDistance = chkShowEnemyDistance.Checked;
            _mapCanvas.Invalidate();
        }

        private void trkUIScale_Scroll(object sender, EventArgs e)
        {
            _config.UIScale = trkUIScale.Value;
            _uiScale = (.01f * trkUIScale.Value);

            InitiateUIScaling();
        }
        #endregion
        #endregion
        #endregion
        #endregion

        private void grpMapSetup_Enter(object sender, EventArgs e)
        {

        }

        private void trkAimLength_Scroll(object sender, EventArgs e)
        {

        }

        #region ESP Event Handlers
        private void ChkEnableEsp_CheckedChanged(object sender, EventArgs e)
        {
            _config.EnableEsp = chkEnableEsp.Checked;
            Config.SaveConfig(_config);

            if (_config.EnableEsp)
            {
                if (_espOverlay == null || _espOverlay.IsDisposed)
                {
                    _espOverlay = new EspOverlay();
                    _espOverlay.Show();
                }
            }
            else
            {
                if (_espOverlay != null && !_espOverlay.IsDisposed)
                {
                    _espOverlay.Close();
                    _espOverlay = null;
                }
            }
        }

        private void TrkEspMaxDistance_Scroll(object sender, EventArgs e)
        {
            _config.EspMaxDistance = trkEspMaxDistance.Value;
            lblEspMaxDistance.Text = $"Max Distance: {trkEspMaxDistance.Value}m";
            Config.SaveConfig(_config);
        }

        private void ChkShowAllies_CheckedChanged(object sender, EventArgs e)
        {
            _config.EspShowAllies = chkShowAllies.Checked;
            Config.SaveConfig(_config);
        }

        private void ChkEspShowNames_CheckedChanged(object sender, EventArgs e)
        {
            _config.ShowNames = chkEspShowNames.Checked;
            Config.SaveConfig(_config);
        }

        private void ChkEspShowDistance_CheckedChanged(object sender, EventArgs e)
        {
            _config.EspShowDistance = chkEspShowDistance.Checked;
            Config.SaveConfig(_config);
        }

        private void ChkEspShowHealth_CheckedChanged(object sender, EventArgs e)
        {
            _config.EspShowHealth = chkEspShowHealth.Checked;
            Config.SaveConfig(_config);
        }

        private void TxtEspFontSize_TextChanged(object sender, EventArgs e)
        {
            if (float.TryParse(txtEspFontSize.Text, out float fontSize) && fontSize > 0)
            {
                _config.ESPFontSize = fontSize;
                Config.SaveConfig(_config);
            }
            else
            {
                txtEspFontSize.Text = _config.ESPFontSize.ToString();
            }
        }
        private void TxtEspColorA_TextChanged(object sender, EventArgs e)
        {
            if (byte.TryParse(txtEspColorA.Text, out byte a) && a >= 0 && a <= 255)
            {
                
                var color = _config.EspTextColor;
                color.A = a;
                _config.EspTextColor = color;
                Config.SaveConfig(_config);
            }
            else
            {
                txtEspColorA.Text = _config.EspTextColor.A.ToString();
            }
        }

        private void TxtEspColorR_TextChanged(object sender, EventArgs e)
        {
            if (byte.TryParse(txtEspColorR.Text, out byte r) && r >= 0 && r <= 255)
            {
                var color = _config.EspTextColor;
                color.R = r;
                _config.EspTextColor = color;
                Config.SaveConfig(_config);
            }
            else
            {
                txtEspColorR.Text = _config.EspTextColor.R.ToString();
            }
        }

        private void TxtEspColorG_TextChanged(object sender, EventArgs e)
        {
            if (byte.TryParse(txtEspColorG.Text, out byte g) && g >= 0 && g <= 255)
            {
                var color = _config.EspTextColor;
                color.G = g;
                _config.EspTextColor = color;
                Config.SaveConfig(_config);
            }
            else
            {
                txtEspColorG.Text = _config.EspTextColor.G.ToString();
            }
        }

        private void TxtEspColorB_TextChanged(object sender, EventArgs e)
        {
            if (byte.TryParse(txtEspColorB.Text, out byte b) && b >= 0 && b <= 255)
            {
                var color = _config.EspTextColor;
                color.B = b;
                _config.EspTextColor = color;
                Config.SaveConfig(_config);
            }
            else
            {
                txtEspColorB.Text = _config.EspTextColor.B.ToString();
            }
        }
        #endregion
    }
}