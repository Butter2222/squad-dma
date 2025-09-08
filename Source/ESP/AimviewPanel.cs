using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Runtime.InteropServices;
using System.Numerics;
using SharpDX.DirectWrite;
using System.Diagnostics;
using System;
using System.Reflection;

namespace squad_dma
{
    /// <summary>
    /// AimviewPanel - A resizable, draggable ESP overlay panel with crosshair
    /// Features: Real-time ESP rendering, collision-safe resizing, thread-safe DirectX operations
    /// Size constraints: 480x270 (mini 1080p) to 860x360 (mini 3440x1440)
    /// </summary>
    public partial class AimviewPanel : UserControl
    {
        private WindowRenderTarget renderTarget;
        private SolidColorBrush brush;
        private SolidColorBrush vehicleBrush;
        private SolidColorBrush boneBrush;
        private SolidColorBrush healthBrush;
        private SolidColorBrush friendlyBrush;
        private SolidColorBrush crosshairBrush;
        private SharpDX.DirectWrite.TextFormat textFormat;
        private volatile bool running = true;
        private readonly object _renderLock = new object();
        private Game Game => Memory._game;
        private DateTime _lastSuccessfulRender = DateTime.Now;

        // Panel state
        private bool _isCollapsed = false;
        private int _expandedHeight = 360; // Mini 2560x1440: 2560/7 ≈ 366, 1440/4 = 360
        private int _collapsedHeight = 24;
        private int _minWidth = 480;   // Mini 1080p width (1920/4 = 480)
        private int _minHeight = 270;  // Mini 1080p height (1080/4 = 270)
        private int _maxWidth = 860;   // Mini 3440x1440 width (3440/4 = 860)
        private int _maxHeight = 360;  // Mini 3440x1440 height (1440/4 = 360)

        // UI Controls
        private Panel _headerPanel;
        private Panel _contentPanel;
        private Label _titleLabel;
        private Button _toggleButton;
        
        // Resize functionality
        private bool _isResizing = false;
        private Point _resizeStartPoint;
        private Size _resizeStartSize;
        
        // Drag functionality
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private Point _dragStartLocation;
        private DateTime _lastDragUpdate = DateTime.MinValue;
        private bool _isResizeHandleHovered = false;

        private static readonly Dictionary<ActorType, string> ActorTypeNames = new Dictionary<ActorType, string>
        {
            { ActorType.FOBRadio, "FOB Radio" },
            { ActorType.Hab, "Hab" },
            { ActorType.AntiAir, "Anti-Air" },
            { ActorType.APC, "APC" },
            { ActorType.AttackHelicopter, "Attack Helicopter" },
            { ActorType.LoachCAS, "Loach CAS" },
            { ActorType.LoachScout, "Loach Scout" },
            { ActorType.Boat, "Boat" },
            { ActorType.BoatLogistics, "Boat Logistics" },
            { ActorType.DeployableAntiAir, "Deployable Anti-Air" },
            { ActorType.DeployableAntitank, "Deployable Antitank" },
            { ActorType.DeployableAntitankGun, "Deployable Antitank Gun" },
            { ActorType.DeployableGMG, "Deployable GMG" },
            { ActorType.DeployableHellCannon, "Deployable Hell Cannon" },
            { ActorType.DeployableHMG, "Deployable HMG" },
            { ActorType.DeployableMortars, "Deployable Mortars" },
            { ActorType.DeployableRockets, "Deployable Rockets" },
            { ActorType.Drone, "Drone" },
            { ActorType.IFV, "IFV" },
            { ActorType.JeepAntiAir, "Jeep Anti-Air" },
            { ActorType.JeepAntitank, "Jeep Antitank" },
            { ActorType.JeepArtillery, "Jeep Artillery" },
            { ActorType.JeepLogistics, "Jeep Logistics" },
            { ActorType.JeepTransport, "Jeep Transport" },
            { ActorType.JeepRWSTurret, "Jeep RWS Turret" },
            { ActorType.JeepTurret, "Jeep Turret" },
            { ActorType.Mine, "Mine" },
            { ActorType.Motorcycle, "Motorcycle" },
            { ActorType.RallyPoint, "Rally Point" },
            { ActorType.Tank, "Tank" },
            { ActorType.TankMGS, "Tank MGS" },
            { ActorType.TrackedAPC, "Tracked APC" },
            { ActorType.TrackedAPCArtillery, "Tracked APC Artillery" },
            { ActorType.TrackedIFV, "Tracked IFV" },
            { ActorType.TrackedJeep, "Tracked Jeep" },
            { ActorType.TrackedLogistics, "Tracked Logistics" },
            { ActorType.TransportHelicopter, "Transport Helicopter" },
            { ActorType.TruckAntiAir, "Truck Anti-Air" },
            { ActorType.TruckArtillery, "Truck Artillery" },
            { ActorType.TruckLogistics, "Truck Logistics" },
            { ActorType.TruckTransport, "Truck Transport" },
            { ActorType.TruckTransportArmed, "Truck Transport Armed" }
        };

