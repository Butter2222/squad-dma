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

        private const float DRAG_SENSITIVITY = 3.5f;
        private const double PAN_SMOOTHNESS = 0.1;
        private const int PAN_INTERVAL = 10;

        private bool _isWaitingForKey = false;
        private Button _currentKeybindButton = null;
        private Keys _currentKeybind = Keys.None;
        private bool _isHolding_QuickZoom = false;
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
            InitializeDarkMode();
            InitializeFormSettings();
            InitializeMapCanvas();
            InitializeTimers();
            InitializeEventHandlers();
            LoadInitialData();
            InitializeKeybinds();
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
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;

            // Add Local Soldier feature event handlers
            chkDisableSuppression.CheckedChanged += ChkDisableSuppression_CheckedChanged;
            chkSetInteractionDistances.CheckedChanged += ChkSetInteractionDistances_CheckedChanged;
            chkAllowShootingInMainBase.CheckedChanged += ChkAllowShootingInMainBase_CheckedChanged;
            chkSpeedHack.CheckedChanged += ChkSetTimeDilation_CheckedChanged;
            chkAirStuck.CheckedChanged += ChkAirStuck_CheckedChanged;
            chkDisableCollision.CheckedChanged += ChkDisableCollision_CheckedChanged;
            chkHideActor.CheckedChanged += ChkHideActor_CheckedChanged;
            chkQuickZoom.CheckedChanged += ChkQuickZoom_CheckedChanged;
            chkRapidFire.CheckedChanged += ChkRapidFire_CheckedChanged;
            chkShowEnemyDistance.CheckedChanged += ChkShowEnemyDistance_CheckedChanged;
            chkInfiniteAmmo.CheckedChanged += ChkInfiniteAmmo_CheckedChanged;
            chkQuickSwap.CheckedChanged += ChkQuickSwap_CheckedChanged;
        }

        private void LoadInitialData()
        {
            LoadConfig();
            LoadMaps();
            _fpsWatch.Start();
        }
        #endregion

        #region Overrides
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true; // Cancel shutdown
            this.Enabled = false; // Lock window

            CleanupLoadedBitmaps();
            Config.SaveConfig(_config); // Save Config to Config.json
            Memory.Shutdown(); // Wait for Memory Thread to gracefully exit
            e.Cancel = false; // Ready to close
            base.OnFormClosing(e); // Proceed with closing
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
                _config.SetAirStuck = !_config.SetAirStuck;
                Memory._game?.SetAirStuck(_config.SetAirStuck);
                UpdateStatusIndicator(lblStatusAirStuck, _config.SetAirStuck);
                
                if (chkDisableCollision.Checked)
                {
                    _config.DisableCollision = _config.SetAirStuck;
                    Memory._game?.DisableCollision(_config.DisableCollision);
                }
                
                Config.SaveConfig(_config);
                return true;
            }
            else if (keyData == _config.KeybindHideActor && chkHideActor.Checked)
            {
                _config.SetHideActor = !_config.SetHideActor;
                Memory._game?.SetHideActor(_config.SetHideActor);
                UpdateStatusIndicator(lblStatusHideActor, _config.SetHideActor);
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
                statusLabel == lblStatusHideActor ||
                statusLabel == lblStatusToggleEnemyDistance)
            {
                statusLabel.Text = isEnabled ? "ON" : "OFF";
            }
        }

        private void InitializeKeybinds()
        {
            chkDisableSuppression.Checked = _config.DisableSuppression;
            chkDisableSuppression.CheckedChanged += ChkDisableSuppression_CheckedChanged;

            chkSetInteractionDistances.Checked = _config.SetInteractionDistances;
            chkSetInteractionDistances.CheckedChanged += ChkSetInteractionDistances_CheckedChanged;

            chkAllowShootingInMainBase.Checked = _config.AllowShootingInMainBase;
            chkAllowShootingInMainBase.CheckedChanged += ChkAllowShootingInMainBase_CheckedChanged;

            chkSpeedHack.Checked = _config.SetSpeedHack;
            chkSpeedHack.CheckedChanged += ChkSetTimeDilation_CheckedChanged;

            chkAirStuck.Checked = _config.SetAirStuck;
            chkAirStuck.CheckedChanged += ChkAirStuck_CheckedChanged;

            chkDisableCollision.Checked = _config.DisableCollision;
            chkDisableCollision.Enabled = _config.SetAirStuck; // Only enable if AirStuck is checked
            chkDisableCollision.CheckedChanged += ChkDisableCollision_CheckedChanged;

            chkHideActor.Checked = _config.SetHideActor;
            chkHideActor.CheckedChanged += ChkHideActor_CheckedChanged;

            chkQuickZoom.Checked = _config.QuickZoom;
            chkQuickZoom.CheckedChanged += ChkQuickZoom_CheckedChanged;

            chkRapidFire.Checked = _config.RapidFire;
            chkRapidFire.CheckedChanged += ChkRapidFire_CheckedChanged;

            chkShowEnemyDistance.Checked = _config.ShowEnemyDistance;
            chkShowEnemyDistance.CheckedChanged += ChkShowEnemyDistance_CheckedChanged;

            chkInfiniteAmmo.Checked = _config.InfiniteAmmo;
            chkInfiniteAmmo.CheckedChanged += ChkInfiniteAmmo_CheckedChanged;

            chkQuickSwap.Checked = _config.QuickSwap;
            chkQuickSwap.CheckedChanged += ChkQuickSwap_CheckedChanged;

            // Keybind buttons
            btnKeybindSpeedHack.Text = _config.KeybindSpeedHack == Keys.None ? "None" : _config.KeybindSpeedHack.ToString();
            btnKeybindAirStuck.Text = _config.KeybindAirStuck == Keys.None ? "None" : _config.KeybindAirStuck.ToString();
            btnKeybindHideActor.Text = _config.KeybindHideActor == Keys.None ? "None" : _config.KeybindHideActor.ToString();
            btnKeybindQuickZoom.Text = _config.KeybindQuickZoom == Keys.None ? "None" : _config.KeybindQuickZoom.ToString();
            btnKeybindToggleEnemyDistance.Text = _config.KeybindToggleEnemyDistance == Keys.None ? "None" : _config.KeybindToggleEnemyDistance.ToString();
            btnKeybindToggleMap.Text = _config.KeybindToggleMap == Keys.None ? "None" : _config.KeybindToggleMap.ToString();
            btnKeybindToggleFullscreen.Text = _config.KeybindToggleFullscreen == Keys.None ? "None" : _config.KeybindToggleFullscreen.ToString();
            btnKeybindDumpNames.Text = _config.KeybindDumpNames == Keys.None ? "None" : _config.KeybindDumpNames.ToString();
            btnKeybindZoomIn.Text = _config.KeybindZoomIn == Keys.None ? "None" : _config.KeybindZoomIn.ToString();
            btnKeybindZoomOut.Text = _config.KeybindZoomOut == Keys.None ? "None" : _config.KeybindZoomOut.ToString();

            UpdateStatusIndicator(lblStatusSpeedHack, _config.SetSpeedHack);
            UpdateStatusIndicator(lblStatusAirStuck, _config.SetAirStuck);
            UpdateStatusIndicator(lblStatusHideActor, _config.SetHideActor);
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
            bool shift = ModifierKeys.HasFlag(Keys.Shift);
            bool ctrl = ModifierKeys.HasFlag(Keys.Control);
            bool alt = ModifierKeys.HasFlag(Keys.Alt);

            // Hold-to-activate features
            if (_config.KeybindQuickZoom != Keys.None && InputManager.IsKeyDown(_config.KeybindQuickZoom) && chkQuickZoom.Checked)
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
            if (InputManager.IsKeyDown(_config.KeybindZoomIn))
                ZoomIn(_config.ZoomStep);
            else if (InputManager.IsKeyDown(_config.KeybindZoomOut))
                ZoomOut(_config.ZoomStep);

            // Handle feature toggles with keybinds
            if (InputManager.IsKeyPressed(_config.KeybindSpeedHack) && chkSpeedHack.Checked)
            {
                _config.SetSpeedHack = !_config.SetSpeedHack;
                Memory._game?.SetSpeedHack(_config.SetSpeedHack);
                Config.SaveConfig(_config);
                UpdateStatusIndicator(lblStatusSpeedHack, _config.SetSpeedHack);
            }
            if (InputManager.IsKeyPressed(_config.KeybindAirStuck) && chkAirStuck.Checked)
            {
                _config.SetAirStuck = !_config.SetAirStuck;
                Memory._game?.SetAirStuck(_config.SetAirStuck);
                UpdateStatusIndicator(lblStatusAirStuck, _config.SetAirStuck);
                
                // If DisableCollision is checked, toggle it along with AirStuck
                if (chkDisableCollision.Checked)
                {
                    _config.DisableCollision = _config.SetAirStuck;
                    Memory._game?.DisableCollision(_config.DisableCollision);
                }
            }
            if (InputManager.IsKeyPressed(_config.KeybindHideActor) && chkHideActor.Checked)
            {
                _config.SetHideActor = !_config.SetHideActor;
                Memory._game?.SetHideActor(_config.SetHideActor);
                Config.SaveConfig(_config);
                UpdateStatusIndicator(lblStatusHideActor, _config.SetHideActor);
            }

            // Handle other keybinds
            if (InputManager.IsKeyPressed(_config.KeybindToggleEnemyDistance))
            {
                ToggleEnemyDistance();
            }
            if (InputManager.IsKeyPressed(_config.KeybindToggleMap))
                ToggleMap();
            if (InputManager.IsKeyPressed(_config.KeybindToggleFullscreen))
                ToggleFullscreen(FormBorderStyle is FormBorderStyle.Sizable);
            if (InputManager.IsKeyPressed(_config.KeybindDumpNames))
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

        private void HandleMapZoom(MouseEventArgs e)
        {
            int zoomStep = 3;
            if (e.Delta < 0)
                ZoomOut(zoomStep);
            else if (e.Delta > 0)
                ZoomIn(zoomStep);

            if (_isFreeMapToggled && e.Delta < 0)
            {
                var mousePos = _mapCanvas.PointToClient(Cursor.Position);
                var mapParams = GetMapLocation();
                var mapMousePos = new SKPoint(
                    (float)(mapParams.Bounds.Left + mousePos.X / mapParams.XScale),
                    (float)(mapParams.Bounds.Top + mousePos.Y / mapParams.YScale)
                );

                _targetPanPosition = mapMousePos;
                if (!_panTimer.Enabled)
                    _panTimer.Start();
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
                int dx = (int)((e.X - _lastMousePosition.X) * DRAG_SENSITIVITY);
                int dy = (int)((e.Y - _lastMousePosition.Y) * DRAG_SENSITIVITY);

                _targetPanPosition.X -= dx / _config.DefaultZoom * 20; 
                _targetPanPosition.Y -= dy / _config.DefaultZoom * 20;

                _mapPanPosition.X = _targetPanPosition.X;
                _mapPanPosition.Y = _targetPanPosition.Y;

                if (!_panTimer.Enabled)
                    _panTimer.Start();
            }
            _lastMousePosition = e.Location;
        }

        private void HandlePOIHover(MouseEventArgs e)
        {
            _hoveredPoi = null;
            if (InGame && _pointsOfInterest.Count > 0)
            {
                var mapParams = GetMapLocation();
                var mousePos = new SKPoint(e.X, e.Y);

                _hoveredPoi = _pointsOfInterest.FirstOrDefault(poi =>
                {
                    var poiPos = poi.Position.ToMapPos(_selectedMap).ToZoomedPos(mapParams).GetPoint();
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
                var mapParams = GetMapLocation();
                var mouseX = e.X / mapParams.XScale + mapParams.Bounds.Left;
                var mouseY = e.Y / mapParams.YScale + mapParams.Bounds.Top;

                var worldX = (mouseX - _selectedMap.ConfigFile.X) / _selectedMap.ConfigFile.Scale;
                var worldY = (mouseY - _selectedMap.ConfigFile.Y) / _selectedMap.ConfigFile.Scale;
                var worldZ = this.LocalPlayer.Position.Z;

                var poiPosition = new Vector3D(worldX, worldY, worldZ);

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
        {
            if (!btnToggleMap.Enabled)
                return false;

            _mapSelectionIndex = (_mapSelectionIndex + 1) % _maps.Count;
            tabRadar.Text = $"Radar ({_maps[_mapSelectionIndex].Name})";
            _mapChangeTimer.Restart();
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
                map.ConfigFile.MapLayers = map.ConfigFile.MapLayers.OrderBy(x => x.MinHeight).ToList();
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
            trkTechMarkerScale.Value = _config.TechMarkerScale;
            #endregion

            #region Local Soldier Features
            chkDisableSuppression.Checked = _config.DisableSuppression;
            chkSetInteractionDistances.Checked = _config.SetInteractionDistances;
            chkAllowShootingInMainBase.Checked = _config.AllowShootingInMainBase;
            chkSpeedHack.Checked = _config.SetSpeedHack;
            chkAirStuck.Checked = _config.SetAirStuck;
            chkHideActor.Checked = _config.SetHideActor;
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
        {
            // Handle tab control selection changes if needed
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
            for (int i = 0; i < 10; i++) 
            {
                if (Memory._game != null)
                {
                    await Task.Delay(1000); 
                    
                    if (_config.DisableSuppression)
                        Memory._game?.SetSuppression(true);
                    
                    if (_config.SetInteractionDistances)
                        Memory._game?.SetInteractionDistances(true);
                    
                    if (_config.AllowShootingInMainBase)
                        Memory._game?.SetShootingInMainBase(true);
                    
                    if (_config.SetSpeedHack)
                        Memory._game?.SetSpeedHack(true);
                    
                    if (_config.SetAirStuck)
                        Memory._game?.SetAirStuck(true);
                    
                    if (_config.SetHideActor)
                        Memory._game?.SetHideActor(true);
                        
                    if (_config.RapidFire)
                        Memory._game?.SetRapidFire(true);
                        
                    if (_config.InfiniteAmmo)
                        Memory._game?.SetInfiniteAmmo(true);
                        
                    if (_config.QuickSwap)
                        Memory._game?.SetQuickSwap(true);
                        
                    if (_config.DisableCollision)
                        Memory._game?.DisableCollision(true);
                        
                    break;
                }
                await Task.Delay(500);
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
                var selectedMap = _maps.FirstOrDefault(x => x.ConfigFile.MapID.Any(id => id.Equals(currentMap, StringComparison.OrdinalIgnoreCase)));
                if (selectedMap is not null)
                {
                    _selectedMap = selectedMap;
                    CleanupLoadedBitmaps();
                    ClearPointsOfInterest();
                    LoadMapBitmaps();
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
            int mapLayerIndex = GetMapLayerIndex(localPlayerPos.Height);

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
                TechScale = (.01f * _config.TechMarkerScale),
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

                if (_isFreeMapToggled)
                {
                    _mapPanPosition.Height = localPlayerMapPos.Height;
                    _mapPanPosition.TechScale = (.01f * _config.TechMarkerScale);
                    return GetMapParameters(_mapPanPosition);
                }
                else
                {
                    _mapPanPosition.X = localPlayerMapPos.X;
                    _mapPanPosition.Y = localPlayerMapPos.Y;
                    _mapPanPosition.Height = localPlayerMapPos.Height;
                    _mapPanPosition.TechScale = (.01f * _config.TechMarkerScale);
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
            var removedProjectiles = _aaProjectileOrigins.Keys.Except(activeProjectiles).ToList();
            foreach (var projectile in removedProjectiles)
            {
                _aaProjectileOrigins.Remove(projectile);
            }

            var localPlayerMapPos = LocalPlayer.Position.ToMapPos(_selectedMap);
            var mapParams = GetMapLocation();
            var localPlayerZoomedPos = localPlayerMapPos.ToZoomedPos(mapParams);

            localPlayerZoomedPos.DrawPlayerMarker(canvas, LocalPlayer, trkAimLength.Value);

            foreach (var actor in allPlayers)
            {
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

                if (actor.ActorType != ActorType.ProjectileAA)
                {
                    int aimlineLength = actor == LocalPlayer ? 0 : 15;
                    DrawActor(canvas, actor, actorZoomedPos, aimlineLength, localPlayerMapPos);
                }

                if (actor.ActorType == ActorType.ProjectileAA)
                {
                    projectileAAs.Add(actor);
                }
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
                string[] lines = null;
                var height = actorZoomedPos.Height - localPlayerMapPos.Height;

                if (actor.ActorType == ActorType.Player)
                {
                    var color = actor.IsInMySquad() ? SKPaints.Squad : actor.GetEntityPaint().Color;
                    actorZoomedPos.DrawPlayerMarker(canvas, actor, aimlineLength, color);

                    if (!actor.IsFriendly() && _config.ShowEnemyDistance)
                    {
                        var dx = LocalPlayer.Position.X - actor.Position.X;
                        var dy = LocalPlayer.Position.Y - actor.Position.Y;
                        var dz = LocalPlayer.Position.Z - actor.Position.Z;
                        var dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
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
                        _aaProjectileOrigins[actor] = new Vector3D(
                            actor.Position.X + AbsoluteLocation.X,
                            actor.Position.Y + AbsoluteLocation.Y,
                            actor.Position.Z + AbsoluteLocation.Z
                        );

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
                        ActorType.LoachScout,
                        ActorType.TrackedAPCArtillery
                    };

                    if (vehicleTypes.Contains(actor.ActorType))
                    {
                        var dx = LocalPlayer.Position.X - actor.Position.X;
                        var dy = LocalPlayer.Position.Y - actor.Position.Y;
                        var dz = LocalPlayer.Position.Z - actor.Position.Z;
                        var dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
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
            int adminAimlineLength = 40;

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
                DrawDead(canvas, pos, SKColors.Black, SKColors.White, 5 * _uiScale);
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

            foreach (var poi in _pointsOfInterest)
            {
                var poiMapPos = poi.Position.ToMapPos(_selectedMap);
                var poiZoomedPos = poiMapPos.ToZoomedPos(mapParams);

                var dx = localPlayerPos.X - poi.Position.X;
                var dy = localPlayerPos.Y - poi.Position.Y;
                var dz = localPlayerPos.Z - poi.Position.Z;
                var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
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

        private void DrawPOIText(SKCanvas canvas, MapPosition position, double distance, float bearing, float crosshairSize)
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

        private double MetersToMilliradians(double meters)
        {
            double[] distances = { 50, 100, 150, 200, 250, 300, 350, 400, 450, 500, 550, 600, 650, 700, 750, 800, 850, 900, 950, 1000, 1050, 1100, 1150, 1200, 1250 };
            double[] milliradians = { 1579, 1558, 1538, 1517, 1496, 1475, 1453, 1431, 1409, 1387, 1364, 1341, 1317, 1292, 1267, 1240, 1212, 1183, 1152, 1118, 1081, 1039, 988, 918, 800 };

            if (meters <= 50) return 1579.0;
            if (meters >= 1250) return 800.0;

            for (int i = 0; i < distances.Length - 1; i++)
            {
                if (meters >= distances[i] && meters <= distances[i + 1])
                {
                    double rate = (milliradians[i + 1] - milliradians[i]) / (distances[i + 1] - distances[i]);
                    return Math.Round(milliradians[i] + rate * (meters - distances[i]), 1);
                }
            }
            return 800;
        }

        private float CalculateBearing(Vector3D playerPos, Vector3D poiPos)
        {
            double deltaX = poiPos.X - playerPos.X;
            double deltaY = playerPos.Y - poiPos.Y;

            double radians = Math.Atan2(deltaY, deltaX);
            double degrees = radians * (180.0 / Math.PI);
            degrees = 90.0 - degrees;

            if (degrees < 0) degrees += 360.0;

            return (float)degrees;
        }

        private void DrawToolTips(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            var mapParams = GetMapLocation();

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
            var panDifference = new SKPoint(
                (float)(this._targetPanPosition.X - this._mapPanPosition.X),
                (float)(this._targetPanPosition.Y - this._mapPanPosition.Y)
            );

            if (panDifference.Length > 0.1)
            {
                this._mapPanPosition.X += (float)(panDifference.X * PAN_SMOOTHNESS);
                this._mapPanPosition.Y += (float)(panDifference.Y * PAN_SMOOTHNESS);
            }
            else
            {
                this._panTimer.Stop();
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
                var mapParams = GetMapLocation();
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
            Memory.Restart();
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

        private void ChkShowEnemyDistance_CheckedChanged(object sender, EventArgs e)
        {
            _config.ShowEnemyDistance = chkShowEnemyDistance.Checked;
            UpdateStatusIndicator(lblStatusToggleEnemyDistance, _config.ShowEnemyDistance);
            _mapCanvas.Invalidate();
            Config.SaveConfig(_config);
        }

        private void trkUIScale_Scroll(object sender, EventArgs e)
        {
            _config.UIScale = trkUIScale.Value;
            _uiScale = .01f * trkUIScale.Value;

            InitiateUIScaling();
        }

        private void trkTechMarkerScale_Scroll(object sender, EventArgs e)
        {
            _config.TechMarkerScale = trkTechMarkerScale.Value;
            
            if (_isFreeMapToggled)
            {
                _mapPanPosition.TechScale = .01f * _config.TechMarkerScale;
            }
            
            _mapCanvas.Invalidate();
            Config.SaveConfig(_config);
        }

        private void ChkDisableSuppression_CheckedChanged(object sender, EventArgs e)
        {
            if (!InGame) return;

            _config.DisableSuppression = chkDisableSuppression.Checked;
            Memory._game?.SetSuppression(chkDisableSuppression.Checked);
            Config.SaveConfig(_config);
        }

        private void ChkSetInteractionDistances_CheckedChanged(object sender, EventArgs e)
        {
            if (!InGame) return;
            
            _config.SetInteractionDistances = chkSetInteractionDistances.Checked;
            Memory._game?.SetInteractionDistances(_config.SetInteractionDistances);
            Config.SaveConfig(_config);
        }

        private void ChkAllowShootingInMainBase_CheckedChanged(object sender, EventArgs e)
        {
            if (!InGame) return;
            
            _config.AllowShootingInMainBase = chkAllowShootingInMainBase.Checked;
            Memory._game?.SetShootingInMainBase(_config.AllowShootingInMainBase);
            Config.SaveConfig(_config);
        }

        private void ChkSetTimeDilation_CheckedChanged(object sender, EventArgs e)
        {
            if (!InGame) return;
            if (!chkSpeedHack.Checked)
            {
                _config.SetSpeedHack = false;
                Memory._game?.SetSpeedHack(false);
                UpdateStatusIndicator(lblStatusSpeedHack, false);
            }
            Config.SaveConfig(_config);
        }

        private void ChkAirStuck_CheckedChanged(object sender, EventArgs e)
        {
            if (!InGame) return;
            
            _config.SetAirStuck = chkAirStuck.Checked;
            
            chkDisableCollision.Enabled = chkAirStuck.Checked;
            
            if (!chkAirStuck.Checked && chkDisableCollision.Checked)
            {
                chkDisableCollision.Checked = false;
                _config.DisableCollision = false;
            }
            
            Config.SaveConfig(_config);
        }

        private void ChkDisableCollision_CheckedChanged(object sender, EventArgs e)
        {
            if (!InGame) return;
            
            if (chkDisableCollision.Checked && !chkAirStuck.Checked)
            {
                chkDisableCollision.Checked = false;
                return;
            }
            
            _config.DisableCollision = chkDisableCollision.Checked;
            Config.SaveConfig(_config);
        }

        private void ChkHideActor_CheckedChanged(object sender, EventArgs e)
        {
            if (!InGame) return;
            if (!chkHideActor.Checked)
            {
                _config.SetHideActor = false;
                Memory._game?.SetHideActor(false);
                UpdateStatusIndicator(lblStatusHideActor, false);
            }
            Config.SaveConfig(_config);
        }

        private void ChkQuickZoom_CheckedChanged(object sender, EventArgs e)
        {
            _config.QuickZoom = chkQuickZoom.Checked;
            Config.SaveConfig(_config);
        }

        private void ChkRapidFire_CheckedChanged(object sender, EventArgs e)
        {
            _config.RapidFire = chkRapidFire.Checked;
            Memory._game?.SetRapidFire(_config.RapidFire);
            Config.SaveConfig(_config);
        }

        private void ChkInfiniteAmmo_CheckedChanged(object sender, EventArgs e)
        {
            _config.InfiniteAmmo = chkInfiniteAmmo.Checked;
            Memory._game?.SetInfiniteAmmo(_config.InfiniteAmmo);
            Config.SaveConfig(_config);
        }

        private void ChkQuickSwap_CheckedChanged(object sender, EventArgs e)
        {
            _config.QuickSwap = chkQuickSwap.Checked;
            Memory._game?.SetQuickSwap(_config.QuickSwap);
            Config.SaveConfig(_config);
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

        private void lblAimline_Click(object sender, EventArgs e)
        {

        }

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
            else if (_currentKeybindButton == btnKeybindHideActor)
            {
                _config.KeybindHideActor = key;
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

        private void BtnKeybindHideActor_Click(object sender, EventArgs e)
        {
            StartKeybindCapture(btnKeybindHideActor);
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