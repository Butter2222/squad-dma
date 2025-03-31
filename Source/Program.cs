using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace squad_dma {
    static class Program {
        private static readonly Mutex _mutex;
        private static readonly bool _singleton;
        private static readonly Config _config;
        
        public static Config Config
        {
            get => _config;
        }

        #region Static Constructor
        static Program()
        {
            _mutex = new Mutex(true, "9A19103F-16F7-4668-BE54-9A1E7A4F7997", out _singleton);

            if (Config.TryLoadConfig(out _config) is not true)
                _config = new Config();
        }
        #endregion

        #region Program Entry Point
        [STAThread]
        private static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.Unicode;
            try
            {
                if (_singleton)
                {
                    RuntimeHelpers.RunClassConstructor(typeof(Memory).TypeHandle); // Initialize Memory
                    ApplicationConfiguration.Initialize();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(true);

                    // Wait for the Memory worker to initialize and find the game
                    while (!Memory.Ready)
                    {
                        Thread.Sleep(100);
                    }
                    Game game = Memory._game;
                    Application.Run(new MainForm());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Squad Radar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Public logging method, writes to Debug Trace, and a Log File (if enabled in Config.Json)
        /// </summary>
        public static void Log(string msg)
        {
            Console.WriteLine(msg);
        }
        /// <summary>
        /// Hide the 'Program Console Window'.
        /// </summary>
        public static void HideConsole()
        {
            ShowWindow(GetConsoleWindow(), 1); // 0 : SW_HIDE
        }
        #endregion

        #region P/Invokes
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();
        #endregion
    }
}