        private static readonly HashSet<ActorType> VehicleTypes = new HashSet<ActorType>
        {
            ActorType.AntiAir, ActorType.APC, ActorType.AttackHelicopter, ActorType.LoachCAS, ActorType.LoachScout,
            ActorType.Boat, ActorType.BoatLogistics, ActorType.IFV, ActorType.JeepAntiAir, ActorType.JeepAntitank,
            ActorType.JeepArtillery, ActorType.JeepLogistics, ActorType.JeepTransport, ActorType.JeepRWSTurret,
            ActorType.JeepTurret, ActorType.Motorcycle, ActorType.Tank, ActorType.TankMGS, ActorType.TrackedAPC,
            ActorType.TrackedAPCArtillery, ActorType.TrackedIFV, ActorType.TrackedJeep, ActorType.TrackedLogistics,
            ActorType.TransportHelicopter, ActorType.TruckAntiAir, ActorType.TruckArtillery, ActorType.TruckLogistics,
            ActorType.TruckTransport, ActorType.TruckTransportArmed
        };

        public AimviewPanel()
        {
            InitializeComponent();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
            
            // Clean, minimal design with dark background
            BackColor = Color.FromArgb(25, 25, 25); // Dark gray background
            
            // Load saved size from config with bounds checking, or use defaults
            int savedWidth = Math.Max(_minWidth, Math.Min(Program.Config.AimviewPanelWidth, _maxWidth));
            int savedHeight = Math.Max(_minHeight, Math.Min(Program.Config.AimviewPanelHeight, _maxHeight));
            _expandedHeight = savedHeight;
            Size = new Size(savedWidth, savedHeight);
            
            // Add custom painting for border only (background handled by BackColor)
            this.Paint += (s, e) => {
                // Draw border
                using (var pen = new Pen(Color.FromArgb(100, 100, 100), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            };
            
            CreateHeaderPanel();
            CreateContentPanel();
            SetupResizing();
            InitializeDirect2D();
            StartRenderLoop();
            
            // Force initial paint to show resize handle
            this.Invalidate(true);
            
        }

        private void CreateHeaderPanel()
        {
            _headerPanel = new Panel
            {
                Height = _collapsedHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(35, 35, 35), // Slightly lighter header background
                Padding = new Padding(1)
            };
            
            // Enable double buffering to prevent visual artifacts
            typeof(Panel).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, _headerPanel, new object[] { true });

            _titleLabel = new Label
            {
                Text = "≡ Aimview", // Add drag indicator
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(180, 180, 180), // Light gray text
                Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            _toggleButton = new Button
            {
                Text = "▼",
                Dock = DockStyle.Right,
                Width = 20,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(160, 160, 160),
                Font = new System.Drawing.Font("Segoe UI", 7f),
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand
            };
            _toggleButton.Click += ToggleButton_Click;
            _toggleButton.MouseEnter += (s, e) => _toggleButton.ForeColor = Color.White;
            _toggleButton.MouseLeave += (s, e) => _toggleButton.ForeColor = Color.FromArgb(160, 160, 160);

            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_toggleButton);
            this.Controls.Add(_headerPanel);
        }

        private void CreateContentPanel()
        {
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 15, 15), 
                BorderStyle = BorderStyle.None,
                Margin = new Padding(1)
            };
            
            // Enable custom painting for the content panel using reflection
            typeof(Panel).InvokeMember("SetStyle",
                BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.NonPublic,
                null, _contentPanel, new object[] { ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true });
            
