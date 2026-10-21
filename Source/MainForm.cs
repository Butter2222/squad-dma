using DarkModeForms;
using MaterialSkin.Controls;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using squad_dma.Source.Misc;
using squad_dma.Source.Squad;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Numerics;

namespace squad_dma
{
    public partial class MainForm : Form
    {
        #region Fields
        private readonly Config _config;
        private SKGLControl _mapCanvas;
        private readonly Stopwatch _housekeepingWatch = new();
        private readonly object _renderLock = new();
        private readonly object _loadMapBitmapsLock = new();
        private readonly System.Timers.Timer _mapChangeTimer = new(100);
        private readonly List<Map> _maps = new();
        private DarkModeCS _darkmode;
        private readonly Dictionary<UActor, Vector3D> _aaProjectileOrigins = new();
        private readonly List<PointOfInterest> _pointsOfInterest = new();

        // Render-layer state. Everything below is owned and reused by the paint callback; none of it is
        // allocated per frame.
        private readonly RadarBatchRenderer _renderer = new();
        private readonly ActorFrameSnapshot _frame = new();
        private readonly List<SKPoint> _deadMarkerBuffer = new();
        private readonly List<ActorFrameData> _projectileAABuffer = new();
        private readonly List<PoiTrajectoryCacheEntry> _poiTrajectoryCache = new();
        private string _lastMapNameChecked;
        private Map _lastSelectedMapChecked;
        private string _rocketBarrageWeaponName;
        private bool _isRocketBarrageWeapon;
        private (double X, double Y, double Z) _lastMapSetupPos;
        private bool _mapSetupTextIsLive;

        private sealed class PoiTrajectoryCacheEntry
        {
            public object WeaponData;
            public float ShooterX, ShooterY, TargetX, TargetY;
            public TerrainHeightmap Terrain;
            public BallisticSolution Solution;
        }

        /// <summary>Snapshot built at the start of the current frame; read-only for widgets drawn in the same pass.</summary>
        internal ActorFrameSnapshot FrameSnapshot => _frame;
        private GameStatus _previousGameStatus = GameStatus.NotFound;
        private EspOverlay _espOverlay;
        private AimviewWidget _aimviewWidget;
        private TInfoWidget _tInfoWidget;
        private TicketsWidget _ticketsWidget;
        private DiagnosticsWidget _diagnostics;

        private bool _isFreeMapToggled;
        private bool _isDragging;
        private float _uiScale = 1.0f;
        private int _mapSelectionIndex;
        private int _lastFriendlyTickets;
        private int _lastEnemyTickets;
        private int _lastKills;
        private int _lastWoundeds;


        private Map _selectedMap;
        private MapImage[] _loadedBitmaps;
        private MapPosition _mapPanPosition = new();
        private Point _lastMousePosition;
        private PointOfInterest _hoveredPoi;
        private UActor _closestPlayerToMouse;

        private const float VELOCITY_DECAY = 0.92f;
        private const float MAX_VELOCITY = 50.0f;

        private bool _isWaitingForKey = false;
        private Button _currentKeybindButton = null;
        private Keys _currentKeybind = Keys.None;
        private bool _isHolding_QuickZoom = false;

        private SKPoint _lastPanPosition;
        private DateTime _lastPanUpdate;
        private float _currentPanSpeed = 0f;

        private Vector2 _velocity = Vector2.Zero;
        private Vector2 _lastMouseDelta = Vector2.Zero;
        private DateTime _lastUpdateTime;
        private bool _isPanning = false;
        #endregion

        #region Properties
        private bool Ready => Memory.Ready;
        private bool InGame => Memory.InGame;
        private string MapName => Memory.MapName;
        private UActor LocalPlayer => Memory.LocalPlayer;
        private ReadOnlyDictionary<ulong, UActor> AllActors => Memory.Actors;
        private Vector3D AbsoluteLocation => Memory.AbsoluteLocation;
        #endregion

        #region Getters
        public void AddPointOfInterest(Vector3D position, string name)
        {
            _pointsOfInterest.Add(new PointOfInterest(position, name));
            PublishFireSupportTarget();
        }

        public void RemovePointOfInterest(string name)
        {
            var poi = _pointsOfInterest.FirstOrDefault(p => p.Name == name);
            if (poi != null)
            {
                _pointsOfInterest.Remove(poi);
                PublishFireSupportTarget();
            }
        }

        public void ClearAllPointsOfInterest()
        {
            _pointsOfInterest.Clear();
            PublishFireSupportTarget();
            _mapCanvas.Invalidate();
        }

        public List<PointOfInterest> GetPointsOfInterest()
        {
            return _pointsOfInterest;
        }

        public double GetTechMortarDegrees(int meters)
        {
            return MortarCalculator.MetersToTechMortarDegrees(meters);
        }

        public double GetMortarMilliradians(int meters)
        {
            return MortarCalculator.MetersToMortarMilliradians(meters);
        }

        public UActor GetLocalPlayer()
        {
            return LocalPlayer;
        }

        public Vector3D GetAbsoluteLocation()
        {
            return AbsoluteLocation;
        }

        public void ClearPointsOfInterest()
        {
            _pointsOfInterest.Clear();
            PublishFireSupportTarget();
            _mapCanvas.Invalidate();
        }

        #endregion

        #region Constructor
        public MainForm()
        {
            _config = Program.Config;
            InitializeComponent();
            if (_config.EnableEsp)
            {
                _espOverlay = new EspOverlay();
                _espOverlay.Show();
            }

            LoadConfig();
            InitializeDarkMode();
            InitializeFormSettings();
            InitializeMapCanvas();
            InitializeTimers();
            InitializeEventHandlers();
            LoadInitialData();
            InitializeKeybinds();
            InitializeColorsTab();
        }

        private void InitializeDarkMode()
        {
            _darkmode = new DarkModeCS(this);            
        }

        private void InitializeFormSettings()
        {
            Size = new Size(1280, 720);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Normal;
            DoubleBuffered = true;
        }

        private void InitializeMapCanvas()
        {
            _mapCanvas = new SKGLControl
            {
                Size = new Size(50, 50),
                Dock = DockStyle.Fill,
                VSync = false
            };
            tabRadar.Controls.Add(_mapCanvas);
            chkMapFree.Parent = _mapCanvas;
            StyleOverlayControls();

            // Initialize Aimview Panel if enabled
            if (_config.EnableAimview)
            {
                InitializeAimviewWidget();
            }
            
            // Initialize TInfo Widget
            InitializeTInfoWidget();
        }
        
        private void InitializeAimviewWidget()
        {
            var savedX = Math.Max(0, Program.Config.AimviewPanelX);
            var savedY = Math.Max(0, Program.Config.AimviewPanelY);
            var savedWidth = Math.Max(480, Math.Min(Program.Config.AimviewPanelWidth, 1280));
            var savedHeight = Math.Max(270, Math.Min(Program.Config.AimviewPanelHeight, 720));

            var location = new SKRect(savedX, savedY, savedX + savedWidth, savedY + savedHeight);

            _aimviewWidget = new AimviewWidget(_mapCanvas, this, location, false, _uiScale);
            
            // Subscribe to widget change events to save position/size when user finishes dragging/resizing
            _aimviewWidget.WidgetChanged += OnAimviewWidgetChanged;
        }

        private void OnAimviewWidgetChanged(object sender, WidgetChangedEventArgs e)
        {
            // Save the new position and size to config
            _config.AimviewPanelX = (int)e.Location.X;
            _config.AimviewPanelY = (int)e.Location.Y;
            _config.AimviewPanelWidth = (int)e.Size.Width;
            _config.AimviewPanelHeight = (int)e.Size.Height;
            Config.SaveConfig(_config);
        }

        private void SaveAimviewWidgetState()
        {
            if (_aimviewWidget != null)
            {
                int newX = (int)_aimviewWidget.Location.X;
                int newY = (int)_aimviewWidget.Location.Y;
                int newWidth = (int)_aimviewWidget.Size.Width;
                int newHeight = (int)_aimviewWidget.Size.Height;

                // Only save if there are actual changes
                if (_config.AimviewPanelX != newX || _config.AimviewPanelY != newY ||
                    _config.AimviewPanelWidth != newWidth || _config.AimviewPanelHeight != newHeight)
                {
                    _config.AimviewPanelX = newX;
                    _config.AimviewPanelY = newY;
                    _config.AimviewPanelWidth = newWidth;
                    _config.AimviewPanelHeight = newHeight;
                    Config.SaveConfig(_config);
                }
            }
        }
        
        /// <summary>Current numbers for the ticket counter widget.</summary>
        internal (int Friendly, int Enemy, int Kills, int Woundeds) TicketCounts
            => (_lastFriendlyTickets, _lastEnemyTickets, _lastKills, _lastWoundeds);

        private void InitializeTInfoWidget()
        {
            _tInfoWidget = new TInfoWidget(_mapCanvas, this, _uiScale);
            _ticketsWidget = new TicketsWidget(_mapCanvas, this, _uiScale);
            _diagnostics = new DiagnosticsWidget(_mapCanvas, _uiScale);
        }

        private void InitializeTimers()
        {
            _mapChangeTimer.AutoReset = false;
            _mapChangeTimer.Elapsed += MapChangeTimer_Elapsed;

            var inputTimer = new System.Windows.Forms.Timer { Interval = 10 };
            inputTimer.Tick += InputUpdate_Tick;
            inputTimer.Start();

            var ticketUpdateTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            ticketUpdateTimer.Tick += (s, e) => UpdateTicketsDisplay();
            ticketUpdateTimer.Start();



            var stateMonitor = new System.Windows.Forms.Timer { Interval = 500 };
            stateMonitor.Tick += (s, e) => HandleGameStateChange();
            stateMonitor.Start();
        }

        private void InitializeEventHandlers()
        {
            Shown += frmMain_Shown;
            _mapCanvas.PaintSurface += skMapCanvas_PaintSurface;
            _mapCanvas.MouseMove += skMapCanvas_MouseMove;
            _mapCanvas.MouseDown += skMapCanvas_MouseDown;
            _mapCanvas.MouseDoubleClick += skMapCanvas_MouseDoubleClick;
            _mapCanvas.MouseUp += skMapCanvas_MouseUp;

            chkDisableSuppression.CheckedChanged += ChkDisableSuppression_CheckedChanged;
            chkSetInteractionDistances.CheckedChanged += ChkSetInteractionDistances_CheckedChanged;
            chkAllowShootingInMainBase.CheckedChanged += ChkAllowShootingInMainBase_CheckedChanged;
            chkSpeedHack.CheckedChanged += ChkSetTimeDilation_CheckedChanged;
            chkAirStuck.CheckedChanged += ChkAirStuck_CheckedChanged;
            chkDisableCollision.CheckedChanged += ChkDisableCollision_CheckedChanged;
            chkQuickZoom.CheckedChanged += ChkQuickZoom_CheckedChanged;
            chkRapidFire.CheckedChanged += ChkRapidFire_CheckedChanged;
            chkShowEnemyDistance.CheckedChanged += ChkShowEnemyDistance_CheckedChanged;
            chkEnableAimview.CheckedChanged += ChkEnableAimview_CheckedChanged;
            chkInfiniteAmmo.CheckedChanged += ChkInfiniteAmmo_CheckedChanged;
            chkQuickSwap.CheckedChanged += ChkQuickSwap_CheckedChanged;
            chkForceFullAuto.CheckedChanged += ChkForceFullAuto_CheckedChanged;

            // ESP Event Handlers
            chkEnableEsp.CheckedChanged += ChkEnableEsp_CheckedChanged;
            chkEnableBones.CheckedChanged += ChkEnableBones_CheckedChanged;
            trkEspMaxDistance.Scroll += TrkEspMaxDistance_Scroll;
            trkEspVehicleMaxDistance.Scroll += TrkEspVehicleMaxDistance_Scroll;
            chkEspShowVehicles.CheckedChanged += ChkEspShowVehicles_CheckedChanged;
            chkShowAllies.CheckedChanged += ChkShowAllies_CheckedChanged;
            chkEspShowNames.CheckedChanged += ChkEspShowNames_CheckedChanged;
            chkEspShowDistance.CheckedChanged += ChkEspShowDistance_CheckedChanged;
            chkEspShowHealth.CheckedChanged += ChkEspShowHealth_CheckedChanged;
            txtEspFontSize.TextChanged += TxtEspFontSize_TextChanged;
        }

        private void LoadInitialData()
        {
            LoadMaps();
            _housekeepingWatch.Start();
        }

