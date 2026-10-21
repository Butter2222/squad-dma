using squad_dma.Source.Misc;
using System.Collections.Concurrent;
using System.Text;
using Vmmsharp;

namespace squad_dma
{
    internal static class InputManager
    {
        private static bool _initialized = false;

        private static long _lastUpdateTicks = 0;
        private static ulong _gafAsyncKeyStateExport;

        private static byte[] _currentStateBitmap = new byte[64];
        private static byte[] _previousStateBitmap = new byte[64];
        private static readonly ConcurrentDictionary<int, byte> _pressedKeys = new ConcurrentDictionary<int, byte>();

        private static Vmm _hVMM;
        private static VmmProcess _winLogon;

        private static int _initAttempts = 0;
        private const int MAX_ATTEMPTS = 3;
        private const int DELAY = 500;
        private const int KEY_CHECK_DELAY = 30; // keys drive the free camera, so they are polled faster

        private static int _currentBuild;
        private static int _updateBuildRevision;

        private static readonly Dictionary<int, bool> _keyPressedStates = new Dictionary<int, bool>();

        public static bool IsReady => _initialized;
        private static readonly Dictionary<int, KeyStateChangedHandler> _keyEvents = new();
        private static readonly object _eventLock = new();
        static InputManager()
        {
            _hVMM = Memory.vmmInstance;

            new Thread(Worker)
            {
                IsBackground = true
            }.Start();
        }

        /// <summary>
        /// Attempts to load Input Manager.
        /// </summary>
        public static void Initialize()
        {
            if (InputManager.InitKeyboard())
                Logger.Info("InputManager Initialized");
            else
                Logger.Info("ERROR Initializing Input Manager");
        }

        private static bool InitKeyboard()
        {
            if (_initialized)
                return true;

            try
            {
                var currentBuild = _hVMM.RegValueRead("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\CurrentBuild", out _);
                _currentBuild = int.Parse(Encoding.Unicode.GetString(currentBuild));

                var UBR = _hVMM.RegValueRead("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\UBR", out _);
                _updateBuildRevision = BitConverter.ToInt32(UBR);

                var tmpProcess = _hVMM.Process("winlogon.exe");
                _winLogon = _hVMM.Process(tmpProcess.PID | Vmm.PID_PROCESS_WITH_KERNELMEMORY);

                if (_winLogon == null)
                {
                    Logger.Error("Winlogon process not found");
                    _initAttempts++;
                    return false;
                }

                return _currentBuild > 22000 ? InputManager.InitKeyboardForNewWindows() : InputManager.InitKeyboardForOldWindows();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error initializing keyboard: {ex.Message}\n{ex.StackTrace}");
                _initAttempts++;
                return false;
            }
        }

        private static VmmProcess.ModuleEntry GetModuleInfo(VmmProcess process, string moduleToFind)
        {
            var modules = process.MapModule();
            var moduleLower = moduleToFind.ToLower();

            foreach (var module in modules)
            {
                if (module.sFullName.ToLower().Contains(moduleLower))
                {
                    return module;
                }
            }

            return new VmmProcess.ModuleEntry();
        }

        private static bool InitKeyboardForNewWindows()
        {

            var csrssProcesses = _hVMM.Processes.Where(p => p.Name.Equals("csrss.exe", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var csrss in csrssProcesses)
            {
                try
                {
                    // Get win32k module info
                    if (!TryGetWin32kInfo(csrss, out ulong win32kBase, out ulong win32kSize))
                        continue;

                    // Find session globals pointer
                    if (!TryFindSessionPointer(csrss, win32kBase, win32kSize, out ulong gSessionGlobalSlots))
                        continue;

                    // Resolve user session state
                    if (!TryResolveUserSessionState(csrss, gSessionGlobalSlots, out ulong userSessionState))
                        continue;

                    // Get async key state offset
                    if (!TryGetAsyncKeyStateOffset(csrss, userSessionState, out ulong keyStateAddress))
                        continue;

                    _gafAsyncKeyStateExport = keyStateAddress;
                    _initialized = true;
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"KEYBOARD ERR: {ex.Message}\n{ex.StackTrace}");
                }
            }

            _initAttempts++;
            Logger.Error("Failed to initialize keyboard handler. Restarting host PC may help");
            return false;
        }