            // Add content panel painting for resize handle
            _contentPanel.Paint += (s, e) => {
                try
                {
                    // Draw resize handle
                    int handleSize = 15;
                    int panelWidth = _contentPanel.Width;
                    int panelHeight = _contentPanel.Height;
                    
                    // Make sure we have valid dimensions
                    if (panelWidth <= handleSize || panelHeight <= handleSize) return;
                    
                    Color handleColor = _isResizeHandleHovered ? Color.FromArgb(200, 200, 200) : Color.FromArgb(150, 150, 150);
                    Color borderColor = _isResizeHandleHovered ? Color.FromArgb(255, 255, 255) : Color.FromArgb(180, 180, 180);
                    
                    using (var handleBrush = new SolidBrush(handleColor))
                    using (var borderPen = new Pen(borderColor, 2))
                    {
                        // Draw resize handle rectangle
                        Rectangle handleRect = new Rectangle(panelWidth - handleSize, panelHeight - handleSize, handleSize, handleSize);
                        e.Graphics.FillRectangle(handleBrush, handleRect);
                        e.Graphics.DrawRectangle(borderPen, handleRect);
                        
                        // Draw grip lines inside the rectangle
                        using (var gripPen = new Pen(Color.FromArgb(80, 80, 80), 1))
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                int offset = i * 3 + 3;
                                int x1 = panelWidth - offset;
                                int y1 = panelHeight - 3;
                                int x2 = panelWidth - 3;
                                int y2 = panelHeight - offset;
                                
                                if (x1 > panelWidth - handleSize && y1 > panelHeight - handleSize &&
                                    x2 > panelWidth - handleSize && y2 > panelHeight - handleSize)
                                {
                                    e.Graphics.DrawLine(gripPen, x1, y1, x2, y2);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Debug: ignore painting errors for now
                }
            };
            
            // Add mouse event handlers to content panel for resize handle
            _contentPanel.MouseDown += ContentPanel_MouseDown;
            _contentPanel.MouseMove += ContentPanel_MouseMove;
            _contentPanel.MouseUp += ContentPanel_MouseUp;
            
            this.Controls.Add(_contentPanel);
        }

        private void ToggleButton_Click(object sender, EventArgs e)
        {
            _isCollapsed = !_isCollapsed;
            
            if (_isCollapsed)
            {
                _toggleButton.Text = "▶";
                _contentPanel.Visible = false;
                this.Height = _collapsedHeight;
            }
            else
            {
                _toggleButton.Text = "▼";
                _contentPanel.Visible = true;
                this.Height = _expandedHeight;
            }
        }

        private void SetupResizing()
        {
            // Add resize and drag functionality
            this.MouseDown += AimviewPanel_MouseDown;
            this.MouseMove += AimviewPanel_MouseMove;
            this.MouseUp += AimviewPanel_MouseUp;
            this.Cursor = Cursors.Default;
            
            // Make header panel draggable
            _headerPanel.MouseDown += HeaderPanel_MouseDown;
            _headerPanel.MouseMove += HeaderPanel_MouseMove;
            _headerPanel.MouseUp += HeaderPanel_MouseUp;
            _headerPanel.MouseEnter += (s, e) => { if (!_isResizing && !_isDragging) _headerPanel.Cursor = Cursors.SizeAll; };
            _headerPanel.MouseLeave += (s, e) => { if (!_isResizing && !_isDragging) _headerPanel.Cursor = Cursors.Default; };
            
            _titleLabel.MouseDown += HeaderPanel_MouseDown;
            _titleLabel.MouseMove += HeaderPanel_MouseMove;
            _titleLabel.MouseUp += HeaderPanel_MouseUp;
            _titleLabel.MouseEnter += (s, e) => { if (!_isResizing && !_isDragging) _titleLabel.Cursor = Cursors.SizeAll; };
            _titleLabel.MouseLeave += (s, e) => { if (!_isResizing && !_isDragging) _titleLabel.Cursor = Cursors.Default; };
        }

        private void AimviewPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !_isDragging)
            {
                // Check if mouse is in resize area (bottom-right 15x15 pixels)
                Rectangle resizeArea = new Rectangle(Width - 15, Height - 15, 15, 15);
                if (resizeArea.Contains(e.Location))
                {
                    _isResizing = true;
                    _resizeStartPoint = Control.MousePosition;
                    _resizeStartSize = this.Size;
                    this.Cursor = Cursors.SizeNWSE;
                }
            }
        }

