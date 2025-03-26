using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Numerics;
using Offsets;
using System.Collections.ObjectModel;

namespace squad_dma
{
    public class EspOverlay : Form
    {
        private WindowRenderTarget renderTarget;
        private SolidColorBrush brush;
        private ReadOnlyDictionary<ulong, UActor> AllActors
        {
            get => Memory.Actors;
        }
        private bool running = true;
        private Game Game => Memory._game;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_EXSTYLE = -20;

        public EspOverlay()
        {
            
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            Width = Screen.PrimaryScreen.Bounds.Width;
            Height = Screen.PrimaryScreen.Bounds.Height;
            Location = new System.Drawing.Point(0, 0);
            BackColor = System.Drawing.Color.Black;

            int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
            SetWindowLong(Handle, GWL_EXSTYLE, exStyle);

            InitializeDirect2D();
            StartRenderLoop();
        }

        private void InitializeDirect2D()
        {
            var factory = new SharpDX.Direct2D1.Factory();
            var renderProperties = new HwndRenderTargetProperties
            {
                Hwnd = Handle,
                PixelSize = new Size2(Width, Height),
                PresentOptions = PresentOptions.Immediately
            };
            renderTarget = new WindowRenderTarget(factory, new RenderTargetProperties(), renderProperties);
            brush = new SolidColorBrush(renderTarget, new RawColor4(
                Program.Config.EspTextColor.R / 255f,
                Program.Config.EspTextColor.G / 255f,
                Program.Config.EspTextColor.B / 255f,
                Program.Config.EspTextColor.A / 255f
            ));
        }

        private void StartRenderLoop()
        {
            Thread renderThread = new Thread(() =>
            {
                Program.Log("Render thread started.");
                bool wasReadyLastFrame = false; 
                while (running)
                {
                    if (Game == null)
                    {
                        Program.Log("Game instance not yet initialized, waiting...");
                        renderTarget.BeginDraw();
                        renderTarget.Clear(new RawColor4(0, 0, 0, 1));
                        renderTarget.EndDraw();
                        this.Invalidate();
                        Thread.Sleep(1500);
                        wasReadyLastFrame = false;
                        continue;
                    }

                    bool isReady = IsReadyToRender();
                    if (!isReady)
                    {
                        if (wasReadyLastFrame) 
                        {
                            Program.Log("Not ready to render, waiting...");
                            renderTarget.BeginDraw();
                            renderTarget.Clear(new RawColor4(0, 0, 0, 1));
                            renderTarget.EndDraw();
                            this.Invalidate();
                        }
                        Thread.Sleep(500); 
                        wasReadyLastFrame = false;
                        continue;
                    }

                    RenderFrame();
                    Thread.Sleep(15); // ~60 FPS
                    wasReadyLastFrame = true;
                }
                Program.Log("Render thread stopped.");
            });
            renderThread.Start();
        }

        private bool IsReadyToRender()
        {
            bool inGame = Game.InGame;
            bool localPlayerExists = Game.LocalPlayer != null;
            bool actorsExist = Game.Actors != null && Game.Actors.Count > 0;

            if (!inGame || !localPlayerExists || !actorsExist)
            {
                return false;
            }
            //Program.Log("Ready to render!");
            return true;
        }

        private void RenderFrame()
        {
            renderTarget.BeginDraw();
            renderTarget.Clear(new RawColor4(0, 0, 0, 0));

            Dictionary<ulong, UActor> actorsCopy;
            lock (((Game)Game).actorsLock) // Verrouillage pour synchronisation
            {
                if (!IsReadyToRender())
                {
                    renderTarget.EndDraw();
                    this.Invalidate();
                    return;
                }
                actorsCopy = new Dictionary<ulong, UActor>(Game.Actors); // Copie pour éviter les conflits
            }

            DrawEsp(actorsCopy); // Passer la copie à DrawEsp

            renderTarget.EndDraw();
        }

        private void DrawEsp(Dictionary<ulong, UActor> actors)
        {
            if (Game.LocalPlayer == null || actors == null || actors.Count < 1)
            {
                Program.Log("LocalPlayer ou actors non initialisés.");
                return;
            }

            var viewInfo = new MinimalViewInfo
            {
                Location = Game.LocalPlayer.Position,
                Rotation = Game.LocalPlayer.Rotation3D,
                FOV = 90f
            };

            Vector3 camPos = viewInfo.Location;
            Team localPlayerTeam = Program.Config.SelectedTeam;

            foreach (var actor in actors.Values)
            {
                if (actor.Position == Vector3.Zero) 
                    continue;

                if (!actor.IsAlive) 
                    continue;

                if (actor.ActorType != ActorType.Player)
                    continue;

                if (Vector3.Distance(Game.LocalPlayer.Position, actor.Position) < 1.0f)
                    continue;

                
                Vector2 screenPos = Camera.WorldToScreen(viewInfo, actor.Position);
                if (screenPos == Vector2.Zero) 
                    continue;

                var distance = Vector3.Distance(camPos, actor.Position) / 100f;
                if (distance > Program.Config.EspMaxDistance)
                    continue;

                if (actor.Team == localPlayerTeam)
                    continue;

                string wdistance = Program.Config.EspShowDistance ? $"[{(int)distance}m]" : "";
                string whealth = Program.Config.EspShowHealth ? $"[{(int)actor.Health}❤]" : "";
                string name = Program.Config.ShowNames ? actor.Name : "";
                string espText = $"{name}{(string.IsNullOrEmpty(name) ? "" : " ")}{wdistance}{(string.IsNullOrEmpty(wdistance) ? "" : " ")}{whealth}";

                // Rendu du texte
                brush.Color = new RawColor4(
                    Program.Config.EspTextColor.R / 255f,
                    Program.Config.EspTextColor.G / 255f,
                    Program.Config.EspTextColor.B / 255f,
                    Program.Config.EspTextColor.A / 255f
                );
                renderTarget.DrawText(
                    espText,
                    new SharpDX.DirectWrite.TextFormat(new SharpDX.DirectWrite.Factory(), "Verdana", Program.Config.ESPFontSize),
                    new RawRectangleF(screenPos.X, screenPos.Y, screenPos.X + 200, screenPos.Y + 20),
                    brush
                );
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            running = false;
            brush.Dispose();
            renderTarget.Dispose();
            base.OnClosed(e);
        }
    }
}