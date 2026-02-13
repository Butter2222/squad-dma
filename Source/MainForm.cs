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
        private readonly Stopwatch _fpsWatch = new();
        private readonly object _renderLock = new();
        private readonly object _loadMapBitmapsLock = new();
        private readonly System.Timers.Timer _mapChangeTimer = new(100);
        private readonly List<Map> _maps = new();
        private DarkModeCS _darkmode;
        private readonly Dictionary<UActor, Vector3D> _aaProjectileOrigins = new();
        private readonly List<PointOfInterest> _pointsOfInterest = new();
        private System.Windows.Forms.Timer _panTimer;
        private GameStatus _previousGameStatus = GameStatus.NotFound;
        private EspOverlay _espOverlay;
        private AimviewWidget _aimviewWidget;
        private TInfoWidget _tInfoWidget;

        private bool _isFreeMapToggled;
        private bool _isDragging;
        private float _uiScale = 1.0f;
        private int _fps;
        private int _mapSelectionIndex;
        private int _lastFriendlyTickets;
        private int _lastEnemyTickets;
        private int _lastKills;
        private int _lastWoundeds;


        private Map _selectedMap;
        private SKBitmap[] _loadedBitmaps;
        private MapPosition _mapPanPosition = new();
        private Point _lastMousePosition;
        private PointOfInterest _hoveredPoi;
        private UActor _closestPlayerToMouse;
        private SKPoint _targetPanPosition;

        private const float DRAG_SENSITIVITY = 1.0f;
        private const float VELOCITY_DECAY = 0.92f;
        private const float MAX_VELOCITY = 50.0f;
        private const float PAN_SMOOTHNESS = 0.3f;
        private const int PAN_INTERVAL = 16;

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
        }

        public void RemovePointOfInterest(string name)
        {
            var poi = _pointsOfInterest.FirstOrDefault(p => p.Name == name);
            if (poi != null)
            {
                _pointsOfInterest.Remove(poi);
            }
        }

        public void ClearAllPointsOfInterest()
        {
            _pointsOfInterest.Clear();
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
        
        private void InitializeTInfoWidget()
        {
            _tInfoWidget = new TInfoWidget(_mapCanvas, this, _uiScale);
        }

        private void InitializeTimers()
        {
            _mapChangeTimer.AutoReset = false;
            _mapChangeTimer.Elapsed += MapChangeTimer_Elapsed;

            _panTimer = new System.Windows.Forms.Timer { Interval = PAN_INTERVAL };
            _panTimer.Tick += PanTimer_Tick;

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
            ticketsPanel.Paint += ticketsPanel_Paint;
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
            txtFirstScopeMag.TextChanged += TxtFirstScopeMag_TextChanged;
            txtSecondScopeMag.TextChanged += TxtSecondScopeMag_TextChanged;
            txtThirdScopeMag.TextChanged += TxtThirdScopeMag_TextChanged;
        }

        private void LoadInitialData()
        {
            LoadMaps();
            _fpsWatch.Start();
        }

        private void InitializeColorsTab()
        {
            // Create and add the color configuration panel to the Colors tab
            var colorPanel = CreateColorConfigurationPanel();
            tabColors.Controls.Add(colorPanel);
        }
        #endregion

        #region Overrides
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
            int zoomStep = 3;
            if (e.Delta < 0)
                ZoomOut(zoomStep);
            else if (e.Delta > 0)
                ZoomIn(zoomStep);

            if (_isFreeMapToggled)
            {
                var mousePos = _mapCanvas.PointToClient(Cursor.Position);
                var mapParams = GetMapLocation();
                
                if (mapParams == null)
                {
                    return; 
                }
                
                var mapMousePos = new SKPoint(
                    (float)(mapParams.Bounds.Left + mousePos.X / mapParams.XScale),
                    (float)(mapParams.Bounds.Top + mousePos.Y / mapParams.YScale)
                );

                // Only update target position if zooming out
                if (e.Delta < 0)
                {
                    _targetPanPosition = mapMousePos;
                    if (!_panTimer.Enabled)
                        _panTimer.Start();
                }
            }
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

        private void HandleMapDragging(MouseEventArgs e)
        {
            if (!_lastMousePosition.IsEmpty)
            {
                float dx = (e.X - _lastMousePosition.X) * DRAG_SENSITIVITY;
                float dy = (e.Y - _lastMousePosition.Y) * DRAG_SENSITIVITY;
                
                float zoomScale = 1.0f / (_config.DefaultZoom * 0.01f);
                
                _targetPanPosition.X -= dx * zoomScale;
                _targetPanPosition.Y -= dy * zoomScale;
                
                // Update position immediately for direct response
                _mapPanPosition.X = (float)_targetPanPosition.X;
                _mapPanPosition.Y = (float)_targetPanPosition.Y;
                
                _mapCanvas.Invalidate();
            }
            
            _lastMousePosition = e.Location;
        }

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
            if (e.Button == MouseButtons.Left && _isFreeMapToggled)
            {
                _isDragging = true;
                _lastMousePosition = e.Location;
                _panTimer.Stop();
            }

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
                // Use flat ground assumption - set POI to same height as local player
                var worldZ = this.LocalPlayer.Position.Z;

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
                _panTimer.Start();
            }
        }

        private void skMapCanvas_PaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
        {
            try
            {
                var canvas = e.Surface.Canvas;
                canvas.Clear();
                UpdateWindowTitle();

                if (IsReadyToRender())
                {
                    lock (_renderLock)
                    {
                        var deadMarkers = new List<SKPoint>();
                        var projectileAAs = new List<UActor>();

                        DrawMap(canvas);
                        DrawActors(canvas, deadMarkers, projectileAAs);
                        DrawPOIs(canvas);
                        DrawToolTips(canvas);
                        DrawTopMost(canvas, deadMarkers, projectileAAs);
                        
                        // Draw aimview widget
                        _aimviewWidget?.Draw(canvas);
                        
                        // Draw TInfo widget
                        _tInfoWidget?.Draw(canvas);
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
            if (Memory.GameStatus != GameStatus.InGame || Memory._game == null)
            {
                // Reset when not in game
                _lastFriendlyTickets = 0;
                _lastEnemyTickets = 0;
                _lastKills = 0;
                _lastWoundeds = 0;
                return;
            }

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            string displayText = $"Friendly: {_lastFriendlyTickets}  |  Enemy: {_lastEnemyTickets}  |  K: {_lastKills}  |  W: {_lastWoundeds}";

            using (var font = new Font("Arial", 9f, FontStyle.Bold))
            using (var format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;

                RectangleF rect = new RectangleF(
                    0,
                    0,
                    ticketsPanel.Width,
                    ticketsPanel.Height
                );

                g.DrawString(
                    displayText,
                    font,
                    Brushes.WhiteSmoke,
                    rect,
                    format
                );
            }
        }

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
            MapPosition.RefreshTechMarkerPaints();
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

            // Click handlers for color selection
            Action openColorPicker = () => OpenColorPicker(colorName, color, (newColor) =>
            {
                onColorChanged(newColor);
                colorBox.BackColor = newColor.ToDrawingColor();
                hexLabel.Text = ColorToHex(newColor.ToDrawingColor());
                
                // Refresh tech marker paints to apply new colors immediately
                MapPosition.RefreshTechMarkerPaints();
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

            var configs = dir.GetFiles("*.json");
            if (configs.Length == 0)
            {
                Program.Log("No .json map configs found in Maps directory.");
                return;
            }

            foreach (var config in configs)
            {
                try
                {
                    var name = Path.GetFileNameWithoutExtension(config.Name);
                    var mapConfig = MapConfig.LoadFromFile(config.FullName);
                    var map = new Map(name.ToUpper(), mapConfig, config.FullName);
                    map.ConfigFile.MapLayers = map.ConfigFile.MapLayers.OrderBy(x => x.MinHeight).ToList();
                    _maps.Add(map);
                }
                catch (Exception ex)
                {
                    Program.Log($"Error loading map config {config.Name}: {ex.Message}");
                }
            }

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
            txtFirstScopeMag.Text = _config.FirstScopeMagnification.ToString("F1");
            txtSecondScopeMag.Text = _config.SecondScopeMagnification.ToString("F1");
            txtThirdScopeMag.Text = _config.ThirdScopeMagnification.ToString("F1");
            trkTechMarkerScale.Value = _config.TechMarkerScale;
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
            chkNoRecoil.Checked = _config.NoRecoil;
            chkNoSpread.Checked = _config.NoSpread;
            chkNoSway.Checked = _config.NoSway;
            chkInstantGrenade.Checked = _config.InstantGrenade;
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
                ticketsPanel.Invalidate();
                
                Task.Run(async () => await ApplyFeaturesAsync());
            }
            else if (currentGameStatus == GameStatus.Menu && _previousGameStatus == GameStatus.InGame)
            {
                _lastFriendlyTickets = 0;
                _lastEnemyTickets = 0;
                _lastKills = 0;
                _lastWoundeds = 0;
                ticketsPanel.Invalidate();
                
                ClearPointsOfInterest();
            }
            else if (currentGameStatus == GameStatus.NotFound)
            {
                _lastFriendlyTickets = 0;
                _lastEnemyTickets = 0;
                _lastKills = 0;
                _lastWoundeds = 0;
                ticketsPanel.Invalidate();
            }
            
            _previousGameStatus = currentGameStatus;
        }

        private async Task ApplyFeaturesAsync()
        {
            const int retryDelay = 250;

            while (true)
            {
                if (Memory._game != null && Memory._game.InGame)
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
                ticketsPanel.Invalidate();
            }
        }




        private void UpdateSelectedMap()
        {
            string currentMap = MapName;
            if (_selectedMap is null || !_selectedMap.ConfigFile.MapID.Any(id => id.Equals(currentMap, StringComparison.OrdinalIgnoreCase)))
            {
                // First try exact match
                var selectedMap = _maps.FirstOrDefault(x => x.ConfigFile.MapID.Any(id => id.Equals(currentMap, StringComparison.OrdinalIgnoreCase)));
                
                // If no exact match found, try partial matching (contains)
                if (selectedMap is null)
                {
                    selectedMap = _maps.FirstOrDefault(x => x.ConfigFile.MapID.Any(id => 
                        id.Contains(currentMap, StringComparison.OrdinalIgnoreCase) || 
                        currentMap.Contains(id, StringComparison.OrdinalIgnoreCase)));
                }
                
                if (selectedMap is not null)
                {
                    _selectedMap = selectedMap;
                    CleanupLoadedBitmaps();
                    ClearPointsOfInterest();
                    LoadMapBitmaps();
                }
                else
                {
                    Logger.Error($"Map Error: Current map '{currentMap}' is not configured. Please add this map name to the corresponding map configuration file.");
                    LogUnmappedMapName(currentMap);
                }
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
                        using var stream = File.Open(mapLayer.Filename, FileMode.Open, FileAccess.Read);
                        _loadedBitmaps[mapLayers.IndexOf(mapLayer)] = SKBitmap.Decode(stream);
                        _loadedBitmaps[mapLayers.IndexOf(mapLayer)].SetImmutable();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading map layer: {ex.Message}");
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

        private void DrawMap(SKCanvas canvas)
        {
            if (grpMapSetup.Visible)
            {
                var localPlayerPos = new Vector3D(
                    LocalPlayer.Position.X + AbsoluteLocation.X,
                    LocalPlayer.Position.Y + AbsoluteLocation.Y,
                    LocalPlayer.Position.Z + AbsoluteLocation.Z
                );
                grpMapSetup.Text = $"Map Setup - X,Y,Z: {localPlayerPos.X}, {localPlayerPos.Y}, {localPlayerPos.Z}";
            }
            else if (grpMapSetup.Text != "Map Setup" && !grpMapSetup.Visible)
            {
                grpMapSetup.Text = "Map Setup";
            }

            var mapParams = GetMapLocation();
            
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

            canvas.DrawBitmap(
                _loadedBitmaps[mapParams.MapLayerIndex],
                mapParams.Bounds,
                mapCanvasBounds,
                SKPaints.PaintBitmap
            );
        }

        private void DrawActors(SKCanvas canvas, List<SKPoint> deadMarkers, List<UActor> projectileAAs)
        {
            if (!InGame || LocalPlayer is null)
                return;

            var allPlayers = AllActors?.Select(x => x.Value);
            if (allPlayers is null)
                return;

            var activeProjectiles = allPlayers.Where(a => a.ActorType == ActorType.ProjectileAA).ToList();
            projectileAAs.AddRange(activeProjectiles);

            var localPlayerMapPos = LocalPlayer.Position.ToMapPos(_selectedMap);
            var mapParams = GetMapLocation();
            
            if (mapParams == null)
            {
                return;
            }
            
            var localPlayerZoomedPos = localPlayerMapPos.ToZoomedPos(mapParams);

            localPlayerZoomedPos.DrawPlayerMarker(canvas, LocalPlayer, trkAimLength.Value);
            
            // Draw max range circle around player if ballistic weapon is equipped
            if (Memory._game?._soldierManager?.WeaponDetector != null)
            {
                var mortarCalculator = Memory._game._soldierManager.WeaponDetector;
                if (mortarCalculator.HasBallisticWeapon && mortarCalculator.CurrentWeaponData != null)
                {
                    // Calculate max range using proper ballistic formula
                    float maxRangeMeters = mortarCalculator.CalculateMaxDistance(mortarCalculator.CurrentWeaponData);
                    float maxRangeMapUnits = maxRangeMeters * 100; // Convert to game units
                    float maxRangePixels = (float)(maxRangeMapUnits * _selectedMap.ConfigFile.Scale * mapParams.XScale);

                    var playerCenter = localPlayerZoomedPos.GetPoint();
                    
                    // Draw max range circle
                    using var maxRangePaint = new SKPaint
                    {
                        Color = SKColors.Black, // Black color
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 2 * _uiScale, // Original thinner line width
                        IsAntialias = true
                    };

                    canvas.DrawCircle(playerCenter.X, playerCenter.Y, maxRangePixels, maxRangePaint);
                }
            }

            foreach (var actor in allPlayers)
            {
                if (actor.ActorType == ActorType.Projectile || actor.ActorType == ActorType.ProjectileAA || actor.ActorType == ActorType.ProjectileSmall)
                    continue;

                var actorPos = new Vector3D(
                    actor.Position.X + AbsoluteLocation.X,
                    actor.Position.Y + AbsoluteLocation.Y,
                    actor.Position.Z + AbsoluteLocation.Z
                );
                if (Math.Abs(actorPos.X - AbsoluteLocation.X) + Math.Abs(actorPos.Y - AbsoluteLocation.Y) + Math.Abs(actorPos.Z - AbsoluteLocation.Z) < 1.0)
                    continue;

                var actorMapPos = actorPos.ToMapPos(_selectedMap);
                var actorZoomedPos = actorMapPos.ToZoomedPos(mapParams);
                actor.ZoomedPosition = new Vector2D(actorZoomedPos.X, actorZoomedPos.Y);

                if (actor.ActorType == ActorType.Player && !actor.IsAlive)
                {
                    HandleDeadPlayer(canvas, actor, deadMarkers, mapParams);
                    continue;
                }

                int aimlineLength = actor == LocalPlayer ? 0 : 15;
                DrawActor(canvas, actor, actorZoomedPos, aimlineLength, localPlayerMapPos);
            }

            foreach (var actor in allPlayers)
            {
                if (actor.ActorType != ActorType.Projectile && actor.ActorType != ActorType.ProjectileSmall)
                    continue;

                var actorPos = new Vector3D(actor.Position.X + AbsoluteLocation.X, actor.Position.Y + AbsoluteLocation.Y, actor.Position.Z + AbsoluteLocation.Z);
                if (Math.Abs(actorPos.X - AbsoluteLocation.X) + Math.Abs(actorPos.Y - AbsoluteLocation.Y) + Math.Abs(actorPos.Z - AbsoluteLocation.Z) < 1.0)
                    continue;

                var actorMapPos = actorPos.ToMapPos(_selectedMap);
                var actorZoomedPos = actorMapPos.ToZoomedPos(mapParams);
                actorZoomedPos.DrawProjectile(canvas, actor);
            }
        }

        private void HandleDeadPlayer(SKCanvas canvas, UActor actor, List<SKPoint> deadMarkers, MapParameters mapParams)
        {
            if (actor.DeathPosition.X != 0 || actor.DeathPosition.Y != 0 || actor.DeathPosition.Z != 0)
            {
                var timeSinceDeath = DateTime.Now - actor.TimeOfDeath;
                if (timeSinceDeath.TotalSeconds <= 8)
                {
                    var deathPosAdjusted = new Vector3D(
                        actor.DeathPosition.X + AbsoluteLocation.X,
                        actor.DeathPosition.Y + AbsoluteLocation.Y,
                        actor.DeathPosition.Z + AbsoluteLocation.Z
                    );
                    var deathPosMap = deathPosAdjusted.ToMapPos(_selectedMap);
                    var deathZoomedPos = deathPosMap.ToZoomedPos(mapParams);
                    deadMarkers.Add(deathZoomedPos.GetPoint());
                }
                else
                {
                    actor.DeathPosition = Vector3D.Zero;
                    actor.TimeOfDeath = DateTime.MinValue;
                }
            }
        }

        private void DrawActor(SKCanvas canvas, UActor actor, MapPosition actorZoomedPos, int aimlineLength, MapPosition localPlayerMapPos)
        {
            if (this.InGame && this.LocalPlayer is not null)
            {
                var height = actorZoomedPos.Height - localPlayerMapPos.Height;

                if (actor.ActorType == ActorType.Player)
                {
                    var color = actor.IsInMySquad() ? SKPaints.Squad : actor.GetActorPaint().Color;
                    
                    if (actor != LocalPlayer && _config.HighAlert && this.InGame && actor.IsLookingAtPlayer(LocalPlayer))
                        aimlineLength = 9999;
                    
                    actorZoomedPos.DrawPlayerMarker(canvas, actor, aimlineLength, color);

                    if (!actor.IsFriendly() && _config.ShowEnemyDistance)
                    {
                        var dx = LocalPlayer.Position.X - actor.Position.X;
                        var dy = LocalPlayer.Position.Y - actor.Position.Y;
                        var dz = LocalPlayer.Position.Z - actor.Position.Z;
                        var dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        if (dist > 50 * 100)
                        {
                            string distanceText = $"{(int)Math.Round(dist / 100)}m";
                            actorZoomedPos.DrawPlayerDistance(canvas, actor, distanceText);
                        }
                    }
                }
                else if (actor.ActorType == ActorType.Projectile)
                {
                    actorZoomedPos.DrawProjectile(canvas, actor);
                }
                else if (actor.ActorType == ActorType.ProjectileSmall)
                {
                    actorZoomedPos.DrawProjectile(canvas, actor);
                }
                else if (actor.ActorType == ActorType.ProjectileAA)
                {
                    if (!_aaProjectileOrigins.ContainsKey(actor))
                        _aaProjectileOrigins[actor] = new Vector3D(
                            actor.Position.X + AbsoluteLocation.X,
                            actor.Position.Y + AbsoluteLocation.Y,
                            actor.Position.Z + AbsoluteLocation.Z
                        );

                    actorZoomedPos.DrawProjectileAA(canvas, actor);
                }
                else if (actor.ActorType == ActorType.Admin)
                {
                    DrawAdmin(canvas, actor, actorZoomedPos);
                }
                else if (Names.Vehicles.Contains(actor.ActorType))
                {
                    if (!actor.IsFriendly())
                    {
                        var dx = LocalPlayer.Position.X - actor.Position.X;
                        var dy = LocalPlayer.Position.Y - actor.Position.Y;
                        var dz = LocalPlayer.Position.Z - actor.Position.Z;
                        var dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        if (dist > 50 * 100)
                        {
                            string distanceText = $"{(int)Math.Round(dist / 100)}m";
                            actorZoomedPos.DrawVehicleDistance(canvas, actor, distanceText);
                        }
                    }
                    actorZoomedPos.DrawTechMarker(canvas, actor);
                }
                else
                {
                    actorZoomedPos.DrawTechMarker(canvas, actor);
                }
            }
        }

        private void DrawAdmin(SKCanvas canvas, UActor admin, MapPosition position)
        {
            int adminAimlineLength = 9999;

            position.DrawPlayerMarker(canvas, admin, adminAimlineLength, SKPaints.AdminMarker);

            string adminText = "ADMIN";
            float textSize = 12 * _uiScale;
            float textOffset = 15 * _uiScale;

            using (var textFill = new SKPaint
            {
                Color = SKPaints.AdminMarker,
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

                float textX = (float)(position.X - (textBounds.Width / 2));
                float textY = (float)(position.Y + textOffset + (textBounds.Height / 2));

                canvas.DrawText(adminText, textX, textY, textOutline);
                canvas.DrawText(adminText, textX, textY, textFill);
            }
        }

        private void DrawTopMost(SKCanvas canvas, List<SKPoint> deadMarkers, List<UActor> projectileAAs)
        {
            foreach (var pos in deadMarkers)
            {
                DrawDead(canvas, pos, SKColors.Black, SKPaints.DeadMarker, 5 * _uiScale);
            }

            foreach (var projectile in projectileAAs)
            {
                var actorPos = new Vector3D(
                    projectile.Position.X + AbsoluteLocation.X,
                    projectile.Position.Y + AbsoluteLocation.Y,
                    projectile.Position.Z + AbsoluteLocation.Z
                );
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
                Color = SKColors.Yellow,
                StrokeWidth = thickness,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round
            })
            {
                canvas.DrawLine(
                    (float)(startPos.X - size), (float)(startPos.Y - size),
                    (float)(startPos.X + size), (float)(startPos.Y + size),
                    outlinePaint
                );
                canvas.DrawLine(
                    (float)(startPos.X + size), (float)(startPos.Y - size),
                    (float)(startPos.X - size), (float)(startPos.Y + size),
                    outlinePaint
                );

                canvas.DrawLine(
                    (float)(startPos.X - size), (float)(startPos.Y - size),
                    (float)(startPos.X + size), (float)(startPos.Y + size),
                    xPaint
                );
                canvas.DrawLine(
                    (float)(startPos.X + size), (float)(startPos.Y - size),
                    (float)(startPos.X - size), (float)(startPos.Y + size),
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
                Color = SKColors.Yellow,
                TextSize = textSize,
                IsAntialias = true,
                TextAlign = SKTextAlign.Left,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            })
            {
                SKRect textBounds = new SKRect();
                textPaint.MeasureText(text, ref textBounds);

                float textX = (float)(startPos.X + textOffset);
                float textY = (float)(startPos.Y + (textBounds.Height / 2));

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
            var localPlayerPos = new Vector3D(
                LocalPlayer.Position.X + AbsoluteLocation.X,
                LocalPlayer.Position.Y + AbsoluteLocation.Y,
                LocalPlayer.Position.Z + AbsoluteLocation.Z
            );

            using var crosshairPaint = new SKPaint
            {
                Color = SKColors.Red,
                StrokeWidth = 1.5f,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            for (int i = 0; i < _pointsOfInterest.Count; i++)
            {
                var poi = _pointsOfInterest[i];
                var poiRenderPos = new Vector3D(poi.Position.X + AbsoluteLocation.X, poi.Position.Y + AbsoluteLocation.Y, poi.Position.Z + AbsoluteLocation.Z);
                var poiMapPos = poiRenderPos.ToMapPos(_selectedMap);
                var poiZoomedPos = poiMapPos.ToZoomedPos(mapParams);

                var center = poiZoomedPos.GetPoint();
                float crossSize = 3; // Fixed small size for precise center marking

                // Draw damage radius circle if ballistic weapon is equipped
                if (Memory._game?._soldierManager?.WeaponDetector != null)
                {
                    var mortarCalculator = Memory._game._soldierManager.WeaponDetector;
                    if (mortarCalculator.HasBallisticWeapon)
                    {
                        // Use flat ground assumption - both positions at same height
                        var playerPos = new Vector3((float)LocalPlayer.Position.X, (float)LocalPlayer.Position.Y, 0f);
                        var targetPos = new Vector3((float)poi.Position.X, (float)poi.Position.Y, 0f);
                        
                        var solution = mortarCalculator.CalculateTrajectory(targetPos, playerPos);
                        if (solution != null && solution.SpreadEllipse != null)
                        {
                            // Calculate elliptical spread dimensions in pixels
                            float horizontalSpreadMeters = solution.SpreadEllipse.Horizontal;
                            float verticalSpreadMeters = solution.SpreadEllipse.Vertical;
                            
                            float horizontalSpreadMapUnits = horizontalSpreadMeters * 100; // Convert to game units
                            float verticalSpreadMapUnits = verticalSpreadMeters * 100; // Convert to game units
                            
                            float horizontalSpreadPixels = (float)(horizontalSpreadMapUnits * _selectedMap.ConfigFile.Scale * mapParams.XScale);
                            float verticalSpreadPixels = (float)(verticalSpreadMapUnits * _selectedMap.ConfigFile.Scale * mapParams.YScale);

                            // Draw elliptical spread (NOT a perfect circle!)
                            using var spreadEllipsePaint = new SKPaint
                            {
                                Color = new SKColor(255, 0, 0, 60), // Transparent red fill
                                Style = SKPaintStyle.Fill,
                                IsAntialias = true
                            };
                            
                            using var spreadEllipseStrokePaint = new SKPaint
                            {
                                Color = new SKColor(255, 0, 0, 180), // Red outline
                                Style = SKPaintStyle.Stroke,
                                StrokeWidth = 2 * _uiScale,
                IsAntialias = true
            };

                            // Create ellipse path with proper orientation relative to local player
                            using var ellipsePath = new SKPath();
                            
                            // Calculate the bearing from local player to target
                            var localPlayerWorldPos = new Vector3D(
                                LocalPlayer.Position.X + AbsoluteLocation.X,
                                LocalPlayer.Position.Y + AbsoluteLocation.Y,
                                LocalPlayer.Position.Z + AbsoluteLocation.Z
                            );
                            var poiWorldPos = new Vector3D(
                                poi.Position.X + AbsoluteLocation.X,
                                poi.Position.Y + AbsoluteLocation.Y,
                                poi.Position.Z + AbsoluteLocation.Z
                            );
                            
                            // Calculate bearing angle in radians (FROM local player TO target)
                            // This is the direction the projectiles are traveling
                            double deltaX = poiWorldPos.X - localPlayerWorldPos.X;
                            double deltaY = poiWorldPos.Y - localPlayerWorldPos.Y;
                            double bearingRadians = Math.Atan2(deltaY, deltaX);
                            
                            // Determine spread orientation based on weapon type
                            string currentWeaponName = mortarCalculator.CurrentWeaponName?.ToLower() ?? "";
                            
                            // For rocket barrages (UB-32, Tech.UB-32), the spread should be perpendicular to the bearing
                            // This creates a linear spread aligned horizontally relative to the player
                            if (currentWeaponName.Contains("ub32") || currentWeaponName.Contains("ub-32"))
                            {
                                // Add 90 degrees to make the ellipse perpendicular to the firing direction
                                bearingRadians += Math.PI / 2;
                            }
                            // For mortars, the spread should point toward the player (current behavior)
                            // No rotation needed for mortars
                            
                            // Create ellipse rectangle (centered on POI)
                            // Create a standard horizontal ellipse first
                            var ellipseRect = new SKRect(
                                center.X - horizontalSpreadPixels / 2,  // Width = horizontal spread
                                center.Y - verticalSpreadPixels / 2,   // Height = vertical spread
                                center.X + horizontalSpreadPixels / 2,
                                center.Y + verticalSpreadPixels / 2
                            );
                            
                            // Add ellipse to path
                            ellipsePath.AddOval(ellipseRect);
                            
                            // Save the current canvas state
                            canvas.Save();
                            
                            // Translate to the center of the ellipse
                            canvas.Translate(center.X, center.Y);
                            
                            // Rotate the canvas by the bearing angle
                            // This aligns the ellipse so vertical spread points toward local player
                            canvas.RotateDegrees((float)(bearingRadians * 180.0 / Math.PI));
                            
                            // Translate back to draw the ellipse at the correct position
                            canvas.Translate(-center.X, -center.Y);
                            
                            // Draw the rotated elliptical spread
                            canvas.DrawPath(ellipsePath, spreadEllipsePaint);
                            canvas.DrawPath(ellipsePath, spreadEllipseStrokePaint);
                            
                            // Restore the canvas state
                            canvas.Restore();
                        }
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
                DrawPOINumber(canvas, center, i + 1, crossSize);
            }
            
            // Draw minimum distance circle if ballistic weapon is equipped and has minimum distance
            if (Memory._game?._soldierManager?.WeaponDetector != null)
            {
                var mortarCalculator = Memory._game._soldierManager.WeaponDetector;
                
                if (mortarCalculator.HasBallisticWeapon && 
                    mortarCalculator.CurrentWeaponData != null && 
                    mortarCalculator.CurrentWeaponData.MinDistance > 0)
                {
                    float minDistanceMeters = mortarCalculator.CurrentWeaponData.MinDistance;
                    float minDistanceMapUnits = minDistanceMeters * 100; // Convert to game units
                    float minDistancePixels = (float)(minDistanceMapUnits * _selectedMap.ConfigFile.Scale * mapParams.XScale);
                    
                    // Create minimum distance circle paints
                    using var minDistanceFillPaint = new SKPaint
                    {
                        Color = new SKColor(64, 64, 64, 77), // Dark grey with 30% opacity (77/255 ≈ 30%)
                        Style = SKPaintStyle.Fill,
                        IsAntialias = true
                    };
                    
                    using var minDistanceStrokePaint = new SKPaint
                    {
                        Color = new SKColor(0, 0, 0, 255), // Black outline
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 2 * _uiScale,
                        IsAntialias = true
                    };
                    
                    // Draw minimum distance circle centered on local player
                    var localPlayerWorldPos = new Vector3D(
                        LocalPlayer.Position.X + AbsoluteLocation.X,
                        LocalPlayer.Position.Y + AbsoluteLocation.Y,
                        LocalPlayer.Position.Z + AbsoluteLocation.Z
                    );
                    var localPlayerMapPos = localPlayerWorldPos.ToMapPos(_selectedMap).ToZoomedPos(mapParams).GetPoint();
                    canvas.DrawCircle(localPlayerMapPos.X, localPlayerMapPos.Y, minDistancePixels, minDistanceFillPaint);
                    canvas.DrawCircle(localPlayerMapPos.X, localPlayerMapPos.Y, minDistancePixels, minDistanceStrokePaint);
                }
            }
        }

        private void DrawPOINumber(SKCanvas canvas, SKPoint center, int markerNumber, float crosshairSize)
        {
            // Calculate position - top right of damage radius circle
            float numberSize = 10 * _uiScale;
            float offsetX = 15 * _uiScale; // Distance from center
            float offsetY = -15 * _uiScale; // Above center
            
            var numberPosition = new SKPoint(center.X + offsetX, center.Y + offsetY);
            
            // Use same styling as player/vehicle distance text
            using var outlinePaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = numberSize,
                TextAlign = SKTextAlign.Center,
                Typeface = CustomFonts.SKFontFamilyRegular,
                IsAntialias = true,
                SubpixelText = true,
                FilterQuality = SKFilterQuality.High,
                StrokeWidth = 2.8f * _uiScale,
                Style = SKPaintStyle.Stroke
            };
            
            using var textPaint = new SKPaint
            {
                Color = SKColors.White,
                TextSize = numberSize,
                TextAlign = SKTextAlign.Center,
                Typeface = CustomFonts.SKFontFamilyRegular,
                IsAntialias = true,
                SubpixelText = true,
                FilterQuality = SKFilterQuality.High,
                Style = SKPaintStyle.Fill
            };
            
            // Draw number text with black outline and white fill
            float textY = numberPosition.Y + (textPaint.FontMetrics.Descent - textPaint.FontMetrics.Ascent) / 2;
            canvas.DrawText(markerNumber.ToString(), numberPosition.X, textY, outlinePaint);
            canvas.DrawText(markerNumber.ToString(), numberPosition.X, textY, textPaint);
        }
        

        private void DrawToolTips(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            var mapParams = GetMapLocation();

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

        private void PanTimer_Tick(object sender, EventArgs e)
        {
            if (!_isDragging)
            {
                float dx = (float)(_targetPanPosition.X - _mapPanPosition.X);
                float dy = (float)(_targetPanPosition.Y - _mapPanPosition.Y);
                
                if (Math.Abs(dx) < 0.1f && Math.Abs(dy) < 0.1f)
                {
                    _panTimer.Stop();
                    return;
                }
                
                _mapPanPosition.X += (float)(dx * PAN_SMOOTHNESS);
                _mapPanPosition.Y += (float)(dy * PAN_SMOOTHNESS);
                _mapCanvas.Invalidate();
            }
        }

        private bool ZoomIn(int step = 1)
        {
            float oldZoom = _config.DefaultZoom;
            _config.DefaultZoom = Math.Max(10, _config.DefaultZoom - step);
            
            if (_isFreeMapToggled)
            {
                float zoomFactor = oldZoom / _config.DefaultZoom;
                _mapPanPosition.X = (float)(_targetPanPosition.X - (_targetPanPosition.X - _mapPanPosition.X) * zoomFactor);
                _mapPanPosition.Y = (float)(_targetPanPosition.Y - (_targetPanPosition.Y - _mapPanPosition.Y) * zoomFactor);
            }
            
            _mapCanvas.Invalidate();
            return true;
        }

        private bool ZoomOut(int step = 1)
        {
            float oldZoom = _config.DefaultZoom;
            _config.DefaultZoom = Math.Min(200, _config.DefaultZoom + step);
            
            if (_isFreeMapToggled)
            {
                float zoomFactor = oldZoom / _config.DefaultZoom;
                _mapPanPosition.X = (float)(_targetPanPosition.X - (_targetPanPosition.X - _mapPanPosition.X) * zoomFactor);
                _mapPanPosition.Y = (float)(_targetPanPosition.Y - (_targetPanPosition.Y - _mapPanPosition.Y) * zoomFactor);
            }
            
            _mapCanvas.Invalidate();
            return true;
        }
        #endregion

        #region Event Handlers
        private void chkMapFree_CheckedChanged(object sender, EventArgs e)
        {
            _isFreeMapToggled = chkMapFree.Checked;
            
            if (_isFreeMapToggled)
            {
                chkMapFree.Text = "Map Free";
                
                var localPlayer = this.LocalPlayer;
                if (localPlayer is not null)
                {
                    var localPlayerPos = new Vector3D(
                        localPlayer.Position.X + AbsoluteLocation.X,
                        localPlayer.Position.Y + AbsoluteLocation.Y,
                        localPlayer.Position.Z + AbsoluteLocation.Z
                    );
                    var localPlayerMapPos = localPlayerPos.ToMapPos(_selectedMap);
                    _targetPanPosition = new SKPoint((float)localPlayerMapPos.X, (float)localPlayerMapPos.Y);
                    _mapPanPosition.X = localPlayerMapPos.X;
                    _mapPanPosition.Y = localPlayerMapPos.Y;
                    _mapPanPosition.Height = localPlayerMapPos.Height;
                    _mapPanPosition.TechScale = (.01f * _config.TechMarkerScale);
                    
                    if (_panTimer.Enabled)
                        _panTimer.Stop();
                }
            }
            else
            {
                chkMapFree.Text = "Map Follow";
                
                var localPlayer = this.LocalPlayer;
                if (localPlayer is not null)
                {
                    var localPlayerPos = new Vector3D(
                        localPlayer.Position.X + AbsoluteLocation.X,
                        localPlayer.Position.Y + AbsoluteLocation.Y,
                        localPlayer.Position.Z + AbsoluteLocation.Z
                    );
                    var localPlayerMapPos = localPlayerPos.ToMapPos(_selectedMap);
                    _targetPanPosition = new SKPoint((float)localPlayerMapPos.X, (float)localPlayerMapPos.Y);
                    _mapPanPosition.X = localPlayerMapPos.X;
                    _mapPanPosition.Y = localPlayerMapPos.Y;
                    _mapPanPosition.Height = localPlayerMapPos.Height;
                    _mapPanPosition.TechScale = (.01f * _config.TechMarkerScale);
                    
                    if (_panTimer.Enabled)
                        _panTimer.Stop();
                }
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
                var worldPos = new Vector3D(worldX, worldY, LocalPlayer.Position.Z);

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

            Memory._game.ListVehicles();
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

        private void trkUIScale_Scroll(object sender, EventArgs e)
        {
            _config.UIScale = trkUIScale.Value;
            InitiateUIScaling();
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


        private void TxtFirstScopeMag_TextChanged(object sender, EventArgs e)
        {
            if (float.TryParse(txtFirstScopeMag.Text, out float mag) && mag >= 0)
            {
                _config.FirstScopeMagnification = mag;
                Config.SaveConfig(_config);
            }
            else
            {
                txtFirstScopeMag.Text = _config.FirstScopeMagnification.ToString("F1");
            }
        }

        private void TxtSecondScopeMag_TextChanged(object sender, EventArgs e)
        {
            if (float.TryParse(txtSecondScopeMag.Text, out float mag) && mag >= 0)
            {
                _config.SecondScopeMagnification = mag;
                Config.SaveConfig(_config);
            }
            else
            {
                txtSecondScopeMag.Text = _config.SecondScopeMagnification.ToString("F1");
            }
        }

        private void TxtThirdScopeMag_TextChanged(object sender, EventArgs e)
        {
            if (float.TryParse(txtThirdScopeMag.Text, out float mag) && mag >= 0)
            {
                _config.ThirdScopeMagnification = mag;
                Config.SaveConfig(_config);
            }
            else
            {
                txtThirdScopeMag.Text = _config.ThirdScopeMagnification.ToString("F1");
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