        private void InitializeColorsTab()
        {
            // Create and add the color configuration panel to the Colors tab
            var colorPanel = CreateColorConfigurationPanel();
            tabColors.Controls.Add(colorPanel);

            // The dark mode theming recolors the panels when they are added to the tab, which wipes the preview
            // swatches to its grey. Paint the real colors again right after, and every time the tab is opened.
            RefreshColorPreviews();
            tabControl.SelectedIndexChanged += (s, e) =>
            {
                if (tabControl.SelectedTab == tabColors)
                    RefreshColorPreviews();
            };
        }
        #endregion

        #region Overrides
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Enabled = false;

            // Put the soldier back first: the game keeps running after the radar closes and must not be left frozen
            try { Memory._game?.ShutdownRenderTweaks(); }
            catch (Exception ex) { Logger.Error($"[RenderTweaks] Could not restore the render settings while closing: {ex.Message}"); }
            try { Memory._game?.SetFreeCamera(false); }
            catch (Exception ex) { Logger.Error($"[FreeCamera] Could not switch it off while closing: {ex.Message}"); }

            Program.Log("Closing form");
            if (_espOverlay != null && !_espOverlay.IsDisposed)
            {
                _espOverlay.Close();
                _espOverlay = null;
            }

            // Save aimview widget position and size before disposing
            SaveAimviewWidgetState();

            if (_aimviewWidget != null)
            {
                _aimviewWidget.WidgetChanged -= OnAimviewWidgetChanged;
                _aimviewWidget.Dispose();
                _aimviewWidget = null;
            }
            
            _tInfoWidget?.Dispose();
            _tInfoWidget = null;

            _ticketsWidget?.Dispose();
            _ticketsWidget = null;
            _diagnostics?.Dispose();
            _diagnostics = null;

            CleanupLoadedBitmaps();
            Config.ClearCache();
            Memory.Shutdown();
            e.Cancel = false;
            base.OnFormClosing(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == _config.KeybindSpeedHack && chkSpeedHack.Checked)
            {
                _config.SetSpeedHack = !_config.SetSpeedHack;
                Memory._game?.SetSpeedHack(_config.SetSpeedHack);
                UpdateStatusIndicator(lblStatusSpeedHack, _config.SetSpeedHack);
                return true;
            }
            else if (keyData == _config.KeybindAirStuck && chkAirStuck.Checked)
            {
                // Toggle AirStuck state
                _config.SetAirStuck = !_config.SetAirStuck;
                Memory._game?.SetAirStuck(_config.SetAirStuck);
                UpdateStatusIndicator(lblStatusAirStuck, _config.SetAirStuck);
                
                // If NoCollision is also checked, toggle it together with AirStuck
                if (chkDisableCollision.Checked)
                {
                    _config.DisableCollision = _config.SetAirStuck;
                    Memory._game?.DisableCollision(_config.DisableCollision);
                }
                
                Config.SaveConfig(_config);
                return true;
            }
            else if (keyData == _config.KeybindFreeCamera && chkFreeCamera.Checked)
            {
                ToggleFreeCamera();
                return true;
            }
            else if (keyData == _config.KeybindToggleEnemyDistance)
            {
                ToggleEnemyDistance();
                return true;
            }
            else if (keyData == _config.KeybindToggleMap)
            {
                ToggleMap();
                return true;
            }
            else if (keyData == _config.KeybindToggleFullscreen)
            {
                ToggleFullscreen(FormBorderStyle is FormBorderStyle.Sizable);
                return true;
            }
            else if (keyData == _config.KeybindDumpNames)
            {
                DumpNames();
                return true;
            }

            if (_isWaitingForKey)
            {
                if (keyData == Keys.Escape)
                {
                    EndKeybindCapture(Keys.None);
                    return true;
                }

                if (keyData != Keys.None)
                {
                    EndKeybindCapture(keyData);
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool ToggleEnemyDistance()
        {
            _config.ShowEnemyDistance = !_config.ShowEnemyDistance;
            chkShowEnemyDistance.Checked = _config.ShowEnemyDistance;
            _mapCanvas.Invalidate();
            Config.SaveConfig(_config);
            UpdateStatusIndicator(lblStatusToggleEnemyDistance, _config.ShowEnemyDistance);
            return true;
        }

        private void UpdateStatusIndicator(Label statusLabel, bool isEnabled)
        {
            if (statusLabel.InvokeRequired)
            {
                statusLabel.Invoke(new Action(() => UpdateStatusIndicator(statusLabel, isEnabled)));
                return;
            }

            // Only update status for specified keybinds
            if (statusLabel == lblStatusSpeedHack ||
                statusLabel == lblStatusAirStuck ||
                statusLabel == lblStatusToggleEnemyDistance)
            {
                statusLabel.Text = isEnabled ? "ON" : "OFF";
            }
        }

        private void InitializeKeybinds()
        {

            // Keybind buttons
            btnKeybindSpeedHack.Text = _config.KeybindSpeedHack == Keys.None ? "None" : _config.KeybindSpeedHack.ToString();
            btnKeybindAirStuck.Text = _config.KeybindAirStuck == Keys.None ? "None" : _config.KeybindAirStuck.ToString();
            btnKeybindFreeCamera.Text = _config.KeybindFreeCamera == Keys.None ? "None" : _config.KeybindFreeCamera.ToString();
            btnKeybindQuickZoom.Text = _config.KeybindQuickZoom == Keys.None ? "None" : _config.KeybindQuickZoom.ToString();
            btnKeybindToggleEnemyDistance.Text = _config.KeybindToggleEnemyDistance == Keys.None ? "None" : _config.KeybindToggleEnemyDistance.ToString();
            btnKeybindToggleMap.Text = _config.KeybindToggleMap == Keys.None ? "None" : _config.KeybindToggleMap.ToString();
            btnKeybindToggleFullscreen.Text = _config.KeybindToggleFullscreen == Keys.None ? "None" : _config.KeybindToggleFullscreen.ToString();
            btnKeybindDumpNames.Text = _config.KeybindDumpNames == Keys.None ? "None" : _config.KeybindDumpNames.ToString();
            btnKeybindZoomIn.Text = _config.KeybindZoomIn == Keys.None ? "None" : _config.KeybindZoomIn.ToString();
            btnKeybindZoomOut.Text = _config.KeybindZoomOut == Keys.None ? "None" : _config.KeybindZoomOut.ToString();

            UpdateStatusIndicator(lblStatusSpeedHack, _config.SetSpeedHack);
            UpdateStatusIndicator(lblStatusAirStuck, _config.SetAirStuck);
            UpdateStatusIndicator(lblStatusToggleEnemyDistance, _config.ShowEnemyDistance);
        }

        private void InputUpdate_Tick(object sender, EventArgs e)
        {
            if (_isWaitingForKey)
                return;

            HandleKeyboardInput();
        }

        private void HandleKeyboardInput()
        {
            // Hold-to-activate features
            if (_config.KeybindQuickZoom != Keys.None && InputManager.IsKeyDown((int)_config.KeybindQuickZoom) && chkQuickZoom.Checked)
            {
                if (!_isHolding_QuickZoom)
                {
                    Memory._game?.SetQuickZoom(true);
                    _isHolding_QuickZoom = true;
                }
            }
            else if (_isHolding_QuickZoom)
            {
                Memory._game?.SetQuickZoom(false);
                _isHolding_QuickZoom = false;
            }

            // Handle zoom controls
            if (InputManager.IsKeyDown((int)_config.KeybindZoomIn))
                ZoomIn(_config.ZoomStep);
            else if (InputManager.IsKeyDown((int)_config.KeybindZoomOut))
                ZoomOut(_config.ZoomStep);

            // Handle feature toggles with keybinds
            if (InputManager.IsKeyPressed((int)_config.KeybindSpeedHack) && chkSpeedHack.Checked)
            {
                _config.SetSpeedHack = !_config.SetSpeedHack;
                Memory._game?.SetSpeedHack(_config.SetSpeedHack);
                Config.SaveConfig(_config);
                UpdateStatusIndicator(lblStatusSpeedHack, _config.SetSpeedHack);
            }
            if (_config.KeybindFreeCamera != Keys.None && chkFreeCamera.Checked && InputManager.IsKeyPressed((int)_config.KeybindFreeCamera))
                ToggleFreeCamera();

            if (InputManager.IsKeyPressed((int)_config.KeybindAirStuck) && chkAirStuck.Checked)
            {
                _config.SetAirStuck = !_config.SetAirStuck;
                Memory._game?.SetAirStuck(_config.SetAirStuck);
                UpdateStatusIndicator(lblStatusAirStuck, _config.SetAirStuck);
                
                if (!_config.SetAirStuck && chkDisableCollision.Checked)
                {
                    _config.DisableCollision = false;
                    Memory._game?.DisableCollision(false);
                }
                else if (_config.SetAirStuck && chkDisableCollision.Checked)
                {
                    _config.DisableCollision = true;
                    Memory._game?.DisableCollision(true);
                }
                
                Config.SaveConfig(_config);
            }

            // Handle other keybinds
            if (InputManager.IsKeyPressed((int)_config.KeybindToggleEnemyDistance))
            {
                ToggleEnemyDistance();
            }
            if (InputManager.IsKeyPressed((int)_config.KeybindToggleMap))
                ToggleMap();
            if (InputManager.IsKeyPressed((int)_config.KeybindToggleFullscreen))
                ToggleFullscreen(FormBorderStyle is FormBorderStyle.Sizable);
            if (InputManager.IsKeyPressed((int)_config.KeybindDumpNames))
                DumpNames();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (tabControl.SelectedIndex == 0)
            {
                HandleMapZoom(e);
                return;
            }
            base.OnMouseWheel(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // Aimview panel positioning is handled by dragging
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            // Aimview panel positioning is handled by dragging
        }

        private void HandleMapZoom(MouseEventArgs e)
        {
            const int zoomStep = 3;

            // On the free map the point under the cursor stays under the cursor while zooming
            var before = _isFreeMapToggled ? GetMapLocation() : null;
            float oldZoom = _config.DefaultZoom;

            if (e.Delta < 0)
                ZoomOut(zoomStep);
            else if (e.Delta > 0)
                ZoomIn(zoomStep);

            if (before == null || oldZoom == _config.DefaultZoom || _mapCanvas.Width <= 0 || _mapCanvas.Height <= 0)
                return;

            // The visible area scales linearly with the zoom setting
            double ratio = _config.DefaultZoom / (double)oldZoom;
            var mouse = _mapCanvas.PointToClient(Cursor.Position);
            double u = (double)mouse.X / _mapCanvas.Width, v = (double)mouse.Y / _mapCanvas.Height;
            double mapX = before.Bounds.Left + mouse.X / before.XScale;
            double mapY = before.Bounds.Top + mouse.Y / before.YScale;

            _mapPanPosition.X = mapX + (0.5 - u) * before.Bounds.Width * ratio;
            _mapPanPosition.Y = mapY + (0.5 - v) * before.Bounds.Height * ratio;
            ClampPanPosition();
            _mapCanvas.Invalidate();
        }

        private void skMapCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (InGame && LocalPlayer is not null)
            {
                HandlePlayerHover(e);
            }
            else
            {
                ClearPlayerRefs();
            }

            if (_isDragging && _isFreeMapToggled)
            {
                HandleMapDragging(e);
            }

            HandlePOIHover(e);
            _mapCanvas.Invalidate();
        }

        private void HandlePlayerHover(MouseEventArgs e)
        {
            var mouse = new Vector2D(e.X, e.Y);
            var players = AllActors?.Select(x => x.Value);
            _closestPlayerToMouse = FindClosestObject(players, mouse, x => x.ZoomedPosition, 12 * _uiScale);
        }

        /// <summary>
        /// The map follows the cursor 1:1: a screen pixel is XScale/YScale pixels of the map image, so the pan
        /// moves by the mouse delta divided by those scales, at any zoom level and window size.
        /// </summary>
        private void HandleMapDragging(MouseEventArgs e)
        {
            if (!_lastMousePosition.IsEmpty)
            {
                var mapParams = GetMapLocation();
                if (mapParams != null && mapParams.XScale > 0 && mapParams.YScale > 0)
                {
                    _mapPanPosition.X -= (e.X - _lastMousePosition.X) / mapParams.XScale;
                    _mapPanPosition.Y -= (e.Y - _lastMousePosition.Y) / mapParams.YScale;
                    ClampPanPosition();
                }
            }

            _lastMousePosition = e.Location;
        }

        /// <summary>
        /// Keeps the pan centre where the view can still follow it. Past the edge of the map the view stops
        /// moving, so without this the pan would keep building up and the map would feel stuck, then jump.
        /// </summary>
        private void ClampPanPosition()
        {
            if (_loadedBitmaps == null || _loadedBitmaps.Length == 0)
                return;

            var bitmap = _loadedBitmaps[Math.Clamp(GetMapLayerIndex(_mapPanPosition.Height), 0, _loadedBitmaps.Length - 1)];
            if (bitmap == null)
                return;

            double zoom = 0.01 * _config.DefaultZoom;
            _mapPanPosition.X = ClampCentre(_mapPanPosition.X, bitmap.Width * zoom / 2, bitmap.Width);
            _mapPanPosition.Y = ClampCentre(_mapPanPosition.Y, bitmap.Height * zoom / 2, bitmap.Height);
        }

        private static double ClampCentre(double centre, double half, double size)
            => half * 2 >= size ? size / 2 : Math.Clamp(centre, half, size - half);

        private void HandlePOIHover(MouseEventArgs e)
        {
            _hoveredPoi = null;
            if (InGame && _pointsOfInterest.Count > 0)
            {
                var mapParams = GetMapLocation();
                
                if (mapParams == null)
                {
                    return; 
                }
                
                var mousePos = new SKPoint(e.X, e.Y);

                _hoveredPoi = _pointsOfInterest.FirstOrDefault(poi =>
                {
                    var poiRenderPos = new Vector3D(poi.Position.X + AbsoluteLocation.X, poi.Position.Y + AbsoluteLocation.Y, poi.Position.Z + AbsoluteLocation.Z);
                    var poiPos = poiRenderPos.ToMapPos(_selectedMap).ToZoomedPos(mapParams).GetPoint();
                    return SKPoint.Distance(mousePos, poiPos) < 20 * _uiScale;
                });
            }
        }

        private void skMapCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            // Clicks on the diagnostics pill or panel belong to it, not to the map
            if (_diagnostics?.Contains(e.Location) == true)
                return;

            if (e.Button == MouseButtons.Left && _isFreeMapToggled)
            {
                _isDragging = true;
                _lastMousePosition = e.Location;
            }

            if (e.Button == MouseButtons.Right && _hoveredPoi != null)
            {
                _pointsOfInterest.Remove(_hoveredPoi);
                PublishFireSupportTarget();
                _hoveredPoi = null;
                _mapCanvas.Invalidate();
            }
        }

        private void skMapCanvas_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && this.InGame && this.LocalPlayer is not null)
            {
                if (_selectedMap == null || _loadedBitmaps == null || _loadedBitmaps.Length == 0)
                {
                    return;
                }

                var mapParams = GetMapLocation();
                
                if (mapParams == null)
                {
                    return; 
                }
                
                var mouseX = e.X / mapParams.XScale + mapParams.Bounds.Left;
                var mouseY = e.Y / mapParams.YScale + mapParams.Bounds.Top;

                var worldX = (mouseX - _selectedMap.ConfigFile.X) / _selectedMap.ConfigFile.Scale;
                var worldY = (mouseY - _selectedMap.ConfigFile.Y) / _selectedMap.ConfigFile.Scale;
                var worldZ = GroundHeightAt(worldX, worldY);

                var poiPosition = new Vector3D(worldX - AbsoluteLocation.X, worldY - AbsoluteLocation.Y, worldZ - AbsoluteLocation.Z);

                AddPointOfInterest(poiPosition, "POI");

                _mapCanvas.Invalidate();
            }
        }

