using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Vmmsharp;
using static Vmmsharp.Vmm;

namespace squad_dma
{
    internal static class Memory
    {
        /// <summary>
        /// Adjust this to achieve desired mem/sec performance. Higher = slower, Lower = faster.
        /// </summary>
        public static Vmm vmmInstance;
        private static VmmProcess _process;
        private static volatile bool _running = false;
        private static volatile bool _restart = false;
        private static volatile bool _ready = false;
        private static Thread _workerThread;
        private static CancellationTokenSource _workerCancellationTokenSource;
        private static uint _pid;
        private static ulong _squadBase;
        public static Game _game;
        private static int _ticksCounter = 0;
        private static volatile int _ticks = 0;
        private static readonly Stopwatch _tickSw = new();
        private static readonly ManualResetEvent _syncProcessRunning = new(false);
        private static readonly Stopwatch _processCheckTimer = new();
        private const int PROCESS_CHECK_INTERVAL = 2000;

        public static Game.GameStatus GameStatus = Game.GameStatus.NotFound;

        #region Getters
        public static int Ticks
        {
            get => _ticks;
        }
        public static bool InGame
        {
            get => _game?.InGame ?? false;
        }
        public static bool Ready
        {
            get => _ready;
        }

        public static string MapName
        {
            get => _game?.MapName;
        }

        public static UActor LocalPlayer
        {
            get => _game?.LocalPlayer;
        }

        public static ReadOnlyDictionary<ulong, UActor> Actors
        {
            get => _game?.Actors;
        }

        public static Vector3 AbsoluteLocation
        {
            get => _game.AbsoluteLocation;
        }
        #endregion

        #region Startup
        /// <summary>
        /// Constructor
        /// </summary>
        static Memory()
        {
            try
            {
                Program.Log($"Startup sequence initializing");

                if (!File.Exists("mmap.txt"))
                {
                    Program.Log($"Memory map not found - generating new map");
                    GenerateMMap();
                    Program.Log($"Memory map generation completed");
                }
                else
                {
                    Program.Log($"Existing memory map found - loading");
                    vmmInstance = new Vmm("-device", "fpga", "-memmap", "mmap.txt");
                    Program.Log($"Memory map loaded successfully");
                }

                InitiateMemoryWorker();
            }
            catch (Exception ex)
            {
                try
                {
                    Program.Log("attempting to regenerate mmap...");

                    if (File.Exists("mmap.txt"))
                        File.Delete("mmap.txt");

                    GenerateMMap();
                    InitiateMemoryWorker();
                }
                catch
                {
                    MessageBox.Show(ex.ToString(), "DMA Init", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(-1);
                }
            }
        }

        private static void InitiateMemoryWorker()
        {
            Program.Log("Starting Memory worker thread...");
            Memory.StartMemoryWorker();
            Program.HideConsole();
            Memory._tickSw.Start();
            // if you have issues with this initlizting and are on Windows 24h2 please just disable these lines below. 
            // dont have time or knowledge to fix. :(
            InputManager.SetVmmInstance(Memory.vmmInstance);
            InputManager.InitInputManager();
        }

        private static void GenerateMMap()
        {
            vmmInstance = new Vmm("-device", "fpga", "-waitinitialize");
            GetMemMap();
        }

        /// <summary>
        /// Generates a Physical Memory Map (mmap.txt) to enhance performance/safety.
        /// </summary>
        private static void GetMemMap()
        {
            try
            {
                var map = vmmInstance.MapMemory();
                if (map.Length == 0) throw new Exception("Map_GetPhysMem() returned no entries!");
                var sb = new StringBuilder();
                for (int i = 0; i < map.Length; i++)
                {
                    sb.AppendLine($"{i.ToString("D4")}  {map[i].pa.ToString("x")}  -  {(map[i].pa + map[i].cb - 1).ToString("x")}  ->  {map[i].pa.ToString("x")}");
                }
                File.WriteAllText("mmap.txt", sb.ToString());
            }
            catch (Exception ex)
            {
                throw new DMAException("Unable to generate MemMap!", ex);
            }
        }

        /// <summary>
        /// Gets Squad Process ID.
        /// </summary>
        private static bool GetPid()
        {
            try
            {
                ThrowIfDMAShutdown();
                _process = vmmInstance.Process("SquadGame.exe");

                if (_process is null)
                    throw new DMAException("Unable to obtain PID. Game is not running.");
                else
                {
                    //Program.Log($"SquadGame.exe is running at PID {_process.PID}");
                    return true;
                }
            }
            catch (DMAShutdown) { throw; }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets module base entry address
        /// </summary>
        public static bool GetModuleBase()
        {
            try
            {
                ThrowIfDMAShutdown();
                _squadBase = _process.GetModuleBase("SquadGame.exe");
                if (_squadBase == 0) throw new DMAException("Unable to obtain Base Module Address. Game may not be running");
                // else
                // {
                //     Program.Log($"Found SquadGame.exe at 0x{_squadBase.ToString("x")}");
                //     return true;
                // }
                return true;
            }
            catch (DMAShutdown) { throw; }
            catch (Exception ex)
            {
                return false;
            }
        }
        #endregion

        #region MemoryThread
        private static void StartMemoryWorker()
        {
            if (Memory._workerThread is not null && Memory._workerThread.IsAlive)
            {
                return;
            }

            Memory._workerCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = Memory._workerCancellationTokenSource.Token;

            Memory._workerThread = new Thread(() => Memory.MemoryWorkerThread(cancellationToken))
            {
                Priority = ThreadPriority.BelowNormal,
                IsBackground = true
            };
            Memory._running = true;
            Memory._workerThread.Start();
        }

        public static async void StopMemoryWorker()
        {
            await Task.Run(() =>
            {
                if (Memory._workerCancellationTokenSource is not null)
                {
                    Memory._workerCancellationTokenSource.Cancel();
                    Memory._workerCancellationTokenSource.Dispose();
                    Memory._workerCancellationTokenSource = null;
                }

                if (Memory._workerThread is not null)
                {
                    Memory._workerThread.Join();
                    Memory._workerThread = null;
                }
            });
        }

        private static void MemoryWorkerThread(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Memory.MemoryWorker();
                }
                catch { }

            }
            Program.Log("[Memory] Refresh thread stopped.");
        }