        private static bool TryGetWin32kInfo(VmmProcess process, out ulong baseAddress, out ulong moduleSize)
        {
            baseAddress = 0;
            moduleSize = 0;

            baseAddress = process.GetModuleBase("win32ksgd.sys");

            if (baseAddress != 0)
            {
                var moduleInfo = GetModuleInfo(process, "win32ksgd.sys");
                moduleSize = moduleInfo.cbImageSize;
                return true;
            }

            baseAddress = process.GetModuleBase("win32k.sys");

            if (baseAddress != 0)
            {
                var moduleInfo = GetModuleInfo(process, "win32k.sys");
                moduleSize = moduleInfo.cbImageSize;
                return true;
            }

            Logger.Error("Failed to get module win32k info");
            return false;
        }

        private static bool TryFindSessionPointer(VmmProcess process, ulong baseAddr, ulong size, out ulong sessionPtr)
        {
            sessionPtr = 0;

            var gSessionPtr = Memory.FindSignature("48 8B 05 ? ? ? ? 48 8B 04 C8", baseAddr, baseAddr + size, process);

            if (gSessionPtr == 0)
            {
                gSessionPtr = Memory.FindSignature("48 8B 05 ? ? ? ? FF C9", baseAddr, baseAddr + size, process);
                if (gSessionPtr == 0)
                {
                    Logger.Error("Failed to find g_session_global_slots");
                    return false;
                }
            }

            var relativeOffsetResult = process.MemReadAs<int>(gSessionPtr + 3);

            if (relativeOffsetResult.Value == 0)
            {
                Logger.Error("Failed to read relative offset");
                return false;
            }

            sessionPtr = gSessionPtr + 7 + (ulong)relativeOffsetResult.Value;

            return true;
        }

        private static bool TryResolveUserSessionState(VmmProcess process, ulong sessionPtr, out ulong sessionState)
        {
            sessionState = 0;

            for (int i = 0; i < 4; i++)
            {
                var t1 = process.MemReadAs<ulong>(sessionPtr);
                if (t1.Value == 0)
                    continue;

                var t2 = process.MemReadAs<ulong>(t1.Value + (ulong)(8 * i));
                if (t2.Value == 0)
                    continue;

                var t3 = process.MemReadAs<ulong>(t2.Value);
                if (t3.Value == 0)
                    continue;

                sessionState = t3.Value;

                if (sessionState > 0x7FFFFFFFFFFF)
                    return true;
            }

            return sessionState != 0;
        }

        private static bool TryGetAsyncKeyStateOffset(VmmProcess process, ulong sessionState, out ulong keyStateAddr)
        {
            keyStateAddr = 0;

            var win32kbaseBase = process.GetModuleBase("win32kbase.sys");

            if (win32kbaseBase == 0)
            {
                Logger.Error("Failed to get module win32kbase info");
                return false;
            }

            var win32kbaseInfo = GetModuleInfo(process, "win32kbase.sys");
            var win32kbaseSize = win32kbaseInfo.cbImageSize;

            var ptr = Memory.FindSignature(
                "48 8D 90 ? ? ? ? E8 ? ? ? ? 0F 57 C0",
                win32kbaseBase,
                win32kbaseBase + win32kbaseSize,
                process);

            if (ptr == 0)
            {
                Logger.Error("Failed to find offset for gafAsyncKeyStateExport");
                return false;
            }

            var offsetResult = process.MemReadAs<uint>(ptr + 3);

            if (offsetResult.Value == 0)
            {
                Logger.Error("Failed to read session offset");
                return false;
            }

            keyStateAddr = sessionState + offsetResult.Value;

            return keyStateAddr > 0x7FFFFFFFFFFF;
        }

        private static bool InitKeyboardForOldWindows()
        {
            Logger.Info("Older Windows version detected, attempting to resolve via EAT");

            var exports = _winLogon.MapModuleEAT("win32kbase.sys");
            var gafAsyncKeyStateExport = exports.FirstOrDefault(e => e.sFunction == "gafAsyncKeyState");

            if (!string.IsNullOrEmpty(gafAsyncKeyStateExport.sFunction) && gafAsyncKeyStateExport.vaFunction >= 0x7FFFFFFFFFFF)
            {
                _gafAsyncKeyStateExport = gafAsyncKeyStateExport.vaFunction;
                _initialized = true;
                Logger.Info("Resolved export via EAT");
                return true;
            }

            Logger.Error("Failed to resolve via EAT, attempting to resolve with PDB");

            var pdb = _winLogon.Pdb("win32kbase.sys");

            if (pdb != null && pdb.SymbolAddress("gafAsyncKeyState", out ulong gafAsyncKeyState))
            {
                if (gafAsyncKeyState >= 0x7FFFFFFFFFFF)
                {
                    _gafAsyncKeyStateExport = gafAsyncKeyState;
                    _initialized = true;
                    Logger.Info("Resolved export via PDB");
                    return true;
                }
            }

            Logger.Error("Failed to find export");
            return false;
        }