        private void AimviewPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging && !_isResizing)
            {
                // Change cursor when hovering over resize area
                Rectangle resizeArea = new Rectangle(Width - 15, Height - 15, 15, 15);
                bool wasHovered = _isResizeHandleHovered;
                _isResizeHandleHovered = resizeArea.Contains(e.Location);
                
                if (_isResizeHandleHovered)
                {
                    this.Cursor = Cursors.SizeNWSE;
                    if (!wasHovered) // Only invalidate when hover state changes
                        this.Invalidate(resizeArea);
                }
                else
                {
                    this.Cursor = Cursors.Default;
                    if (wasHovered) // Only invalidate when hover state changes
                        this.Invalidate(resizeArea);
                }
            }

            // Handle resizing
            if (_isResizing)
            {
                Point currentMousePos = Control.MousePosition;
                int newWidth = Math.Max(_minWidth, _resizeStartSize.Width + (currentMousePos.X - _resizeStartPoint.X));
                int newHeight = Math.Max(_minHeight, _resizeStartSize.Height + (currentMousePos.Y - _resizeStartPoint.Y));
                
                // Enforce maximum size limits to prevent SharpDX crashes
                newWidth = Math.Min(newWidth, _maxWidth);
                newHeight = Math.Min(newHeight, _maxHeight);
                
                this.Size = new Size(newWidth, newHeight);
                _expandedHeight = newHeight;
                
                // Update Direct2D render target size safely
                if (renderTarget != null && _contentPanel != null)
                {
                    try
                    {
                        // Validate size before attempting resize
                        if (_contentPanel.Width <= _maxWidth && _contentPanel.Height <= _maxHeight)
                        {
                            renderTarget.Resize(new Size2(_contentPanel.Width, _contentPanel.Height));
                        }
                    }
                    catch (SharpDX.SharpDXException)
                    {
                        // If resize fails, recreate the render target
                        InitializeDirect2D();
                    }
                    catch (System.AccessViolationException)
                    {
                        // Critical error - dispose and recreate everything
                        DisposeRenderTarget();
                        InitializeDirect2D();
                    }
                }
            }
        }

        private void AimviewPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isResizing)
            {
                _isResizing = false;
                this.Cursor = Cursors.Default;
                SavePosition();
            }
        }

        private void HeaderPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !_isResizing)
            {
                _isDragging = true;
                _dragStartPoint = Control.MousePosition; // Use global mouse position
                _dragStartLocation = this.Location;
            }
        }

        private void HeaderPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                // Throttle updates to prevent excessive redraws (max 60 FPS)
                DateTime now = DateTime.Now;
                if ((now - _lastDragUpdate).TotalMilliseconds < 16) // ~60 FPS
                    return;
                    
                _lastDragUpdate = now;

                Point currentMousePos = Control.MousePosition; // Use global mouse position
                Point newLocation = new Point(
                    _dragStartLocation.X + (currentMousePos.X - _dragStartPoint.X),
                    _dragStartLocation.Y + (currentMousePos.Y - _dragStartPoint.Y)
                );

                // Keep panel within parent bounds
                if (this.Parent != null)
                {
                    newLocation.X = Math.Max(0, Math.Min(newLocation.X, this.Parent.Width - this.Width));
                    newLocation.Y = Math.Max(0, Math.Min(newLocation.Y, this.Parent.Height - this.Height));
                }

                // Only update location if it actually changed (reduces redraws)
                if (this.Location != newLocation)
                {
                    this.SuspendLayout();
                    this.Location = newLocation;
                    this.ResumeLayout(false);
                    
                    // Force header refresh to prevent corruption
                    _headerPanel?.Invalidate();
                }
            }
        }

        private void HeaderPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                SavePosition();
                
                // Final refresh to ensure header is properly drawn
                _headerPanel?.Invalidate();
                _titleLabel?.Invalidate();
                this.Refresh();
            }
        }

        private void ContentPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !_isDragging)
            {
                // Check if mouse is in resize area (bottom-right 15x15 pixels of content panel)
                Rectangle resizeArea = new Rectangle(_contentPanel.Width - 15, _contentPanel.Height - 15, 15, 15);
                if (resizeArea.Contains(e.Location))
                {
                    _isResizing = true;
                    _resizeStartPoint = Control.MousePosition;
                    _resizeStartSize = this.Size;
                    this.Cursor = Cursors.SizeNWSE;
                }
            }
        }

        private void ContentPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging && !_isResizing)
            {
                // Change cursor when hovering over resize area
                Rectangle resizeArea = new Rectangle(_contentPanel.Width - 15, _contentPanel.Height - 15, 15, 15);
                bool wasHovered = _isResizeHandleHovered;
                _isResizeHandleHovered = resizeArea.Contains(e.Location);
                
                if (_isResizeHandleHovered)
                {
                    this.Cursor = Cursors.SizeNWSE;
                    if (!wasHovered) // Only invalidate when hover state changes
                        _contentPanel.Invalidate(resizeArea);
                }
                else
                {
                    this.Cursor = Cursors.Default;
                    if (wasHovered) // Only invalidate when hover state changes
                        _contentPanel.Invalidate(resizeArea);
                }
            }

            // Handle resizing
            if (_isResizing)
            {
                Point currentMousePos = Control.MousePosition;
                int newWidth = Math.Max(_minWidth, _resizeStartSize.Width + (currentMousePos.X - _resizeStartPoint.X));
                int newHeight = Math.Max(_minHeight, _resizeStartSize.Height + (currentMousePos.Y - _resizeStartPoint.Y));
                
                // Enforce maximum size limits to prevent SharpDX crashes
                newWidth = Math.Min(newWidth, _maxWidth);
                newHeight = Math.Min(newHeight, _maxHeight);
                
                this.Size = new Size(newWidth, newHeight);
                _expandedHeight = newHeight;
                
                // Update Direct2D render target size safely
                if (renderTarget != null && _contentPanel != null)
                {
                    try
                    {
                        // Validate size before attempting resize
                        if (_contentPanel.Width <= _maxWidth && _contentPanel.Height <= _maxHeight)
                        {
                            renderTarget.Resize(new Size2(_contentPanel.Width, _contentPanel.Height));
                        }
                    }
                    catch (SharpDX.SharpDXException)
                    {
                        // If resize fails, recreate the render target
                        InitializeDirect2D();
                    }
                    catch (System.AccessViolationException)
                    {
                        // Critical error - dispose and recreate everything
                        DisposeRenderTarget();
                        InitializeDirect2D();
                    }
                }
            }
        }

        private void ContentPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isResizing)
            {
                _isResizing = false;
                this.Cursor = Cursors.Default;
                SavePosition();
            }
        }

        private void SavePosition()
        {
            Program.Config.AimviewPanelX = this.Location.X;
            Program.Config.AimviewPanelY = this.Location.Y;
            Program.Config.AimviewPanelWidth = this.Width;
            Program.Config.AimviewPanelHeight = this.Height;
            Config.SaveConfig(Program.Config);
        }

        public void ApplySavedLocationAndSize()
        {
            // Apply saved position (if valid and panel has a parent)
            if (this.Parent != null && Program.Config.AimviewPanelX >= 0 && Program.Config.AimviewPanelY >= 0)
            {
                int x = Program.Config.AimviewPanelX;
                int y = Program.Config.AimviewPanelY;
                
                // Ensure position is within parent bounds
                x = Math.Max(0, Math.Min(x, this.Parent.Width - this.Width));
                y = Math.Max(0, Math.Min(y, this.Parent.Height - this.Height));
                
                this.Location = new Point(x, y);
            }
            else if (this.Parent != null)
            {
                // Default position (bottom-right)
                this.Location = new Point(this.Parent.Width - this.Width - 10, this.Parent.Height - this.Height - 10);
            }
            
            // Apply saved size with bounds checking
            int savedWidth = Math.Max(_minWidth, Math.Min(Program.Config.AimviewPanelWidth, _maxWidth));
            int savedHeight = Math.Max(_minHeight, Math.Min(Program.Config.AimviewPanelHeight, _maxHeight));
            this.Size = new Size(savedWidth, savedHeight);
            _expandedHeight = savedHeight;
            
            // Update Direct2D render target if needed
            if (renderTarget != null && _contentPanel != null)
            {
                try
                {
                    renderTarget.Resize(new Size2(_contentPanel.Width, _contentPanel.Height));
                }
                catch (SharpDX.SharpDXException)
                {
                    InitializeDirect2D();
                }
            }
        }


        private void DisposeRenderTarget()
        {
            lock (_renderLock)
            {
                try
                {
                    renderTarget?.Dispose();
                    renderTarget = null;
                    brush?.Dispose();
                    brush = null;
                    vehicleBrush?.Dispose();
                    vehicleBrush = null;
                    healthBrush?.Dispose();
                    healthBrush = null;
                    friendlyBrush?.Dispose();
                    friendlyBrush = null;
                    crosshairBrush?.Dispose();
                    crosshairBrush = null;
                    textFormat?.Dispose();
                    textFormat = null;
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }

        private void InitializeDirect2D()
        {
            lock (_renderLock)
            {
                if (_contentPanel?.Handle == IntPtr.Zero || !running || _contentPanel.IsDisposed)
                    return;

                // Dispose existing resources first
                renderTarget?.Dispose();
                brush?.Dispose();
                vehicleBrush?.Dispose();
                healthBrush?.Dispose();
                friendlyBrush?.Dispose();
                crosshairBrush?.Dispose();
                textFormat?.Dispose();

            try
            {
                var factory = new SharpDX.Direct2D1.Factory();
                var renderProperties = new HwndRenderTargetProperties
                {
                    Hwnd = _contentPanel.Handle,
                    PixelSize = new Size2(Math.Max(1, _contentPanel.Width), Math.Max(1, _contentPanel.Height)),
                    PresentOptions = PresentOptions.Immediately
                };
                renderTarget = new WindowRenderTarget(factory, new RenderTargetProperties(), renderProperties);
                
                brush = new SolidColorBrush(renderTarget, new RawColor4(
                    Program.Config.EspTextColor.R / 255f,
                    Program.Config.EspTextColor.G / 255f,
                    Program.Config.EspTextColor.B / 255f,
                    Program.Config.EspTextColor.A / 255f));
                    
                vehicleBrush = new SolidColorBrush(renderTarget, new RawColor4(
                    Program.Config.EspTextColor.R / 255f,
                    Program.Config.EspTextColor.G / 255f,
                    Program.Config.EspTextColor.B / 255f,
                    Program.Config.EspTextColor.A / 255f));
                    
                boneBrush = brush;
                healthBrush = new SolidColorBrush(renderTarget, new RawColor4(1.0f, 0.0f, 0.0f, 1.0f));
                
                // Friendly/ally brush using SKPaints.Friendly color (light blue)
                friendlyBrush = new SolidColorBrush(renderTarget, new RawColor4(0f / 255f, 187f / 255f, 254f / 255f, 1.0f));
                
                // Crosshair brush (semi-transparent white)
                crosshairBrush = new SolidColorBrush(renderTarget, new RawColor4(1.0f, 1.0f, 1.0f, 0.8f));

                var writeFactory = new SharpDX.DirectWrite.Factory();
                textFormat = new SharpDX.DirectWrite.TextFormat(writeFactory, "Segoe UI", 9.0f);
            }
            catch (SharpDX.SharpDXException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize Direct2D: {ex.Message}");
            }
            }
        }

        private void StartRenderLoop()
        {
            Task.Run(() =>
            {
                while (running)
                {
                    if (renderTarget != null && !_isCollapsed)
                    {
                        try
                        {
                            RenderFrame();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Render error: {ex.Message}");
                        }
                    }
                    Thread.Sleep(8); // ~125 FPS
                }
            });
        }

        private void RenderFrame()
        {
            lock (_renderLock)
            {
                if (renderTarget == null || !running) return;

                try
                {
                    renderTarget.BeginDraw();
                    renderTarget.Clear(new RawColor4(0.06f, 0.06f, 0.06f, 0.95f)); // Clean dark background
                    
                    DrawEsp();
                    DrawCrosshair();
                    
                    renderTarget.EndDraw();
                    _lastSuccessfulRender = DateTime.Now;
                }
                catch (SharpDX.SharpDXException)
                {
                    // If rendering fails, try to recreate the render target
                    SafeDisposeAndRecreate();
                }
                catch (System.AccessViolationException)
                {
                    // Critical DirectX error - dispose everything and recreate
                    SafeDisposeAndRecreate();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Render error: {ex.Message}");
                    // Try to recover from any other rendering error
                    SafeDisposeAndRecreate();
                }
            }
        }

        private void SafeDisposeAndRecreate()
        {
            try
            {
                // Prevent rapid recreation attempts
                var timeSinceLastRender = DateTime.Now - _lastSuccessfulRender;
                if (timeSinceLastRender.TotalMilliseconds < 100)
                {
                    Thread.Sleep(100);
                }

                DisposeRenderTarget();
                
                // Wait a bit before recreating to avoid rapid recreation
                Thread.Sleep(50);
                
                if (running && _contentPanel?.Handle != IntPtr.Zero && !_contentPanel.IsDisposed)
                {
                    InitializeDirect2D();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during render target recreation: {ex.Message}");
            }
        }

        private void DrawCrosshair()
        {
            if (renderTarget == null || _contentPanel == null || crosshairBrush == null) return;

            try
            {
                // Calculate center of the content panel
                float centerX = _contentPanel.Width / 2.0f;
                float centerY = _contentPanel.Height / 2.0f;

                // Draw vertical line (center X)
                renderTarget.DrawLine(
                    new RawVector2(centerX, 0),
                    new RawVector2(centerX, _contentPanel.Height),
                    crosshairBrush,
                    1.0f
                );

                // Draw horizontal line (center Y)
                renderTarget.DrawLine(
                    new RawVector2(0, centerY),
                    new RawVector2(_contentPanel.Width, centerY),
                    crosshairBrush,
                    1.0f
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Crosshair draw error: {ex.Message}");
            }
        }

        private void DrawEsp()
        {
            var game = Game;
            if (game?.LocalPlayer == null) return;

            var localPlayer = game.LocalPlayer;
            var viewInfo = new MinimalViewInfo
            {
                Location = localPlayer.Position,
                Rotation = localPlayer.Rotation3D,
                FOV = game.CurrentFOV
            };

            var visibleActors = new List<(UActor actor, Vector2 panelPos, float distance)>();

            // Process all actors
            foreach (var kvp in game.Actors)
            {
                var actor = kvp.Value;
                if (actor == null || actor.Position == Vector3D.Zero)
                    continue;

                // Skip local player
                if (actor == localPlayer)
                    continue;

                // Skip vehicles in aimview
                if (VehicleTypes.Contains(actor.ActorType))
                    continue;

                if (actor.ActorType == ActorType.Player && !actor.IsAlive)
                    continue;

                // Calculate distance
                Vector3 localPlayerPosVec = localPlayer.Position.ToVector3();
                Vector3 actorPosVec = actor.Position.ToVector3();
                float distance = Vector3.Distance(localPlayerPosVec, actorPosVec) / 100f;
                
                // Apply distance limits based on actor type
                bool isPlayer = actor.ActorType == ActorType.Player;
                float maxDistance = isPlayer ? Program.Config.EspMaxDistance : Program.Config.EspMaxDistance + 1000f; // Vehicles visible at longer range
                if (distance > maxDistance)
                    continue;

                // Skip allies if not showing them
                if (!Program.Config.EspShowAllies && actor.IsFriendly())
                    continue;

                // World to screen conversion
                Vector2 screenPos = Camera.WorldToScreen(viewInfo, actor.Position);
                if (screenPos == Vector2.Zero)
                    continue;

                // Convert screen coordinates to panel coordinates
                // Simple direct scaling - let's see if this fixes the bunching issue
                Vector2 panelPos = new Vector2(
                    (screenPos.X / Screen.PrimaryScreen.Bounds.Width) * _contentPanel.Width,
                    (screenPos.Y / Screen.PrimaryScreen.Bounds.Height) * _contentPanel.Height
                );

                // Skip if outside panel bounds (with some margin for partially visible elements)
                if (panelPos.X < -50 || panelPos.X > _contentPanel.Width + 50 || 
                    panelPos.Y < -50 || panelPos.Y > _contentPanel.Height + 50)
                    continue;
                    
                // Skip if coordinates are invalid
                if (float.IsNaN(panelPos.X) || float.IsNaN(panelPos.Y) || 
                    float.IsInfinity(panelPos.X) || float.IsInfinity(panelPos.Y))
                    continue;

                visibleActors.Add((actor, panelPos, distance));
            }

            // Draw all visible actors (players only, vehicles filtered out)
            foreach (var (actor, panelPos, distance) in visibleActors)
            {
                if (actor.ActorType == ActorType.Player)
                {
                    if (Program.Config.EspShowBox)
                        CalculatePlayerBox(actor, panelPos, distance);
                    if (Program.Config.EspBones)
                        DrawBoneLines(actor, panelPos);
                }
                // Note: Vehicles are filtered out in the processing loop above
            }
        }

        private void CalculatePlayerBox(UActor actor, Vector2 panelPos, float distance)
        {
            // Use a simple fixed-size box based on distance for now
            // This prevents coordinate conversion issues
            float boxSize = Math.Max(20f, Math.Min(100f, 2000f / distance)); // Adaptive size based on distance
            float boxWidth = boxSize * 0.6f; // Narrower width for player aspect ratio
            float boxHeight = boxSize;

            var rect = new RawRectangleF(
                panelPos.X - boxWidth / 2f,
                panelPos.Y - boxHeight / 2f,
                panelPos.X + boxWidth / 2f,
                panelPos.Y + boxHeight / 2f
            );

            // Draw slim box outline with appropriate color
            var boxBrush = actor.IsFriendly() ? friendlyBrush : brush;
            renderTarget.DrawRectangle(rect, boxBrush, 1.0f);

            // Draw health bar
            DrawHealthBar(actor, rect);

            // Draw player info text (name and/or distance)
            var textParts = new List<string>();
            
            if (Program.Config.EspShowNames && !string.IsNullOrEmpty(actor.Name))
            {
                textParts.Add(actor.Name);
            }
            
            if (Program.Config.EspShowDistance)
            {
                textParts.Add($"{distance:F0}m");
            }
            
            if (textParts.Count > 0)
            {
                var text = string.Join(" ", textParts);
                var textBrush = actor.IsFriendly() ? friendlyBrush : brush;
                renderTarget.DrawText(text, textFormat, 
                    new RawRectangleF(rect.Left, rect.Bottom + 2, rect.Left + 200, rect.Bottom + 20), textBrush);
            }
        }

        private void DrawHealthBar(UActor actor, RawRectangleF rect)
        {
            if (!Program.Config.EspShowHealth || actor.Health <= 0)
                return;

            float healthPercent = actor.Health / 100.0f;
            float barWidth = rect.Right - rect.Left;
            float barHeight = 2.0f; // Thinner health bar

            var healthRect = new RawRectangleF(
                rect.Left,
                rect.Top - barHeight - 1,
                rect.Left + (barWidth * healthPercent),
                rect.Top - 1
            );

            // Color based on health percentage
            var healthColor = healthPercent > 0.6f ? new RawColor4(0.2f, 0.8f, 0.2f, 1.0f) : // Green
                             healthPercent > 0.3f ? new RawColor4(0.9f, 0.7f, 0.1f, 1.0f) : // Yellow
                                                   new RawColor4(0.9f, 0.2f, 0.2f, 1.0f);   // Red

            using (var healthBrushTemp = new SolidColorBrush(renderTarget, healthColor))
            {
                renderTarget.FillRectangle(healthRect, healthBrushTemp);
            }
        }

        private void DrawBoneLines(UActor actor, Vector2 panelPos)
        {
            // Minimal center dot indicator
            float dotSize = 1.5f;
            var dotRect = new RawRectangleF(
                panelPos.X - dotSize, 
                panelPos.Y - dotSize, 
                panelPos.X + dotSize, 
                panelPos.Y + dotSize);
            
            var dotBrush = actor.IsFriendly() ? friendlyBrush : boneBrush;
            renderTarget.FillRectangle(dotRect, dotBrush);
        }


        private void DrawVehicleBox(UActor actor, Vector2 panelPos, float distance)
        {
            var vehicleName = ActorTypeNames.ContainsKey(actor.ActorType) ? ActorTypeNames[actor.ActorType] : actor.ActorType.ToString();
            var text = $"{vehicleName} ({distance:F0}m)";

            // Slim vehicle box with appropriate color
            var rect = new RawRectangleF(panelPos.X - 25, panelPos.Y - 12, panelPos.X + 25, panelPos.Y + 12);
            var vehBrush = actor.IsFriendly() ? friendlyBrush : vehicleBrush;
            renderTarget.DrawRectangle(rect, vehBrush, 1.0f);
            
            // Small text below vehicle
            using (var smallTextFormat = new SharpDX.DirectWrite.TextFormat(new SharpDX.DirectWrite.Factory(), "Segoe UI", 8.0f))
            {
                renderTarget.DrawText(text, smallTextFormat, 
                    new RawRectangleF(rect.Left, rect.Bottom + 1, rect.Left + 150, rect.Bottom + 15), vehBrush);
            }
        }

        private bool IsVehicle(ActorType type)
        {
            return VehicleTypes.Contains(type);
        }

        /// <summary>
        /// Refresh colors and settings when config changes
        /// </summary>
        public void RefreshSettings()
        {
            if (renderTarget != null)
            {
                // Dispose old brushes
                brush?.Dispose();
                vehicleBrush?.Dispose();
                healthBrush?.Dispose();
                friendlyBrush?.Dispose();

                // Create new brushes with updated colors
                brush = new SolidColorBrush(renderTarget, new RawColor4(
                    Program.Config.EspTextColor.R / 255f,
                    Program.Config.EspTextColor.G / 255f,
                    Program.Config.EspTextColor.B / 255f,
                    Program.Config.EspTextColor.A / 255f));
                    
                vehicleBrush = new SolidColorBrush(renderTarget, new RawColor4(
                    Program.Config.EspTextColor.R / 255f,
                    Program.Config.EspTextColor.G / 255f,
                    Program.Config.EspTextColor.B / 255f,
                    Program.Config.EspTextColor.A / 255f));
                    
                boneBrush = brush;
                healthBrush = new SolidColorBrush(renderTarget, new RawColor4(1.0f, 0.0f, 0.0f, 1.0f));
                friendlyBrush = new SolidColorBrush(renderTarget, new RawColor4(0f / 255f, 187f / 255f, 254f / 255f, 1.0f));
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            running = false;
            
            // Give render thread time to finish current frame
            Thread.Sleep(20);
            
            lock (_renderLock)
            {
                renderTarget?.Dispose();
                brush?.Dispose();
                vehicleBrush?.Dispose();
                healthBrush?.Dispose();
                friendlyBrush?.Dispose();
                crosshairBrush?.Dispose();
                textFormat?.Dispose();
            }
            base.OnHandleDestroyed(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            
            // Enforce size limits to prevent crashes
            if (this.Width > _maxWidth || this.Height > _maxHeight)
            {
                int safeWidth = Math.Min(this.Width, _maxWidth);
                int safeHeight = Math.Min(this.Height, _maxHeight);
                this.Size = new Size(safeWidth, safeHeight);
                return; // Exit early, OnResize will be called again with safe size
            }
            
            if (renderTarget != null && _contentPanel != null && 
                _contentPanel.Width > 0 && _contentPanel.Height > 0 &&
                _contentPanel.Width <= _maxWidth && _contentPanel.Height <= _maxHeight)
            {
                try
                {
                    renderTarget.Resize(new Size2(_contentPanel.Width, _contentPanel.Height));
                }
                catch (SharpDX.SharpDXException)
                {
                    // If resize fails, recreate the render target
                    DisposeRenderTarget();
                    InitializeDirect2D();
                }
                catch (System.AccessViolationException)
                {
                    // Critical error - dispose and recreate everything
                    DisposeRenderTarget();
                    InitializeDirect2D();
                }
            }
        }

    }

    // Designer part
    partial class AimviewPanel
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ResumeLayout(false);
        }
    }
}