        private static bool VerifyRunningProcess()
        {
            try
            {
                if (!GetModuleBase())
                {
                    Program.Log($"Process {_pid} is no longer running!");
                    return false;
                }

                var scatterMap = new ScatterReadMap(2);
                var baseCheckRound = scatterMap.AddRound();
                baseCheckRound.AddEntry<ulong>(0, 0, _squadBase);
                baseCheckRound.AddEntry<string>(0, 1, _squadBase, 32); // Read module header

                scatterMap.Execute();

                if (!scatterMap.Results[0][1].TryGetResult<string>(out var moduleHeader) ||
                    !moduleHeader.StartsWith("MZ"))
                {
                    Program.Log("Module header verification failed!");
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Main worker to perform DMA Reads on.
        /// </summary>
        private static void MemoryWorker()
        {
            try
            {
                while (true)
                {
                    Program.Log("Attempting to find Squad Process...");
                    bool firstAttempt = true;
                    while (!Memory.GetPid() || !Memory.GetModuleBase())
                    {
                        Memory.GameStatus = Game.GameStatus.NotFound;
                        _syncProcessRunning.Reset();
                        var delay = firstAttempt ? 15000 : 5000;
                        Program.Log($"Squad startup failed, trying again in {delay / 1000} seconds...");
                        Thread.Sleep(delay);
                        firstAttempt = false;
                    }

                    Program.Log("Squad process located! Startup successful.");
                    _syncProcessRunning.Set();
                    _processCheckTimer.Restart();

                    while (true)
                    {
                        Memory._game = new Game(Memory._squadBase);
                        try
                        {
                            Program.Log("Ready -- Waiting for game...");
                            Memory.GameStatus = Game.GameStatus.Menu;
                            Memory._ready = true;
                            Memory._game.WaitForGame();

                            while (Memory.GameStatus == Game.GameStatus.InGame && _running)
                            {
                                // Periodic process verification
                                if (_processCheckTimer.ElapsedMilliseconds > PROCESS_CHECK_INTERVAL)
                                {
                                    if (!VerifyRunningProcess())
                                    {
                                        Program.Log("Game process verification failed!");
                                        throw new GameNotRunningException();
                                    }
                                    _processCheckTimer.Restart();
                                }

                                if (Memory._restart)
                                {
                                    HandleRestart();
                                    break;
                                }

                                Memory._game.GameLoop();
                                Thread.SpinWait(1000);
                            }
                        }
                        catch (GameNotRunningException) { break; }
                        catch (ThreadInterruptedException) { throw; }
                        catch (DMAShutdown) { throw; }
                        catch (Exception ex)
                        {
                            Program.Log($"Game loop error: {ex.Message}");
                        }
                        finally
                        {
                            Memory._ready = false;
                            Thread.Sleep(100);
                        }
                    }
                    Program.Log("Game is no longer running! Attempting to restart...");
                }
            }
            catch (ThreadInterruptedException) { }
            catch (DMAShutdown) { }
            catch (Exception ex)
            {
                Environment.FailFast($"FATAL ERROR on Memory Thread: {ex}");
            }
            finally
            {
                Program.Log("Uninitializing DMA Device...");
                Memory.vmmInstance.Dispose();
                Program.Log("Memory Thread closing down gracefully...");
            }
        }
        #endregion

        #region ScatterRead
        /// <summary>
        /// (Base)
        /// Performs multiple reads in one sequence, significantly faster than single reads.
        /// Designed to run without throwing unhandled exceptions, which will ensure the maximum amount of
        /// reads are completed OK even if a couple fail.
        /// </summary>
        /// <param name="pid">Process ID to read from.</param>
        /// <param name="entries">Scatter Read Entries to read from for this round.</param>
        /// <param name="useCache">Use caching for this read (recommended).</param>
        internal static void ReadScatter(ReadOnlySpan<IScatterEntry> entries)
        {
            var scatter = _process.Scatter_Initialize(Vmm.FLAG_NOCACHE);
            if (scatter == null)
            {
                throw new DMAException("Failed to initialize scatter handle");
            }

            try
            {
                foreach (var entry in entries)
                {
                    if (entry is null)
                        continue;

                    ulong addr = entry.ParseAddr();
                    uint size = (uint)entry.ParseSize();

                    if (addr == 0x0 || size == 0)
                    {
                        entry.IsFailed = true;
                        continue;
                    }

                    ulong readAddress = addr + entry.Offset;
                    scatter.Prepare(readAddress, size);
                }

                scatter.Execute();

                foreach (var entry in entries)
                {
                    if (entry is null || entry.IsFailed)
                        continue;

                    ulong readAddress = (ulong)entry.Addr + entry.Offset;
                    uint size = (uint)(int)entry.Size;

                    byte[] buffer = scatter.Read(readAddress, size);

                    if (buffer == null || buffer.Length != size)
                    {
                        entry.IsFailed = true;
                    }
                    else
                    {
                        entry.SetResult(buffer);
                    }
                }
            }
            finally
            {
                scatter.Close();
            }
        }
        #endregion

        #region ReadMethods
        /// <summary>
        /// Read memory into a Span.
        /// </summary>
        public static Span<byte> ReadBuffer(ulong addr, int size)
        {
            if ((uint)size > PAGE_SIZE * 1500) throw new DMAException("Buffer length outside expected bounds!");
            ThrowIfDMAShutdown();
            var buf = _process.MemRead(addr, (uint)size, Vmm.FLAG_NOCACHE);
            if (buf.Length != size) throw new DMAException("Incomplete memory read!");
            return new Span<byte>(buf);
        }


        /// <summary>
        /// Read a chain of pointers and get the final result.
        /// </summary>
        public static ulong ReadPtrChain(ulong ptr, uint[] offsets)
        {
            ulong addr = 0;
            try { addr = ReadPtr(ptr + offsets[0]); }
            catch (Exception ex) { throw new DMAException($"ERROR reading pointer chain at index 0, addr 0x{ptr.ToString("X")} + 0x{offsets[0].ToString("X")}", ex); }
            for (int i = 1; i < offsets.Length; i++)
            {
                try { addr = ReadPtr(addr + offsets[i]); }
                catch (Exception ex) { throw new DMAException($"ERROR reading pointer chain at index {i}, addr 0x{addr.ToString("X")} + 0x{offsets[i].ToString("X")}", ex); }
            }
            return addr;
        }
        /// <summary>
        /// Resolves a pointer and returns the memory address it points to.
        /// </summary>
        public static ulong ReadPtr(ulong ptr)
        {
            var addr = ReadValue<ulong>(ptr);
            if (addr == 0x0) throw new NullPtrException();
            else return addr;
        }

        /// <summary>
        /// Resolves a pointer and returns the memory address it points to.
        /// </summary>
        public static ulong ReadPtrNullable(ulong ptr)
        {
            var addr = ReadValue<ulong>(ptr);
            return addr;
        }

        /// <summary>
        /// Read value type/struct from specified address.
        /// </summary>
        /// <typeparam name="T">Specified Value Type.</typeparam>
        /// <param name="addr">Address to read from.</param>
        public static T ReadValue<T>(ulong addr) where T : struct
        {
            try
            {
                int size = Marshal.SizeOf(typeof(T));
                ThrowIfDMAShutdown();
                var buf = _process.MemRead(addr, (uint)size, Vmm.FLAG_NOCACHE);
                return MemoryMarshal.Read<T>(buf);
            }
            catch (Exception ex)
            {
                throw new DMAException($"ERROR reading {typeof(T)} value at 0x{addr:X}", ex);
            }
        }

        /// <summary>
        /// Read null terminated string.
        /// </summary>
        /// <param name="length">Number of bytes to read.</param>
        /// <exception cref="DMAException"></exception>
        public static string ReadString(ulong addr, uint length = 256)
        {
            try
            {
                if (length > PAGE_SIZE)
                    throw new DMAException("String length outside expected bounds!");

                ThrowIfDMAShutdown();
                var buf = _process.MemRead(addr, length, Vmm.FLAG_NOCACHE);
                int nullTerminator = Array.IndexOf<byte>(buf, 0);

                return nullTerminator != -1
                    ? Encoding.Default.GetString(buf, 0, nullTerminator)
                    : Encoding.Default.GetString(buf);
            }
            catch (Exception ex)
            {
                throw new DMAException($"ERROR reading string at 0x{addr:X}", ex);
            }
        }

        public static Dictionary<uint, string> GetNamesById(List<uint> addresses)
        {
            var count = addresses.Count;
            var firstNameScatterMap = new ScatterReadMap(count);
            var namePoolChunkRound = firstNameScatterMap.AddRound();
            var nameEntryRound = firstNameScatterMap.AddRound();
            var fnamePoolAddr = _squadBase + Offsets.GameObjects.GNames;

            for (int i = 0; i < count; i++)
            {
                var nameId = addresses[i];
                var chunkOffset = nameId >> 16;
                var nameOffset = (ushort)nameId;

                var namePoolChunkAddr = namePoolChunkRound.AddEntry<ulong>(i, 0, fnamePoolAddr + ((ulong)chunkOffset + 2) * 8); // todo optimize by chunks
                var nameEntryAddr = nameEntryRound.AddEntry<ushort>(i, 1, namePoolChunkAddr, null, 2 * (uint)nameOffset);
            }

            firstNameScatterMap.Execute();
            var finalNameScatterMap = new ScatterReadMap(count);
            var nameRound = finalNameScatterMap.AddRound();

            for (int i = 0; i < count; i++)
            {
                if (!firstNameScatterMap.Results[i][0].TryGetResult<ulong>(out var namePoolChunkAddr))
                    continue;
                if (!firstNameScatterMap.Results[i][1].TryGetResult<ushort>(out var nameEntry))
                    continue;

                var playerNameOffset = addresses[i];
                var nameOffset = (ushort)playerNameOffset;
                var entryOffset = namePoolChunkAddr + 2 * (uint)nameOffset;
                var nameLength = (int)(nameEntry >> 6);

                if (nameLength > 256)
                {
                    nameLength = 255;
                }

                var name = nameRound.AddEntry<string>(i, 0, entryOffset + 0x2, nameLength);
            }

            finalNameScatterMap.Execute();

            var result = new Dictionary<uint, string>();
            for (int i = 0; i < count; i++)
            {
                if (finalNameScatterMap.Results[i].Count < 1)
                {
                    continue;
                }
                if (!finalNameScatterMap.Results[i][0].TryGetResult<string>(out var name))
                {
                    continue;
                }
                result.Add(addresses[i], name);
            }
            return result;
        }
        #endregion

        #region WriteMethods

        /// <summary>
        /// (Base)
        /// Write value type/struct to specified address.
        /// </summary>
        /// <typeparam name="T">Value Type to write.</typeparam>
        /// <param name="pid">Process ID to write to.</param>
        /// <param name="addr">Virtual Address to write to.</param>
        /// <param name="value"></param>
        /// <exception cref="DMAException"></exception>
        public static void WriteValue<T>(ulong addr, T value)
            where T : unmanaged
        {            
             try
             {
                 if (!vmmInstance.MemWriteStruct( addr, value))
                     throw new Exception("Memory Write Failed!");
             }
             catch (Exception ex)
             {
                 throw new DMAException($"[DMA] ERROR writing {typeof(T)} value at 0x{addr.ToString("X")}", ex);
             }
        }

        /// <summary>
        /// Performs multiple memory write operations in a single call
        /// </summary>
        /// <param name="entries">A collection of entries defining the memory writes.</param>
        public static void WriteScatter(IEnumerable<IScatterWriteEntry> entries)
        {            
             using (var scatter = vmmInstance.Scatter_Initialize(Vmm.FLAG_NOCACHE))
             {
                 if (scatter == null)
                     throw new InvalidOperationException("Failed to initialize scatter.");

                 foreach (var entry in entries)
                 {
                     bool success = entry switch
                     {
                         IScatterWriteDataEntry<int> intEntry => scatter.PrepareWriteStruct(intEntry.Address, intEntry.Data),
                         IScatterWriteDataEntry<float> floatEntry => scatter.PrepareWriteStruct(floatEntry.Address, floatEntry.Data),
                         IScatterWriteDataEntry<ulong> ulongEntry => scatter.PrepareWriteStruct(ulongEntry.Address, ulongEntry.Data),
                         IScatterWriteDataEntry<bool> boolEntry => scatter.PrepareWriteStruct(boolEntry.Address, boolEntry.Data),
                         IScatterWriteDataEntry<byte> byteEntry => scatter.PrepareWriteStruct(byteEntry.Address, byteEntry.Data),
                         _ => throw new NotSupportedException($"Unsupported data type: {entry.GetType()}")
                     };

                     if (!success)
                     {
                         Program.Log($"Failed to prepare scatter write for address: {entry.Address}");
                         continue;
                     }
                 }

                 if (!scatter.Execute())
                     throw new Exception("Scatter write execution failed.");

                 scatter.Close();
             }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Sets restart flag to re-initialize the game/pointers from the bottom up.
        /// </summary>
        public static void Restart()
        {
            if (InGame)
            {
                _restart = true;
            }
        }
        private static void HandleRestart()
        {
            Memory.GameStatus = Game.GameStatus.Menu;
            Program.Log("Restarting game... getting fresh GameWorld instance");
            Memory._restart = false;
        }
        /// <summary>
        /// Close down DMA Device Connection.
        /// </summary>
        public static void Shutdown()
        {
            if (_running)
            {
                Program.Log("Closing down Memory Thread...");
                _running = false;
                Memory.StopMemoryWorker();
            }
        }

        public static string GetActorClassName(ulong actorPtr)
        {
            try
            {
                var id = ReadValue<uint>(actorPtr + Offsets.Actor.ID);
                var names = GetNamesById(new uint[] { id }.ToList());
                return names.ContainsKey(id) ? names[id] : "Unknown";
            }
            catch (Exception ex)
            {
                Program.Log($"Error retrieving Actor name : {ex.Message}");
                return "Unknown";
            }
        }

        private static void ThrowIfDMAShutdown()
        {
            if (!_running) throw new DMAShutdown("Memory Thread/DMA is shutting down!");
        }

        /// Mem Align Functions Ported from Win32 (C Macros)
        private const ulong PAGE_SIZE = 0x1000;
        private const int PAGE_SHIFT = 12;

        /// <summary>
        /// The PAGE_ALIGN macro takes a virtual address and returns a page-aligned
        /// virtual address for that page.
        /// </summary>
        private static ulong PAGE_ALIGN(ulong va)
        {
            return (va & ~(PAGE_SIZE - 1));
        }
        /// <summary>
        /// The ADDRESS_AND_SIZE_TO_SPAN_PAGES macro takes a virtual address and size and returns the number of pages spanned by the size.
        /// </summary>
        private static uint ADDRESS_AND_SIZE_TO_SPAN_PAGES(ulong va, uint size)
        {
            return (uint)((BYTE_OFFSET(va) + (size) + (PAGE_SIZE - 1)) >> PAGE_SHIFT);
        }

        /// <summary>
        /// The BYTE_OFFSET macro takes a virtual address and returns the byte offset
        /// of that address within the page.
        /// </summary>
        private static uint BYTE_OFFSET(ulong va)
        {
            return (uint)(va & (PAGE_SIZE - 1));
        }
        #endregion
    }

    #region Exceptions
    public class DMAException : Exception
    {
        public DMAException()
        {
        }

        public DMAException(string message)
            : base(message)
        {
        }

        public DMAException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class NullPtrException : Exception
    {
        public NullPtrException()
        {
        }

        public NullPtrException(string message)
            : base(message)
        {
        }

        public NullPtrException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class DMAShutdown : Exception
    {
        public DMAShutdown()
        {
        }

        public DMAShutdown(string message)
            : base(message)
        {
        }

        public DMAShutdown(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
    #endregion
}