        private void skMapCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                _lastMousePosition = e.Location;
            }
        }

        private void skMapCanvas_PaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
        {
            try
            {
                PerfStats.FrameBegin();
                var canvas = e.Surface.Canvas;
                canvas.Clear();
                RunFrameHousekeeping();

                if (IsReadyToRender())
                {
                    lock (_renderLock)
                    {
                        // Eliminates 5 redundant GetMapLocation() calls (each allocating MapParameters and running
                        // AspectFill) plus one per AA projectile: map parameters are computed once per frame.
                        // Eliminates live per-entity reads (locked Position getters, repeated team lookups) and the
                        // three separate dictionary enumerations: actors are copied once into a reusable snapshot.
                        // Built first: it also advances the position smoothing the map centre depends on.
                        _frame.Build(AllActors, LocalPlayer);

                        var mapParams = GetMapLocation();

                        // Eliminates per-frame List allocations for the dead-marker / AA lists.
                        _deadMarkerBuffer.Clear();
                        _projectileAABuffer.Clear();

                        DrawMap(canvas, mapParams);
                        DrawObjectives(canvas, mapParams);
                        DrawActors(canvas, mapParams, _deadMarkerBuffer, _projectileAABuffer);
                        DrawPOIs(canvas, mapParams);
                        DrawToolTips(canvas, mapParams);
                        DrawTopMost(canvas, mapParams, _deadMarkerBuffer, _projectileAABuffer);
                        
                        // Draw aimview widget
                        _aimviewWidget?.Draw(canvas);
                        
                        // Draw TInfo widget
                        _tInfoWidget?.Draw(canvas);

                        // Draw tickets widget
                        _ticketsWidget?.Draw(canvas);
                    }
                }
                else
                {
                    DrawStatusText(canvas);
                }

                // Fixed status pill (and the detail panel when open), drawn over everything else
                _diagnostics?.Draw(canvas);

                canvas.Flush();
                PerfStats.FrameEnd();
            }
            catch { }
        }

        #region Overlay styling
        // Same look as the radar widgets: dark rounded card with a hairline border
        private static readonly Color CardColor = Color.FromArgb(16, 19, 24);
        private static readonly Color CardBorderColor = Color.FromArgb(58, 63, 72);
        private static readonly Color CardValueColor = Color.White;
        private const int CardRadius = 6;

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = radius * 2;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>Cuts a control to rounded corners, and keeps it that way when it changes size.</summary>
        private static void ApplyRoundedRegion(Control control, int radius)
        {
            void Apply()
            {
                if (control.Width <= 0 || control.Height <= 0)
                    return;
                using var path = RoundedRect(new RectangleF(0, 0, control.Width, control.Height), radius);
                var old = control.Region;
                control.Region = new Region(path);
                old?.Dispose();
            }

            Apply();
            control.SizeChanged += (s, e) => Apply();
        }

        private void StyleOverlayControls()
        {
            // Map follow / free toggle
            chkMapFree.FlatStyle = FlatStyle.Flat;
            chkMapFree.BackColor = CardColor;
            chkMapFree.ForeColor = CardValueColor;
            chkMapFree.FlatAppearance.BorderSize = 1;
            chkMapFree.FlatAppearance.BorderColor = CardBorderColor;
            chkMapFree.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 35, 43);
            chkMapFree.FlatAppearance.MouseDownBackColor = Color.FromArgb(40, 46, 56);
            chkMapFree.FlatAppearance.CheckedBackColor = Color.FromArgb(14, 72, 94); // free mode: lit up
            chkMapFree.Cursor = Cursors.Hand;
            ApplyRoundedRegion(chkMapFree, CardRadius);
        }
        #endregion

        private void btnToggleMap_Click(object sender, EventArgs e)
        {
            ToggleMap();
        }
        #endregion

        #region GUI Events / Functions
        #region General Helper Functions
        private bool ToggleMap()
        { /*
            if (!btnToggleMap.Enabled)
                return false;

            if (_mapSelectionIndex == _maps.Count - 1)
                _mapSelectionIndex = 0; // Start over when end of maps reached
            else
                _mapSelectionIndex++; // Move onto next map

            tabRadar.Text = $"Radar ({_maps[_mapSelectionIndex].Name})";
            _mapChangeTimer.Restart(); // Start delay
            ClearPointsOfInterest();
            Logger.Info("Toggled Map");
            */
            return true; 
        } 

        private void InitiateUIScaling()
        {
            _uiScale = (.01f * _config.UIScale);

            #region Update Paints/Text
            SKPaints.TextOutline.StrokeWidth = 2 * _uiScale;
            SKPaints.TextRadarStatus.TextSize = 48 * _uiScale;
            SKPaints.PaintBase.StrokeWidth = 3 * _uiScale;
            SKPaints.PaintTransparentBacker.StrokeWidth = 1 * _uiScale;
            #endregion

            InitiateFontSize();
            
            _aimviewWidget?.SetScaleFactor(_uiScale);
            _tInfoWidget?.SetScaleFactor(_uiScale);
            _ticketsWidget?.SetScaleFactor(_uiScale);
            _diagnostics?.SetScaleFactor(_uiScale);
            
            if (_mapCanvas != null)
            {
                _mapCanvas.Invalidate();
            }
        }

        private void InitiateFont()
        {
            var fontToUse = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
            SKPaints.TextBase.Typeface = fontToUse;
            SKPaints.TextOutline.Typeface = fontToUse;
            SKPaints.TextRadarStatus.Typeface = fontToUse;
        }

        private void InitiateFontSize()
        {
            SKPaints.TextBase.TextSize = _config.FontSize * _uiScale;
            SKPaints.TextOutline.TextSize = _config.FontSize * _uiScale;
        }

        private void LoadRadarColors()
        {
            SKPaints.LoadColorsFromConfig(_config.RadarColors);
            // Refresh tech marker paints to apply new colors immediately
            _renderer.RefreshTechPaints();
        }

        #region Color Configuration Methods
        /// <summary>
        /// Opens color picker for a specific radar color setting
        /// </summary>
        private void OpenColorPicker(string colorName, ColorConfig currentColor, Action<ColorConfig> onColorChanged)
        {
            using (var colorDialog = new ColorDialog())
            {
                colorDialog.Color = currentColor.ToDrawingColor();
                colorDialog.FullOpen = true;
                
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    var newColor = new ColorConfig(
                        colorDialog.Color.R,
                        colorDialog.Color.G,
                        colorDialog.Color.B
                    );
                    
                    onColorChanged(newColor);
                    LoadRadarColors(); // Reload colors into SKPaints
                    _mapCanvas.Invalidate(); // Refresh the map
                    Config.SaveConfig(_config); // Save configuration
                }
            }
        }

        /// <summary>
        /// Creates a color picker button for the specified color setting
        /// </summary>
        private Button CreateColorButton(string text, ColorConfig color, Action<ColorConfig> onColorChanged)
        {
            var button = new Button
            {
                Text = text,
                Size = new Size(120, 30),
                BackColor = color.ToDrawingColor(),
                ForeColor = GetContrastColor(color.ToDrawingColor()),
                UseVisualStyleBackColor = false,
                FlatStyle = FlatStyle.Flat
            };

            button.Click += (s, e) => OpenColorPicker(text, color, (newColor) =>
            {
                onColorChanged(newColor);
                button.BackColor = newColor.ToDrawingColor();
                button.ForeColor = GetContrastColor(newColor.ToDrawingColor());
            });

            return button;
        }

        /// <summary>
        /// Gets contrasting text color (black or white) for the given background color
        /// </summary>
        private System.Drawing.Color GetContrastColor(System.Drawing.Color backgroundColor)
        {
            // Calculate luminance to determine if we should use black or white text
            double luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255;
            return luminance > 0.5 ? System.Drawing.Color.Black : System.Drawing.Color.White;
        }

        /// <summary>
        /// Creates a unique, clean color configuration panel with "Color name - Color Selection" layout
        /// </summary>
        public Panel CreateColorConfigurationPanel()
        {
            _colorPreviewRefreshers.Clear();
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.FromArgb(32, 32, 32),
                Padding = new Padding(30)
            };

            // Title
            var titleLabel = new Label
            {
                Text = "Color Configuration",
                Font = new Font("Segoe UI", 14, FontStyle.Regular),
                ForeColor = System.Drawing.Color.White,
                Location = new Point(30, 30),
                AutoSize = true
            };
            mainPanel.Controls.Add(titleLabel);

            // Content area
            var contentPanel = new Panel
            {
                Location = new Point(30, 70),
                Size = new Size(mainPanel.Width - 60, mainPanel.Height - 140),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                AutoScroll = true,
                BackColor = System.Drawing.Color.FromArgb(32, 32, 32)
            };
            mainPanel.Controls.Add(contentPanel);

            // All color configurations
            var colorConfigs = new (string name, ColorConfig color, Action<ColorConfig> setter)[]
            {
                ("Squad Members", _config.RadarColors.SquadMembers, (ColorConfig c) => _config.RadarColors.SquadMembers = c),
                ("Friendly Players", _config.RadarColors.FriendlyPlayers, (ColorConfig c) => _config.RadarColors.FriendlyPlayers = c),
                ("Enemy Players", _config.RadarColors.EnemyPlayers, (ColorConfig c) => _config.RadarColors.EnemyPlayers = c),
                ("Unknown Players", _config.RadarColors.UnknownPlayers, (ColorConfig c) => _config.RadarColors.UnknownPlayers = c),
                ("Friendly Vehicles", _config.RadarColors.FriendlyVehicles, (ColorConfig c) => _config.RadarColors.FriendlyVehicles = c),
                ("Enemy Vehicles", _config.RadarColors.EnemyVehicles, (ColorConfig c) => _config.RadarColors.EnemyVehicles = c),
                ("Unclaimed Vehicles", _config.RadarColors.UnclaimedVehicles, (ColorConfig c) => _config.RadarColors.UnclaimedVehicles = c),
                ("Regular Projectiles", _config.RadarColors.RegularProjectiles, (ColorConfig c) => _config.RadarColors.RegularProjectiles = c),
                ("AA Projectiles", _config.RadarColors.AAProjectiles, (ColorConfig c) => _config.RadarColors.AAProjectiles = c),
                ("Small Projectiles", _config.RadarColors.SmallProjectiles, (ColorConfig c) => _config.RadarColors.SmallProjectiles = c),
                ("Enemy Player Distance", _config.RadarColors.EnemyPlayerDistanceText, (ColorConfig c) => _config.RadarColors.EnemyPlayerDistanceText = c),
                ("Vehicle Distance", _config.RadarColors.VehicleDistanceText, (ColorConfig c) => _config.RadarColors.VehicleDistanceText = c),
                ("Dead Markers", _config.RadarColors.DeadMarkers, (ColorConfig c) => _config.RadarColors.DeadMarkers = c),
                ("Admin Markers", _config.RadarColors.AdminMarkers, (ColorConfig c) => _config.RadarColors.AdminMarkers = c)
            };

            // Create color rows with unique design
            int yPosition = 0;
            foreach (var config in colorConfigs)
            {
                var colorRow = CreateColorRow(config.name, config.color, config.setter);
                colorRow.Location = new Point(0, yPosition);
                colorRow.Width = contentPanel.Width - 20;
                colorRow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                contentPanel.Controls.Add(colorRow);
                yPosition += 50;
            }

            // Reset button
            var resetButton = new Button
            {
                Text = "Reset All Colors",
                Size = new Size(140, 32),
                Location = new Point(0, yPosition + 20),
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };

            resetButton.FlatAppearance.BorderSize = 1;
            resetButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 80, 80);
            resetButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(70, 70, 70);

            resetButton.Click += (s, e) =>
            {
                if (MessageBox.Show("Reset all colors to defaults?", "Confirm Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _config.RadarColors = new RadarColors();
                    LoadRadarColors();
                    _mapCanvas.Invalidate();
                    Config.SaveConfig(_config);
                    
                    // Refresh panel
                    tabColors.Controls.Clear();
                    tabColors.Controls.Add(CreateColorConfigurationPanel());
                    RefreshColorPreviews();
                }
            };

            contentPanel.Controls.Add(resetButton);
            return mainPanel;
        }

        /// <summary>
        /// Creates a unique color row with "Color name - Color Selection" design
        /// </summary>
        private Panel CreateColorRow(string colorName, ColorConfig color, Action<ColorConfig> onColorChanged)
        {
            var rowPanel = new Panel
            {
                Height = 40,
                BackColor = System.Drawing.Color.FromArgb(40, 40, 40),
                Margin = new Padding(0, 0, 0, 10)
            };

            // Color name label
            var nameLabel = new Label
            {
                Text = colorName,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                ForeColor = System.Drawing.Color.White,
                Location = new Point(15, 10),
                Size = new Size(200, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };
            rowPanel.Controls.Add(nameLabel);

            // Dash separator
            var dashLabel = new Label
            {
                Text = "—",
                Font = new Font("Segoe UI", 12f, FontStyle.Regular),
                ForeColor = System.Drawing.Color.FromArgb(120, 120, 120),
                Location = new Point(220, 8),
                Size = new Size(20, 24),
                TextAlign = ContentAlignment.MiddleCenter
            };
            rowPanel.Controls.Add(dashLabel);

            // Color preview box
            var colorBox = new Panel
            {
                Size = new Size(30, 24),
                Location = new Point(250, 8),
                BackColor = color.ToDrawingColor(),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            rowPanel.Controls.Add(colorBox);

            // Color hex label
            var hexLabel = new Label
            {
                Text = ColorToHex(color.ToDrawingColor()),
                Font = new Font("Consolas", 9f, FontStyle.Regular),
                ForeColor = System.Drawing.Color.FromArgb(180, 180, 180),
                Location = new Point(290, 10),
                Size = new Size(80, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };
            rowPanel.Controls.Add(hexLabel);

            // The swatch always shows the color currently set, including the one loaded from the config
            var current = color;
            void RefreshPreview()
            {
                colorBox.BackColor = current.ToDrawingColor();
                hexLabel.Text = ColorToHex(current.ToDrawingColor());
            }
            _colorPreviewRefreshers.Add(RefreshPreview);

            // Click handlers for color selection
            Action openColorPicker = () => OpenColorPicker(colorName, current, (newColor) =>
            {
                onColorChanged(newColor);
                current = newColor;
                RefreshPreview();

                // Refresh tech marker paints to apply new colors immediately
                _renderer.RefreshTechPaints();
            });

            rowPanel.Click += (s, e) => openColorPicker();
            colorBox.Click += (s, e) => openColorPicker();
            nameLabel.Click += (s, e) => openColorPicker();
            hexLabel.Click += (s, e) => openColorPicker();

            // Hover effects
            rowPanel.MouseEnter += (s, e) => rowPanel.BackColor = System.Drawing.Color.FromArgb(50, 50, 50);
            rowPanel.MouseLeave += (s, e) => rowPanel.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);

            return rowPanel;
        }

        private readonly List<Action> _colorPreviewRefreshers = new();

        private void RefreshColorPreviews()
        {
            foreach (var refresh in _colorPreviewRefreshers)
                refresh();
        }

        /// <summary>
        /// Converts a color to hex string
        /// </summary>
        private string ColorToHex(System.Drawing.Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        #endregion

        private DialogResult ShowErrorDialog(string message)
        {
            return new MaterialDialog(this, "Error", message, "OK", false, "", true).ShowDialog(this);
        }

        private void LoadMaps()
        {
            var dir = new DirectoryInfo($"{Environment.CurrentDirectory}\\Maps");
            if (!dir.Exists)
            {
                Program.Log("Maps directory not found. This should have been caught at startup.");
                return;
            }

            // Map packs (one folder per map: tiles, heights, map.json)
            foreach (var packDir in MapPack.FindPacks(dir.FullName))
            {
                try
                {
                    _maps.Add(MapPack.Load(packDir));
                }
                catch (Exception ex)
                {
                    Program.Log($"Error loading map pack {Path.GetFileName(packDir)}: {ex.Message}");
                }
            }

            // Single-image maps from before map packs, only for maps that have no pack
            foreach (var config in dir.GetFiles("*.json"))
            {
                try
                {
                    var name = Path.GetFileNameWithoutExtension(config.Name);
                    var mapConfig = MapConfig.LoadFromFile(config.FullName);
                    if (_maps.Any(m => m.ConfigFile.MapID.Any(id => mapConfig.MapID.Contains(id, StringComparer.OrdinalIgnoreCase))))
                        continue;

                    var map = new Map(name.ToUpper(), mapConfig, config.FullName);
                    map.ConfigFile.MapLayers = map.ConfigFile.MapLayers.OrderBy(x => x.MinHeight).ToList();
                    _maps.Add(map);
                }
                catch (Exception ex)
                {
                    Program.Log($"Error loading map config {config.Name}: {ex.Message}");
                }
            }

            if (_maps.Count == 0)
            {
                Program.Log("No map packs or .json map configs found in Maps directory.");
                return;
            }

            var ordered = _maps.OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
            _maps.Clear();
            _maps.AddRange(ordered);

            Program.Log($"Loaded {_maps.Count} map configurations.");
        }
        private void LoadConfig()
        {
            #region Settings
            #region General
            #region UI Config
            chkShowEnemyDistance.Checked = _config.ShowEnemyDistance;
            chkEnableAimview.Checked = _config.EnableAimview;
            chkHighAlert.Checked = _config.HighAlert;
            chkShowObjectives.Checked = _config.ShowObjectives;

            trkAimLength.Value = _config.PlayerAimLineLength;
            trkUIScale.Value = _config.UIScale;
            #endregion

            #region ESP Config
            chkEnableEsp.Checked = _config.EnableEsp;
            chkEnableBones.Checked = _config.EspBones;
            trkEspMaxDistance.Value = (int)_config.EspMaxDistance;
            lblEspMaxDistance.Text = $"Player Max Distance: {_config.EspMaxDistance}m";
            trkEspVehicleMaxDistance.Value = (int)_config.EspVehicleMaxDistance;
            lblEspVehicleMaxDistance.Text = $"Vehicle Max Distance: {_config.EspVehicleMaxDistance}m";
            chkEspShowVehicles.Checked = _config.EspShowVehicles;
            chkShowAllies.Checked = _config.EspShowAllies;
            chkEspShowNames.Checked = _config.EspShowNames;
            chkEspShowDistance.Checked = _config.EspShowDistance;
            chkEspShowHealth.Checked = _config.EspShowHealth;
            txtEspFontSize.Text = _config.ESPFontSize.ToString();
            trkTechMarkerScale.Value = _config.TechMarkerScale;
            trkAimviewOpacity.Value = Math.Clamp(_config.AimviewOpacity, trkAimviewOpacity.Minimum, trkAimviewOpacity.Maximum);
            trkInfoWidgetOpacity.Value = Math.Clamp(_config.InfoWidgetOpacity, trkInfoWidgetOpacity.Minimum, trkInfoWidgetOpacity.Maximum);
            UpdateOpacityLabels();
            #endregion
            #endregion

            #region Features Config
            chkDisableSuppression.Checked = _config.DisableSuppression;
            chkSetInteractionDistances.Checked = _config.SetInteractionDistances;
            chkAllowShootingInMainBase.Checked = _config.AllowShootingInMainBase;
            chkSpeedHack.Checked = _config.SetSpeedHack;
            chkAirStuck.Checked = _config.AirStuckEnabled;
            chkDisableCollision.Checked = _config.DisableCollision;
            chkQuickZoom.Checked = _config.QuickZoom;
            chkRapidFire.Checked = _config.RapidFire;
            chkInfiniteAmmo.Checked = _config.InfiniteAmmo;
            chkQuickSwap.Checked = _config.QuickSwap;
            chkForceFullAuto.Checked = _config.ForceFullAuto;
            chkNoCameraShake.Checked = _config.NoCameraShake;
            chkFreeCamera.Checked = _config.FreeCameraEnabled;
            chkNoFoliage.Checked = _config.RenderNoFoliage;
            chkShowDiagnostics.Checked = _config.ShowDiagnostics;
            chkNoRecoil.Checked = _config.NoRecoil;
            chkNoSpread.Checked = _config.NoSpread;
            chkNoSway.Checked = _config.NoSway;
            chkInstantGrenade.Checked = _config.InstantGrenade;
            chkQuickAds.Checked = _config.QuickAds;
            chkFireSupport.Checked = _config.FireSupport;
            cmbFireSupportMode.SelectedIndex = _config.FireSupportMode == (int)squad_dma.Source.Squad.Features.FireSupportMode.Drop ? 1 : 0;
            trkFireSupportSpread.Value = Math.Clamp((int)_config.FireSupportSpreadMeters, trkFireSupportSpread.Minimum, trkFireSupportSpread.Maximum);
            lblFireSupportSpread.Text = $"Fire Support Spread: {trkFireSupportSpread.Value} m";
            #endregion

            #endregion
            InitiateFont();
            InitiateUIScaling();
            LoadRadarColors();
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
                // The wait happens on a pool thread, so the UI thread stays free for input and timers between frames.
                await Task.Run(WaitForNextFrame);
                _mapCanvas.Refresh(); // draw next frame
            }
        }

        private const double TargetFrameMs = 1000.0 / 120;
        private long _nextFrameTicks;

        /// <summary>Caps the radar at ~120 fps: sleeps most of the gap, spins the last bit for accuracy.</summary>
        private void WaitForNextFrame()
        {
            long now = Stopwatch.GetTimestamp();
            long frame = (long)(TargetFrameMs * Stopwatch.Frequency / 1000.0);
            if (_nextFrameTicks == 0 || now - _nextFrameTicks > frame * 4)
                _nextFrameTicks = now; // first frame, or we fell far behind: don't try to catch up

            _nextFrameTicks += frame;

            while (true)
            {
                double remainingMs = (_nextFrameTicks - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
                if (remainingMs <= 0) return;
                if (remainingMs > 2) Thread.Sleep(1);
                else Thread.SpinWait(50);
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

            // Load the new map first, outside the render lock, so painting is only held up for the swap itself.
            var newMap = _maps[_mapSelectionIndex];
            MapImage[] newImages = null;
            try
            {
                newImages = new MapImage[newMap.ConfigFile.MapLayers.Count];
                for (int i = 0; i < newImages.Length; i++)
                    newImages[i] = newMap.ConfigFile.MapLayers[i].LoadImage();
            }
            catch (Exception ex)
            {
                throw new Exception($"ERROR loading map {newMap.Name}: {ex}");
            }

            lock (_renderLock)
            {
                try
                {
                    _selectedMap = newMap; // Swap map

                    var oldImages = _loadedBitmaps;
                    _loadedBitmaps = newImages;

                    if (oldImages is not null)
                    {
                        foreach (var bitmap in oldImages)
                            bitmap?.Dispose(); // Cleanup resources
                    }

                    TerrainHeightmap.Select(_selectedMap);
                }
                catch (Exception ex)
                {
                    throw new Exception($"ERROR loading map {_selectedMap.Name}: {ex}");
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

        private void HandleGameStateChange()
        {
            var currentGameStatus = Memory.GameStatus;

            
            if (currentGameStatus == _previousGameStatus)
            {
                if (currentGameStatus == GameStatus.InGame && Memory._game != null)
                {
                    UpdateTicketsDisplay();
                }
                return;
            }
                        
            if (currentGameStatus == GameStatus.InGame)
            {
                _lastFriendlyTickets = 0;
                _lastEnemyTickets = 0;
                _lastKills = 0;
                _lastWoundeds = 0;
                
                Task.Run(async () => await ApplyFeaturesAsync());
            }
            else if (currentGameStatus == GameStatus.Menu && _previousGameStatus == GameStatus.InGame)
            {
                _lastFriendlyTickets = 0;
                _lastEnemyTickets = 0;
                _lastKills = 0;
                _lastWoundeds = 0;
                
                ClearPointsOfInterest();
            }
            else if (currentGameStatus == GameStatus.NotFound)
            {
                _lastFriendlyTickets = 0;
                _lastEnemyTickets = 0;
                _lastKills = 0;
                _lastWoundeds = 0;
            }
            
            _previousGameStatus = currentGameStatus;
        }

        private async Task ApplyFeaturesAsync()
        {
            const int retryDelay = 250;
            var game = Memory._game; // features belong to this map, stop once the game is replaced or ends

            while (true)
            {
                if (!ReferenceEquals(Memory._game, game) || Memory.GameStatus == GameStatus.NotFound)
                    return;

                if (game != null && game.InGame)
                {
                    try
                    {
                        if (Memory._game._soldierManager?.IsLocalPlayerValid() == true)
                        {
                            if (_config.DisableSuppression)
                                Memory._game.SetSuppression(true);
                            
                            if (_config.SetInteractionDistances)
                                Memory._game.SetInteractionDistances(true);
                            
                            if (_config.AllowShootingInMainBase)
                                Memory._game.SetShootingInMainBase(true);
                            
                            if (_config.SetSpeedHack)
                                Memory._game.SetSpeedHack(true);
                            
                            // The camera is never resumed on its own: a new session starts with the soldier's view
                            if (_config.SetFreeCamera)
                            {
                                _config.SetFreeCamera = false;
                            }

                            if (_config.RenderNoFoliage)
                                Memory._game.RefreshRenderTweaks();

                            if (_config.SetAirStuck)
                                Memory._game.SetAirStuck(true);
                                
                            if (_config.RapidFire)
                                Memory._game.SetRapidFire(true);
                                
                            if (_config.InfiniteAmmo)
                                Memory._game.SetInfiniteAmmo(true);
                                
                            if (_config.QuickSwap)
                                Memory._game.SetQuickSwap(true);
                                
                            if (_config.DisableCollision)
                                Memory._game.DisableCollision(true);

                            if (_config.ForceFullAuto)
                                Memory._game.SetForceFullAuto(true);

                            if (_config.NoSpread)
                                Memory._game.SetNoSpread(true);

                            if (_config.NoRecoil)
                                Memory._game.SetNoRecoil(true);

                            if (_config.NoSway)
                                Memory._game.SetNoSway(true);

                            if (_config.NoCameraShake)
                                Memory._game.SetNoCameraShake(true);

                            if (_config.InstantGrenade)
                                Memory._game.SetInstantGrenade(true);

                            if (_config.QuickAds)
                                Memory._game.SetQuickAds(true);

                            if (_config.FireSupport)
                                Memory._game.SetFireSupport(true);

                            return;
                        }

                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error applying features: {ex.Message}");
                    }
                }

                await Task.Delay(retryDelay);
            }
        }
        #endregion

        #region Radar Tab
        #region Helper Functions
        /// <summary>Per-frame upkeep while in a match: follows the map the game loaded and trims GPU resources once a second.</summary>
        private void RunFrameHousekeeping()
        {
            if (!this.InGame || this.LocalPlayer is null)
                return;

            UpdateSelectedMap();

            if (_housekeepingWatch.ElapsedMilliseconds >= 1000)
            {
                // Only drop GPU resources that have gone unused; a full purge every second forced the map tiles to be re-uploaded.
                _mapCanvas.GRContext.PurgeUnusedResources(10_000);
                _housekeepingWatch.Restart();
            }
        }

        private void UpdateTicketsDisplay()
        {
            if (Memory.GameStatus != GameStatus.InGame || Memory._game == null)
                return;

            var gameTickets = Memory._game.GameTickets;
            int friendly = 0;
            int enemy = 0;

            if (gameTickets != null)
            {
                friendly = gameTickets.FriendlyTickets;
                enemy = gameTickets.EnemyTickets;
            }

            var gameStats = Memory._game.GameStats;
            int kills = 0;
            int woundeds = 0;

            if (gameStats != null)
            {
                kills = gameStats.Kills;
                woundeds = gameStats.Woundeds;
            }

            if (friendly != _lastFriendlyTickets ||
                enemy != _lastEnemyTickets ||
                kills != _lastKills ||
                woundeds != _lastWoundeds)
            {
                _lastFriendlyTickets = friendly;
                _lastEnemyTickets = enemy;
                _lastKills = kills;
                _lastWoundeds = woundeds;
            }
        }




        private void UpdateSelectedMap()
        {
            string currentMap = MapName;

            // Eliminates a LINQ Any() + closure allocation every frame: the map match can only change when the
            // level name or the selected map changes (or when no map is selected yet), so skip the search while both
            // are unchanged. A manual map swap changes _selectedMap, which still triggers the full check.
            if (_selectedMap is not null && ReferenceEquals(currentMap, _lastMapNameChecked) && ReferenceEquals(_selectedMap, _lastSelectedMapChecked))
                return;
            _lastMapNameChecked = currentMap;

            if (_selectedMap is null || !_selectedMap.ConfigFile.MapID.Any(id => id.Equals(currentMap, StringComparison.OrdinalIgnoreCase)))
            {
                // First try exact match
                var selectedMap = _maps.FirstOrDefault(x => x.ConfigFile.MapID.Any(id => id.Equals(currentMap, StringComparison.OrdinalIgnoreCase)));
                
                // If no exact match found, try partial matching (contains)
                if (selectedMap is null)
                {
                    // Longest name first, so Narva_f is tried before the Narva it contains
                    selectedMap = _maps.OrderByDescending(x => x.Name.Length).FirstOrDefault(x => x.ConfigFile.MapID.Any(id =>
                        id.Contains(currentMap, StringComparison.OrdinalIgnoreCase) ||
                        currentMap.Contains(id, StringComparison.OrdinalIgnoreCase)));
                }

                if (selectedMap is not null)
                {
                    _selectedMap = selectedMap;
                    CleanupLoadedBitmaps();
                    ClearPointsOfInterest();
                    LoadMapBitmaps();
                    TerrainHeightmap.Select(_selectedMap);
                }
                else
                {
                    Logger.Error($"Map Error: Current map '{currentMap}' is not configured. Please add this map name to the corresponding map configuration file.");
                    LogUnmappedMapName(currentMap);
                }
            }

            _lastSelectedMapChecked = _selectedMap;
        }

        private void LoadMapBitmaps()
        {
            var mapLayers = _selectedMap.ConfigFile.MapLayers;
            _loadedBitmaps = new MapImage[mapLayers.Count];

            Parallel.ForEach(mapLayers, (mapLayer, _, _) =>
            {
                lock (_loadMapBitmapsLock)
                {
                    try
                    {
                        _loadedBitmaps[mapLayers.IndexOf(mapLayer)] = mapLayer.LoadImage();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error loading map layer: {ex.Message}");
                    }
                }
            });
        }

        private void CleanupLoadedBitmaps()
        {
            if (_loadedBitmaps is not null)
            {
                Parallel.ForEach(_loadedBitmaps, bitmap => bitmap?.Dispose());
                _loadedBitmaps = null;
            }
        }

        /// <summary>
        /// Logs unmapped map names to console for debugging purposes
        /// </summary>
        /// <param name="mapName">The map name that couldn't be matched</param>
        private void LogUnmappedMapName(string mapName)
        {
            Console.WriteLine($"[MAP DEBUG] Unmapped map name detected: '{mapName}'");
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

        private int GetMapLayerIndex(double playerHeight)
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
            if (_loadedBitmaps == null || _loadedBitmaps.Length == 0)
            {
                return null; 
            }

            int mapLayerIndex = GetMapLayerIndex(localPlayerPos.Height);
            
            if (mapLayerIndex >= _loadedBitmaps.Length || _loadedBitmaps[mapLayerIndex] == null)
            {
                return null;
            }

            var bitmap = _loadedBitmaps[mapLayerIndex];
            double zoomFactor = 0.01 * _config.DefaultZoom;
            double zoomWidth = bitmap.Width * zoomFactor;
            double zoomHeight = bitmap.Height * zoomFactor;

            double left = Math.Max(Math.Min(localPlayerPos.X, bitmap.Width - zoomWidth / 2) - zoomWidth / 2, 0);
            double top = Math.Max(Math.Min(localPlayerPos.Y, bitmap.Height - zoomHeight / 2) - zoomHeight / 2, 0);
            double right = Math.Min(Math.Max(localPlayerPos.X, zoomWidth / 2) + zoomWidth / 2, bitmap.Width);
            double bottom = Math.Min(Math.Max(localPlayerPos.Y, zoomHeight / 2) + zoomHeight / 2, bitmap.Height);

            var bounds = new SKRect(
                (float)left,
                (float)top,
                (float)right,
                (float)bottom
            ).AspectFill(_mapCanvas.CanvasSize);

            return new MapParameters
            {
                UIScale = _uiScale,
                TechScale = localPlayerPos.TechScale,
                MapLayerIndex = mapLayerIndex,
                Bounds = bounds,
                XScale = (double)_mapCanvas.Width / bounds.Width,
                YScale = (double)_mapCanvas.Height / bounds.Height
            };
        }

        private MapParameters GetMapLocation()
        {
            var localPlayer = this.LocalPlayer;
            if (localPlayer is not null)
            {
                var localPlayerPos = new Vector3D(
                    localPlayer.Position.X + AbsoluteLocation.X,
                    localPlayer.Position.Y + AbsoluteLocation.Y,
                    localPlayer.Position.Z + AbsoluteLocation.Z
                );
                var localPlayerMapPos = localPlayerPos.ToMapPos(_selectedMap);
                localPlayerMapPos.TechScale = (.01f * _config.TechMarkerScale); // Set tech scale for map position

                if (_isFreeMapToggled)
                {
                    _mapPanPosition.Height = localPlayerMapPos.Height;
                    _mapPanPosition.TechScale = localPlayerMapPos.TechScale;
                    return GetMapParameters(_mapPanPosition);
                }
                else
                {
                    _mapPanPosition.X = localPlayerMapPos.X;
                    _mapPanPosition.Y = localPlayerMapPos.Y;
                    _mapPanPosition.Height = localPlayerMapPos.Height;
                    _mapPanPosition.TechScale = localPlayerMapPos.TechScale;
                    return GetMapParameters(localPlayerMapPos);
                }
            }
            else
            {
                return GetMapParameters(_mapPanPosition);
            }
        }

        private void DrawMap(SKCanvas canvas, MapParameters mapParams)
        {
            if (grpMapSetup.Visible)
            {
                var lp = LocalPlayer.Position;
                var abs = AbsoluteLocation;
                var pos = (X: lp.X + abs.X, Y: lp.Y + abs.Y, Z: lp.Z + abs.Z);

                // Eliminates a string allocation + WinForms Text assignment (and repaint request) every frame:
                // the caption is only rebuilt when the coordinates actually change.
                if (!_mapSetupTextIsLive || pos != _lastMapSetupPos)
                {
                    grpMapSetup.Text = $"Map Setup - X,Y,Z: {pos.X}, {pos.Y}, {pos.Z}";
                    _lastMapSetupPos = pos;
                    _mapSetupTextIsLive = true;
                }
            }
            else if (_mapSetupTextIsLive || grpMapSetup.Text != "Map Setup")
            {
                grpMapSetup.Text = "Map Setup";
                _mapSetupTextIsLive = false;
            }

            if (mapParams == null)
            {
                return;
            }

            var mapCanvasBounds = new SKRect
            {
                Left = _mapCanvas.Left,
                Right = _mapCanvas.Right,
                Top = _mapCanvas.Top,
                Bottom = _mapCanvas.Bottom
            };

            _loadedBitmaps[mapParams.MapLayerIndex].Draw(
                canvas,
                mapParams.Bounds,
                mapCanvasBounds,
                SKPaints.PaintBitmap
            );
        }

        private void DrawObjectives(SKCanvas canvas, MapParameters mapParams)
        {
            var objectives = Memory._game?.Objectives;
            if (objectives == null || mapParams == null)
                return;

            // The Objectives object is created per match, so the setting is re-applied every frame.
            objectives.Enabled = _config.ShowObjectives;
            objectives.Draw(canvas, mapParams, _selectedMap, _uiScale);
        }

        private void DrawActors(SKCanvas canvas, MapParameters mapParams, List<SKPoint> deadMarkers, List<ActorFrameData> projectileAAs)
        {
            if (!InGame || LocalPlayer is null || AllActors is null || mapParams == null)
                return;

            _renderer.SetScale(_uiScale);

            // Max range circle around the player when a ballistic weapon is equipped.
            var mortarCalculator = Memory._game?._soldierManager?.WeaponDetector;
            if (mortarCalculator != null && mortarCalculator.HasBallisticWeapon && mortarCalculator.CurrentWeaponData != null)
            {
                float maxRangeMeters = mortarCalculator.CalculateMaxDistance(mortarCalculator.CurrentWeaponData);
                float maxRangeMapUnits = maxRangeMeters * 100; // Convert to game units
                float maxRangePixels = (float)(maxRangeMapUnits * _selectedMap.ConfigFile.Scale * mapParams.XScale);

                var playerCenter = LocalPlayer.Position.ToMapPos(_selectedMap).ToZoomedPos(mapParams).GetPoint();

                // Shared paint (width set once per UI scale) replaces a `using var new SKPaint` every frame.
                canvas.DrawCircle(playerCenter.X, playerCenter.Y, maxRangePixels, _renderer.RangeCirclePaint);
            }

            // All entity drawing is bucketed by type and flushed in batched passes inside the renderer.
            _renderer.DrawActors(
                canvas, _frame, mapParams, _selectedMap,
                trkAimLength.Value, _config.HighAlert, _config.ShowEnemyDistance,
                _uiScale, DateTime.Now, deadMarkers, projectileAAs);
        }

        private void DrawTopMost(SKCanvas canvas, MapParameters mapParams, List<SKPoint> deadMarkers, List<ActorFrameData> projectileAAs)
        {
            if (mapParams == null)
                return;

            _renderer.DrawTopMost(canvas, mapParams, _selectedMap, _uiScale, deadMarkers, projectileAAs, _aaProjectileOrigins);
        }

        private static readonly string[] PoiNumberStrings = Enumerable.Range(0, 100).Select(i => i.ToString()).ToArray();

        private void DrawPOIs(SKCanvas canvas, MapParameters mapParams)
        {
            if (mapParams == null)
                return;

            _renderer.SetScale(_uiScale);

            var cfg = _selectedMap.ConfigFile;
            var lp = LocalPlayer.Position; // one locked read per frame (was ~9 per POI)
            float shooterX = (float)lp.X, shooterY = (float)lp.Y;

            // Projection constants hoisted out of the POI loop. POI world position + absolute offset is
            // reduced by the absolute offset again inside ToMapPos, so the offset cancels.
            double bx = (cfg.X - mapParams.Bounds.Left) * mapParams.XScale;
            double by = (cfg.Y - mapParams.Bounds.Top) * mapParams.YScale;
            double sx = cfg.Scale * mapParams.XScale;
            double sy = cfg.Scale * mapParams.YScale;

            var mortarCalculator = Memory._game?._soldierManager?.WeaponDetector;
            bool ballistic = mortarCalculator != null && mortarCalculator.HasBallisticWeapon;
            var weaponData = ballistic ? mortarCalculator.CurrentWeaponData : null;

            // Eliminates a ToLower() string allocation + two Contains scans per POI per frame:
            // orientation only changes when the equipped weapon name changes.
            if (ballistic)
            {
                string weaponName = mortarCalculator.CurrentWeaponName;
                if (!ReferenceEquals(weaponName, _rocketBarrageWeaponName))
                {
                    string lower = weaponName?.ToLower() ?? "";
                    _isRocketBarrageWeapon = lower.Contains("ub32") || lower.Contains("ub-32");
                    _rocketBarrageWeaponName = weaponName;
                }
            }

            while (_poiTrajectoryCache.Count < _pointsOfInterest.Count)
                _poiTrajectoryCache.Add(new PoiTrajectoryCacheEntry());

            var crosshairPaint = _renderer.PoiCrosshairPaint;
            const float crossSize = 3; // Fixed small size for precise center marking

            for (int i = 0; i < _pointsOfInterest.Count; i++)
            {
                var poi = _pointsOfInterest[i];
                var poiPos = poi.Position;
                var center = new SKPoint((float)(bx + poiPos.X * sx), (float)(by + poiPos.Y * sy));

                // Draw damage radius ellipse if ballistic weapon is equipped
                if (ballistic)
                {
                    // Eliminates re-running the ballistic solver (several trig/iteration steps) for every POI on every
                    // frame: the solution only depends on weapon data and the two flat positions, which are
                    // unchanged between frames while the player stands still.
                    var cache = _poiTrajectoryCache[i];
                    float targetX = (float)poiPos.X, targetY = (float)poiPos.Y;
                    var terrain = TerrainHeightmap.Current;
                    if (!ReferenceEquals(cache.WeaponData, weaponData) || !ReferenceEquals(cache.Terrain, terrain) ||
                        cache.ShooterX != shooterX || cache.ShooterY != shooterY ||
                        cache.TargetX != targetX || cache.TargetY != targetY)
                    {
                        // Ground height at both ends from the map's heightmap; flat ground when it has none
                        float shooterZ = 0f, targetZ = 0f;
                        if (terrain != null &&
                            terrain.TryGetHeightCm(shooterX, shooterY, out double shooterGround) &&
                            terrain.TryGetHeightCm(targetX, targetY, out double targetGround))
                        {
                            shooterZ = (float)shooterGround;
                            targetZ = (float)targetGround;
                        }

                        cache.Solution = mortarCalculator.CalculateTrajectory(
                            new Vector3(targetX, targetY, targetZ), new Vector3(shooterX, shooterY, shooterZ));
                        cache.WeaponData = weaponData;
                        cache.Terrain = terrain;
                        cache.ShooterX = shooterX; cache.ShooterY = shooterY;
                        cache.TargetX = targetX; cache.TargetY = targetY;
                    }

                    var solution = cache.Solution;
                    if (solution != null && solution.SpreadEllipse != null)
                    {
                        // Calculate elliptical spread dimensions in pixels (meters -> game units -> pixels)
                        float horizontalSpreadPixels = (float)(solution.SpreadEllipse.Horizontal * 100 * cfg.Scale * mapParams.XScale);
                        float verticalSpreadPixels = (float)(solution.SpreadEllipse.Vertical * 100 * cfg.Scale * mapParams.YScale);

                        // Bearing from the local player to the target = direction the projectiles travel
                        double bearingRadians = Math.Atan2(poiPos.Y - lp.Y, poiPos.X - lp.X);

                        // Rocket barrages: spread is perpendicular to the bearing. Mortars: points toward the player.
                        if (_isRocketBarrageWeapon)
                            bearingRadians += Math.PI / 2;

                        var ellipseRect = new SKRect(
                            center.X - horizontalSpreadPixels / 2,  // Width = horizontal spread
                            center.Y - verticalSpreadPixels / 2,   // Height = vertical spread
                            center.X + horizontalSpreadPixels / 2,
                            center.Y + verticalSpreadPixels / 2
                        );

                        // One rotate-about-point replaces Translate/Rotate/Translate, and DrawOval replaces
                        // building an SKPath per POI per frame; both paints are shared (width set per UI scale).
                        canvas.Save();
                        canvas.RotateDegrees((float)(bearingRadians * 180.0 / Math.PI), center.X, center.Y);
                        canvas.DrawOval(ellipseRect, _renderer.SpreadFillPaint);
                        canvas.DrawOval(ellipseRect, _renderer.SpreadStrokePaint);
                        canvas.Restore();
                    }
                }

                // Draw crosshair
                canvas.DrawLine(
                    center.X - crossSize, center.Y - crossSize,
                    center.X + crossSize, center.Y + crossSize,
                    crosshairPaint);

                canvas.DrawLine(
                    center.X + crossSize, center.Y - crossSize,
                    center.X - crossSize, center.Y + crossSize,
                    crosshairPaint);

                // Draw marker number instead of text
                DrawPOINumber(canvas, center, i + 1);
            }

            // Draw minimum distance circle if ballistic weapon is equipped and has minimum distance
            if (ballistic && weaponData != null && mortarCalculator.CurrentWeaponData.MinDistance > 0)
            {
                float minDistanceMapUnits = mortarCalculator.CurrentWeaponData.MinDistance * 100; // Convert to game units
                float minDistancePixels = (float)(minDistanceMapUnits * cfg.Scale * mapParams.XScale);

                // Draw minimum distance circle centered on local player
                float cx = (float)(bx + lp.X * sx);
                float cy = (float)(by + lp.Y * sy);
                canvas.DrawCircle(cx, cy, minDistancePixels, _renderer.MinDistanceFillPaint);
                canvas.DrawCircle(cx, cy, minDistancePixels, _renderer.MinDistanceStrokePaint);
            }

            DrawFireSupportImpacts(canvas, bx, by, sx, sy);
        }


        // Fire Support impacts: world positions copied from the feature, then projected to the canvas. Buffers are reused every frame.
        private readonly List<Vector3D> _impactWorldBuffer = new();
        private readonly List<SKPoint> _impactPointBuffer = new();

        private void DrawFireSupportImpacts(SKCanvas canvas, double bx, double by, double sx, double sy)
        {
            if (!_config.FireSupport) return;

            squad_dma.Source.Squad.Features.FireSupport.CopyRecentImpacts(_impactWorldBuffer);
            if (_impactWorldBuffer.Count == 0) return;

            _impactPointBuffer.Clear();
            foreach (var impact in _impactWorldBuffer)
                _impactPointBuffer.Add(new SKPoint((float)(bx + impact.X * sx), (float)(by + impact.Y * sy)));

            _renderer.DrawImpactMarkers(canvas, _impactPointBuffer, _uiScale);
        }

        private void DrawPOINumber(SKCanvas canvas, SKPoint center, int markerNumber)
        {
            // Top right of the damage radius circle, same styling as the distance text.
            // Paints/typeface/baseline offset are built once per UI scale instead of two new SKPaints per POI per frame.
            float x = center.X + 15 * _uiScale;
            float y = center.Y - 15 * _uiScale + _renderer.PoiNumberBaselineOffset;
            string text = (uint)markerNumber < (uint)PoiNumberStrings.Length ? PoiNumberStrings[markerNumber] : markerNumber.ToString();

            canvas.DrawText(text, x, y, _renderer.PoiNumberOutline);
            canvas.DrawText(text, x, y, _renderer.PoiNumberFill);
        }
        

        private void DrawToolTips(SKCanvas canvas, MapParameters mapParams)
        {
            var localPlayer = this.LocalPlayer;

            if (mapParams == null)
            {
                return; 
            }

            if (localPlayer is not null)
            {
                if (_closestPlayerToMouse is not null)
                {
                    var localPlayerPos = new Vector3D(
                        localPlayer.Position.X + AbsoluteLocation.X,
                        localPlayer.Position.Y + AbsoluteLocation.Y,
                        localPlayer.Position.Z + AbsoluteLocation.Z
                    );
                    var hoveredPlayerPos = new Vector3D(
                        _closestPlayerToMouse.Position.X + AbsoluteLocation.X,
                        _closestPlayerToMouse.Position.Y + AbsoluteLocation.Y,
                        _closestPlayerToMouse.Position.Z + AbsoluteLocation.Z
                    );
                    
                    var dx = localPlayerPos.X - hoveredPlayerPos.X;
                    var dy = localPlayerPos.Y - hoveredPlayerPos.Y;
                    var dz = localPlayerPos.Z - hoveredPlayerPos.Z;
                    var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                    var distanceText = $"{(int)Math.Round(distance / 100)}m";

                    var playerPos = new Vector3D(
                        _closestPlayerToMouse.Position.X + AbsoluteLocation.X,
                        _closestPlayerToMouse.Position.Y + AbsoluteLocation.Y,
                        _closestPlayerToMouse.Position.Z + AbsoluteLocation.Z
                    );
                    var playerZoomedPos = playerPos
                        .ToMapPos(_selectedMap)
                        .ToZoomedPos(mapParams);

                    playerZoomedPos.DrawToolTip(canvas, _closestPlayerToMouse, distanceText);
                }
            }
        }

        /// <summary>
        /// Ground height at a map position (cm). Falls back to the local player's height when the map has no height
        /// data, which is the old flat-ground assumption.
        /// </summary>
        private double GroundHeightAt(double worldX, double worldY)
        {
            var terrain = TerrainHeightmap.Current;
            if (terrain != null && terrain.TryGetHeightCm(worldX, worldY, out double height))
                return height;
            return LocalPlayer.Position.Z;
        }

        private void btnAddPOI_Click(object sender, EventArgs e)
        {
            var localPlayer = this.LocalPlayer;
            if (localPlayer is not null)
            {
                var position = new Vector3D(
                    localPlayer.Position.X + AbsoluteLocation.X,
                    localPlayer.Position.Y + AbsoluteLocation.Y,
                    localPlayer.Position.Z + AbsoluteLocation.Z
                );
                AddPointOfInterest(position, "POI 1");

                _mapCanvas.Invalidate();
            }
        }


        private void DrawStatusText(SKCanvas canvas)
        {
            string statusText = Memory.GameStatus == GameStatus.NotFound ? "Game Not Running" :
                                !Ready ? "Game Process Not Running" :
                                !InGame ? "Waiting for Game Start..." :
                                LocalPlayer is null ? "Cannot find LocalPlayer" :
                                _selectedMap is null ? "Loading Map" : null;

            if (statusText != null)
            {
                var centerX = _mapCanvas.Width / 2;
                var centerY = _mapCanvas.Height / 2;
                canvas.DrawText(statusText, centerX, centerY, SKPaints.TextRadarStatus);
            }
        }

        private void ClearPlayerRefs()
        {
            _closestPlayerToMouse = null;
        }

        private T FindClosestObject<T>(IEnumerable<T> objects, Vector2D position, Func<T, Vector2D> positionSelector, float threshold)
            where T : class
        {
            if (objects is null || !objects.Any())
                return null;

            var closestObject = objects.Aggregate(
                (x1, x2) =>
                {
                    if (x2 == null) return x1;
                    
                    var pos1 = positionSelector(x1);
                    var pos2 = positionSelector(x2);
                    
                    var dx1 = pos1.X - position.X;
                    var dy1 = pos1.Y - position.Y;
                    var dist1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                    
                    var dx2 = pos2.X - position.X;
                    var dy2 = pos2.Y - position.Y;
                    var dist2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                    
                    return dist1 < dist2 ? x1 : x2;
                }
            );

            if (closestObject is not null)
            {
                var closestPos = positionSelector(closestObject);
                var dx = closestPos.X - position.X;
                var dy = closestPos.Y - position.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                
                if (distance < threshold)
                    return closestObject;
            }

            return null;
        }

        // On the free map the view zooms around its own centre; the mouse wheel then re-anchors it to the cursor
        private bool ZoomIn(int step = 1)
        {
            _config.DefaultZoom = Math.Max(10, _config.DefaultZoom - step);
            if (_isFreeMapToggled)
                ClampPanPosition();

            _mapCanvas.Invalidate();
            return true;
        }

        private bool ZoomOut(int step = 1)
        {
            _config.DefaultZoom = Math.Min(200, _config.DefaultZoom + step);
            if (_isFreeMapToggled)
                ClampPanPosition();

            _mapCanvas.Invalidate();
            return true;
        }
        #endregion

        #region Event Handlers
        private void chkMapFree_CheckedChanged(object sender, EventArgs e)
        {
            _isFreeMapToggled = chkMapFree.Checked;
            _isDragging = false;
            chkMapFree.Text = _isFreeMapToggled ? "Map Free" : "Map Follow";

            // Either way the view starts from the player, so switching to free never jumps and switching back snaps to them
            var localPlayer = this.LocalPlayer;
            if (localPlayer is not null)
            {
                var localPlayerPos = new Vector3D(
                    localPlayer.Position.X + AbsoluteLocation.X,
                    localPlayer.Position.Y + AbsoluteLocation.Y,
                    localPlayer.Position.Z + AbsoluteLocation.Z
                );
                var localPlayerMapPos = localPlayerPos.ToMapPos(_selectedMap);
                _mapPanPosition.X = localPlayerMapPos.X;
                _mapPanPosition.Y = localPlayerMapPos.Y;
                _mapPanPosition.Height = localPlayerMapPos.Height;
                _mapPanPosition.TechScale = (.01f * _config.TechMarkerScale);
                if (_isFreeMapToggled)
                    ClampPanPosition();
            }

            _mapCanvas.Invalidate();
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
                if (_selectedMap == null || _loadedBitmaps == null || _loadedBitmaps.Length == 0)
                {
                    return;
                }

                var mapParams = GetMapLocation();
                
                if (mapParams == null)
                {
                    return; 
                }
                
                var mouseX = (e.X / mapParams.XScale) + mapParams.Bounds.Left;
                var mouseY = (e.Y / mapParams.YScale) + mapParams.Bounds.Top;

                var worldX = (mouseX - _selectedMap.ConfigFile.X) / _selectedMap.ConfigFile.Scale;
                var worldY = (mouseY - _selectedMap.ConfigFile.Y) / _selectedMap.ConfigFile.Scale;
                var worldPos = new Vector3D(worldX, worldY, GroundHeightAt(worldX, worldY));

                _pointsOfInterest.Add(new PointOfInterest(worldPos, "POI"));
                PublishFireSupportTarget();
                _mapCanvas.Invalidate();
            }
            else if (e.Button == MouseButtons.Right && _hoveredPoi != null)
            {
                _pointsOfInterest.Remove(_hoveredPoi);
                PublishFireSupportTarget();
                _mapCanvas.Invalidate();
            }
        }

        public class PointOfInterest
        {
            public Vector3D Position { get; }
            public string Name { get; }

            public PointOfInterest(Vector3D position, string name)
            {
                Position = position;
                Name = name;
            }
        }
        #endregion
        #endregion

        #region Settings
        #region General
        #region Event Handlers

        private void btnRestartRadar_Click(object sender, EventArgs e)
        {
            if (_espOverlay != null && !_espOverlay.IsDisposed)
            {
                _espOverlay.Close();
                _espOverlay = null;
            }
            Thread.Sleep(1000);
            Memory.Restart();
            Thread.Sleep(1000);
            if (_config.EnableEsp)
            {
                _espOverlay = new EspOverlay();
                _espOverlay.Show();
            }
        }

        private bool DumpNames()
        {
            if (!InGame) return false;

            Memory._game.LogVehicles(force: true);
            return true;
        }

        private void btnDumpNames_Click(object sender, EventArgs e)
        {
            if (!InGame) return;

            Memory._game.LogVehicles(force: true);
        }

        private void btnListVehicles_Click(object sender, EventArgs e)
        {
            if (!InGame) return;

            // Toggle vehicle detection
            bool isEnabled = btnListVehicles.Checked;
            Memory._game?.DebugVehicles?.ToggleVehicleDetection(isEnabled);
        }

        private void ChkShowEnemyDistance_CheckedChanged(object sender, EventArgs e)
        {
            _config.ShowEnemyDistance = chkShowEnemyDistance.Checked;
            UpdateStatusIndicator(lblStatusToggleEnemyDistance, _config.ShowEnemyDistance);
            _mapCanvas.Invalidate();
            Config.SaveConfig(_config);
        }

        private void ChkEnableAimview_CheckedChanged(object sender, EventArgs e)
        {
            _config.EnableAimview = chkEnableAimview.Checked;
            Config.SaveConfig(_config);

            if (_config.EnableAimview)
            {
                if (_aimviewWidget == null)
                {
                    InitializeAimviewWidget();
                }
            }
            else
            {
                // Save aimview widget state before disposing
                SaveAimviewWidgetState();
                if (_aimviewWidget != null)
                {
                    _aimviewWidget.WidgetChanged -= OnAimviewWidgetChanged;
                    _aimviewWidget.Dispose();
                    _aimviewWidget = null;
                }
            }
        }

        private void chkHighAlert_CheckedChanged(object sender, EventArgs e)
        {
            _config.HighAlert = chkHighAlert.Checked;
            Config.SaveConfig(_config);
        }

        private void chkShowObjectives_CheckedChanged(object sender, EventArgs e)
        {
            // LoadConfig() sets Checked during construction, before the map canvas exists, so bail out until it does.
            if (_mapCanvas == null)
                return;

            _config.ShowObjectives = chkShowObjectives.Checked;
            var objectives = Memory._game?.Objectives;
            if (objectives != null) objectives.Enabled = _config.ShowObjectives;
            _mapCanvas.Invalidate();
            Config.SaveConfig(_config);
        }

        private void trkUIScale_Scroll(object sender, EventArgs e)
        {
            _config.UIScale = trkUIScale.Value;
            InitiateUIScaling();
            _mapCanvas.Invalidate();
            Config.SaveConfig(_config);
        }

        private void trkInfoWidgetOpacity_Scroll(object sender, EventArgs e)
        {
            _config.InfoWidgetOpacity = trkInfoWidgetOpacity.Value;
            UpdateOpacityLabels();
            _mapCanvas.Invalidate();
            Config.SaveConfig(_config);
        }

        private void UpdateOpacityLabels()
        {
            lblAimviewOpacity.Text = $"Aimview Opacity: {trkAimviewOpacity.Value}%";
            lblInfoWidgetOpacity.Text = $"Info Widget Opacity: {trkInfoWidgetOpacity.Value}%";
        }

        private void trkAimviewOpacity_Scroll(object sender, EventArgs e)
        {
            _config.AimviewOpacity = trkAimviewOpacity.Value;
            UpdateOpacityLabels();
            _mapCanvas.Invalidate();
            Config.SaveConfig(_config);
        }

        private void trkTechMarkerScale_Scroll(object sender, EventArgs e)
        {
            _config.TechMarkerScale = trkTechMarkerScale.Value;
            InitiateUIScaling();
            _mapCanvas.Invalidate();
            Config.SaveConfig(_config);
        }

        private void ChkDisableSuppression_CheckedChanged(object sender, EventArgs e)
        {
            _config.DisableSuppression = chkDisableSuppression.Checked;
            Config.SaveConfig(_config);
            
            if (InGame)
            {
                Memory._game?.SetSuppression(_config.DisableSuppression);
            }
        }

        private void ChkSetInteractionDistances_CheckedChanged(object sender, EventArgs e)
        {
            _config.SetInteractionDistances = chkSetInteractionDistances.Checked;
            Config.SaveConfig(_config);
            
            if (InGame)
            {
                Memory._game?.SetInteractionDistances(_config.SetInteractionDistances);
            }
        }

        private void ChkAllowShootingInMainBase_CheckedChanged(object sender, EventArgs e)
        {
            _config.AllowShootingInMainBase = chkAllowShootingInMainBase.Checked;
            Config.SaveConfig(_config);
            
            if (InGame)
            {
                Memory._game?.SetShootingInMainBase(_config.AllowShootingInMainBase);
            }
        }

        private void ChkSetTimeDilation_CheckedChanged(object sender, EventArgs e)
        {
            if (!chkSpeedHack.Checked)
            {
                _config.SetSpeedHack = false;
                Config.SaveConfig(_config);
                
                if (InGame)
                {
                    Memory._game?.SetSpeedHack(false);
                    UpdateStatusIndicator(lblStatusSpeedHack, false);
                }
            }
        }

        private void ChkAirStuck_CheckedChanged(object sender, EventArgs e)
        {
            // Update the AirStuckEnabled config property (checkbox state)
            _config.AirStuckEnabled = chkAirStuck.Checked;
            Config.SaveConfig(_config);
            
            // Enable/disable collision checkbox based on AirStuck checkbox
            chkDisableCollision.Enabled = chkAirStuck.Checked;
            
            // If checkbox is unchecked, also disable the active feature
            if (!chkAirStuck.Checked && _config.SetAirStuck)
            {
                _config.SetAirStuck = false;
                Config.SaveConfig(_config);
                
                if (InGame)
                {
                    Memory._game?.SetAirStuck(false);
                    UpdateStatusIndicator(lblStatusAirStuck, false);
                    
                    if (_config.DisableCollision)
                    {
                        _config.DisableCollision = false;
                        chkDisableCollision.Checked = false;
                        Memory._game?.DisableCollision(false);
                    }
                }
            }
        }

        private void ChkDisableCollision_CheckedChanged(object sender, EventArgs e)
        {
            if (chkDisableCollision.Checked && !chkAirStuck.Checked)
            {
                chkDisableCollision.Checked = false;
                return;
            }
            
            _config.DisableCollision = chkDisableCollision.Checked;
            Config.SaveConfig(_config);
            
            if (InGame)
            {
                Memory._game?.DisableCollision(_config.DisableCollision);
            }
        }

        private void ChkQuickZoom_CheckedChanged(object sender, EventArgs e)
        {
            _config.QuickZoom = chkQuickZoom.Checked;
            Config.SaveConfig(_config);
        }

        private void ChkRapidFire_CheckedChanged(object sender, EventArgs e)
        {
            _config.RapidFire = chkRapidFire.Checked;
            Config.SaveConfig(_config);
            
            if (InGame)
            {
                Memory._game?.SetRapidFire(_config.RapidFire);
            }
        }

        private void ChkInfiniteAmmo_CheckedChanged(object sender, EventArgs e)
        {
            _config.InfiniteAmmo = chkInfiniteAmmo.Checked;
            Config.SaveConfig(_config);
            
            if (InGame)
            {
                Memory._game?.SetInfiniteAmmo(_config.InfiniteAmmo);
            }
        }

        private void ChkQuickSwap_CheckedChanged(object sender, EventArgs e)
        {
            _config.QuickSwap = chkQuickSwap.Checked;
            Config.SaveConfig(_config);
            
            if (InGame)
            {
                Memory._game?.SetQuickSwap(_config.QuickSwap);
            }
        }

        // Fire Support sends artillery shells to the newest marker on the map
        private void PublishFireSupportTarget()
        {
            squad_dma.Source.Squad.Features.FireSupport.SetTarget(_pointsOfInterest.Count > 0 ? _pointsOfInterest[^1].Position : null);
        }

        private void ChkFireSupport_CheckedChanged(object sender, EventArgs e)
        {
            _config.FireSupport = chkFireSupport.Checked;
            Config.SaveConfig(_config);

            if (InGame)
            {
                Memory._game?.SetFireSupport(_config.FireSupport);
            }
        }

        private void CmbFireSupportMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            _config.FireSupportMode = cmbFireSupportMode.SelectedIndex == 1
                ? (int)squad_dma.Source.Squad.Features.FireSupportMode.Drop
                : (int)squad_dma.Source.Squad.Features.FireSupportMode.Arc;
            Config.SaveConfig(_config);
        }

        private void TrkFireSupportSpread_Scroll(object sender, EventArgs e)
        {
            _config.FireSupportSpreadMeters = trkFireSupportSpread.Value;
            lblFireSupportSpread.Text = $"Fire Support Spread: {trkFireSupportSpread.Value} m";
            Config.SaveConfig(_config);
        }

        private void ChkQuickAds_CheckedChanged(object sender, EventArgs e)
        {
            _config.QuickAds = chkQuickAds.Checked;
            Config.SaveConfig(_config);

            if (InGame)
            {
                Memory._game?.SetQuickAds(_config.QuickAds);
            }
        }

        private void ChkForceFullAuto_CheckedChanged(object sender, EventArgs e)
        {
            _config.ForceFullAuto = chkForceFullAuto.Checked;
            Config.SaveConfig(_config);
            
            if (InGame)
            {
                Memory._game?.SetForceFullAuto(_config.ForceFullAuto);
            }
        }

        private void ChkNoRecoil_CheckedChanged(object sender, EventArgs e)
        {
            _config.NoRecoil = chkNoRecoil.Checked;
            Config.SaveConfig(_config);
            
            if (InGame && Memory._game != null)
            {
                Memory._game?.SetNoRecoil(_config.NoRecoil);
            }
        }

        private void ChkNoSpread_CheckedChanged(object sender, EventArgs e)
        {
            _config.NoSpread = chkNoSpread.Checked;
            Config.SaveConfig(_config);
            
            if (InGame && Memory._game != null)
            {
                Memory._game?.SetNoSpread(_config.NoSpread);
            }
        }

        private void ChkNoSway_CheckedChanged(object sender, EventArgs e)
        {
            _config.NoSway = chkNoSway.Checked;
            Config.SaveConfig(_config);
            
            if (InGame)
            {
                Memory._game?.SetNoSway(_config.NoSway);
            }
        }

        private void ChkShowDiagnostics_CheckedChanged(object sender, EventArgs e)
        {
            _config.ShowDiagnostics = chkShowDiagnostics.Checked;
            Config.SaveConfig(_config);
            _mapCanvas?.Invalidate();
        }

        private void ChkRenderTweak_CheckedChanged(object sender, EventArgs e)
        {
            _config.RenderNoFoliage = chkNoFoliage.Checked;
            Config.SaveConfig(_config);

            if (InGame)
                Memory._game?.RefreshRenderTweaks();
        }

        private void ChkFreeCamera_CheckedChanged(object sender, EventArgs e)
        {
            _config.FreeCameraEnabled = chkFreeCamera.Checked;

            // Unchecking also stops a camera that is flying
            if (!chkFreeCamera.Checked && _config.SetFreeCamera)
            {
                _config.SetFreeCamera = false;
                if (InGame)
                    Memory._game?.SetFreeCamera(false);
            }

            Config.SaveConfig(_config);
        }

        private void ToggleFreeCamera()
        {
            if (!InGame)
            {
                return;
            }

            _config.SetFreeCamera = !_config.SetFreeCamera;
            Memory._game?.SetFreeCamera(_config.SetFreeCamera);
            Config.SaveConfig(_config);
        }

        private void ChkNoCameraShake_CheckedChanged(object sender, EventArgs e)
        {
            _config.NoCameraShake = chkNoCameraShake.Checked;
            Config.SaveConfig(_config);
            
            if (InGame)
            {
                Memory._game?.SetNoCameraShake(_config.NoCameraShake);
            }
        }

        private void ChkInstantGrenade_CheckedChanged(object sender, EventArgs e)
        {
            _config.InstantGrenade = chkInstantGrenade.Checked;
            Config.SaveConfig(_config);
            
            if (InGame)
            {
                Memory._game?.SetInstantGrenade(_config.InstantGrenade);
            }
        }

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

        private void ChkEnableBones_CheckedChanged(object sender, EventArgs e)
        {
            _config.EspBones = chkEnableBones.Checked;
            Config.SaveConfig(_config);
        }

        private void TrkEspMaxDistance_Scroll(object sender, EventArgs e)
        {
            _config.EspMaxDistance = trkEspMaxDistance.Value;
            lblEspMaxDistance.Text = $"Player Max Distance: {trkEspMaxDistance.Value}m";
            Config.SaveConfig(_config);
        }

        private void TrkEspVehicleMaxDistance_Scroll(object sender, EventArgs e)
        {
            _config.EspVehicleMaxDistance = trkEspVehicleMaxDistance.Value;
            lblEspVehicleMaxDistance.Text = $"Vehicle Max Distance: {trkEspVehicleMaxDistance.Value}m";
            Config.SaveConfig(_config);
        }

        private void ChkEspShowVehicles_CheckedChanged(object sender, EventArgs e)
        {
            _config.EspShowVehicles = chkEspShowVehicles.Checked;
            Config.SaveConfig(_config);
        }

        private void ChkShowAllies_CheckedChanged(object sender, EventArgs e)
        {
            _config.EspShowAllies = chkShowAllies.Checked;
            Config.SaveConfig(_config);
        }

        private void ChkEspShowNames_CheckedChanged(object sender, EventArgs e)
        {
            _config.EspShowNames = chkEspShowNames.Checked;
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


        #endregion
        #endregion
        #endregion

        private void trkAimLength_Scroll(object sender, EventArgs e)
        {
        }

        private void lblAimline_Click(object sender, EventArgs e)
        { 
        }
        #endregion

        #region keybinds
        private void StartKeybindCapture(Button button)
        {
            if (_isWaitingForKey) return;
            _isWaitingForKey = true;
            _currentKeybindButton = button;
            _currentKeybind = Keys.None;
            button.Text = "Press any key...";
        }

        private void EndKeybindCapture(Keys key)
        {
            if (!_isWaitingForKey || _currentKeybindButton == null) return;

            _currentKeybind = key;
            _currentKeybindButton.Text = key == Keys.None ? "None" : key.ToString();
            _isWaitingForKey = false;

            if (_currentKeybindButton == btnKeybindToggleFullscreen)
            {
                _config.KeybindToggleFullscreen = key;
            }
            else if (_currentKeybindButton == btnKeybindToggleMap)
            {
                _config.KeybindToggleMap = key;
            }
            else if (_currentKeybindButton == btnKeybindToggleEnemyDistance)
            {
                _config.KeybindToggleEnemyDistance = key;
            }
            else if (_currentKeybindButton == btnKeybindSpeedHack)
            {
                _config.KeybindSpeedHack = key;
            }
            else if (_currentKeybindButton == btnKeybindFreeCamera)
            {
                _config.KeybindFreeCamera = key;
            }
            else if (_currentKeybindButton == btnKeybindAirStuck)
            {
                _config.KeybindAirStuck = key;
            }
            else if (_currentKeybindButton == btnKeybindQuickZoom)
            {
                _config.KeybindQuickZoom = key;
            }
            else if (_currentKeybindButton == btnKeybindDumpNames)
            {
                _config.KeybindDumpNames = key;
            }
            else if (_currentKeybindButton == btnKeybindZoomIn)
            {
                _config.KeybindZoomIn = key;
            }
            else if (_currentKeybindButton == btnKeybindZoomOut)
            {
                _config.KeybindZoomOut = key;
            }

            Config.SaveConfig(_config);
            _currentKeybindButton = null;
        }

        // Keybind button click handlers
        private void BtnKeybindSpeedHack_Click(object sender, EventArgs e)
        {
            StartKeybindCapture(btnKeybindSpeedHack);
        }

        private void BtnKeybindFreeCamera_Click(object sender, EventArgs e)
        {
            StartKeybindCapture(btnKeybindFreeCamera);
        }

        private void BtnKeybindAirStuck_Click(object sender, EventArgs e)
        {
            StartKeybindCapture(btnKeybindAirStuck);
        }


        private void BtnKeybindToggleEnemyDistance_Click(object sender, EventArgs e)
        {
            StartKeybindCapture(btnKeybindToggleEnemyDistance);
        }

        private void BtnKeybindToggleMap_Click(object sender, EventArgs e)
        {
            StartKeybindCapture(btnKeybindToggleMap);
        }

        private void BtnKeybindToggleFullscreen_Click(object sender, EventArgs e)
        {
            StartKeybindCapture(btnKeybindToggleFullscreen);
        }

        private void BtnKeybindDumpNames_Click(object sender, EventArgs e)
        {
            StartKeybindCapture(btnKeybindDumpNames);
        }
    }
}
#endregion