        public static unsafe void UpdateKeys()
        {
            if (!_initialized)
                return;

            Array.Copy(_currentStateBitmap, _previousStateBitmap, 64);

            fixed (byte* pb = _currentStateBitmap)
            {
                var success = _winLogon.MemRead(
                    _gafAsyncKeyStateExport,
                    pb,
                    64,
                    out _,
                    Vmm.FLAG_NOCACHE
                );

                if (!success)
                    return;

                _pressedKeys.Clear();
                for (int vk = 0; vk < 256; ++vk)
                {
                    if ((_currentStateBitmap[(vk * 2 / 8)] & 1 << vk % 4 * 2) != 0)
                        _pressedKeys.AddOrUpdate(vk, 1, (oldkey, oldvalue) => 1);
                }
                for (int vk = 0; vk < 256; ++vk)
                {
                    bool wasDown = (_previousStateBitmap[(vk * 2 / 8)] & (1 << (vk % 4 * 2))) != 0;
                    bool isDown = (_currentStateBitmap[(vk * 2 / 8)] & (1 << (vk % 4 * 2))) != 0;

                    if (wasDown != isDown)
                    {
                        lock (_eventLock)
                        {
                            if (_keyEvents.TryGetValue(vk, out var handler))
                            {
                                handler?.Invoke(null, new KeyEventArgs(vk, isDown));
                            }
                        }
                    }
                }
            }

            _lastUpdateTicks = DateTime.UtcNow.Ticks;
        }

        public static void RegisterKeyEvent(int keyCode, KeyStateChangedHandler handler)
        {
            lock (_eventLock)
            {
                if (_keyEvents.ContainsKey(keyCode))
                    _keyEvents[keyCode] += handler;
                else
                    _keyEvents[keyCode] = handler;
            }
        }

        public static void UnregisterKeyEvent(int keyCode, KeyStateChangedHandler handler)
        {
            lock (_eventLock)
            {
                if (_keyEvents.ContainsKey(keyCode))
                {
                    _keyEvents[keyCode] -= handler;
                    if (_keyEvents[keyCode] == null)
                        _keyEvents.Remove(keyCode);
                }
            }
        }

        public static bool IsKeyDown(int key)
        {
            if (!_initialized || _gafAsyncKeyStateExport < 0x7FFFFFFFFFFF)
                return false;

            var virtualKeyCode = (int)key;

            return _pressedKeys.ContainsKey(virtualKeyCode);
        }

        public static bool IsKeyPressed(int key)
        {
            if (!_initialized || _gafAsyncKeyStateExport < 0x7FFFFFFFFFFF)
                return false;

            var virtualKeyCode = (int)key;
            bool isCurrentlyPressed = _pressedKeys.ContainsKey(virtualKeyCode);
            
            // If key wasn't pressed before and is now pressed
            if (!_keyPressedStates.TryGetValue(virtualKeyCode, out bool wasPressed))
            {
                _keyPressedStates[virtualKeyCode] = isCurrentlyPressed;
                return isCurrentlyPressed;
            }
            
            // If key state changed from not pressed to pressed
            bool isNewPress = !wasPressed && isCurrentlyPressed;
            _keyPressedStates[virtualKeyCode] = isCurrentlyPressed;
            
            return isNewPress;
        }

        /// <summary>
        /// InputManager Managed thread.
        /// </summary>
        private static void Worker()
        {
            Logger.Info("InputManager thread starting...");
            while (true)
            {
                try
                {
                    if (Memory._running)
                        UpdateKeys();
                }
                catch { }
                finally
                {
                    Thread.Sleep(KEY_CHECK_DELAY);
                }
            }
        }

        public class KeyEventArgs : EventArgs
        {
            public int KeyCode { get; }
            public bool IsPressed { get; }

            public KeyEventArgs(int keyCode, bool isPressed)
            {
                KeyCode = keyCode;
                IsPressed = isPressed;
            }
        }

        public delegate void KeyStateChangedHandler(object sender, KeyEventArgs e);
    }
}