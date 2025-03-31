﻿using Offsets;
using System;
using System.Collections.ObjectModel;
using System.Numerics;

namespace squad_dma
{
    /// <summary>
    /// Class containing Game instance.
    /// </summary>
    public class Game
    {
        private readonly ulong _squadBase;
        private volatile bool _inGame = false;
        private RegistredActors _actors;
        private UActor _localUPlayer;
        private ulong _gameWorld;
        private ulong _gameInstance;
        private ulong _localPlayer;
        private ulong _playerController;
        private Vector3 _absoluteLocation;
        private string _currentLevel = string.Empty;
        private bool _vehiclesLogged = false;
        private DateTime _lastTeamCheck = DateTime.MinValue;
        private const int TeamCheckInterval = 1000;

        public enum GameStatus
        {
            NotFound,
            Menu,
            InGame,
        }

        #region Getters
        public bool InGame
        {
            get => _inGame;
        }

        public string MapName
        {
            get => _currentLevel;
        }

        public UActor LocalPlayer
        {
            get => _localUPlayer;
        }

        public ReadOnlyDictionary<ulong, UActor> Actors
        {
            get => _actors?.Actors;
        }

        public Vector3 AbsoluteLocation
        {
            get => _absoluteLocation;
        }

        public Dictionary<int, int> TeamTickets
        {
            get => GetTickets();
        }
        #endregion

        /// <summary>
        /// Game Constructor.
        /// </summary>
        public Game(ulong squadBase)
        {
            _squadBase = squadBase;
        }

        #region GameLoop
        /// <summary>
        /// Main Game Loop executed by Memory Worker Thread.
        /// </summary>
        public void GameLoop()
        {
            try
            {
                if (!this._inGame)
                {
                    this._vehiclesLogged = false;
                    throw new GameEnded("Game has ended!");
                }

                UpdateLocalPlayerInfo();
                this._actors.UpdateList();
                this._actors.UpdateAllPlayers();

                // LogTeamInfo();

            }
            catch (DMAShutdown)
            {
                HandleDMAShutdown();
            }
            catch (GameEnded e)
            {
                HandleGameEnded(e);
            }
            catch (Exception ex)
            {
                HandleUnexpectedException(ex);
            }
        }
        #endregion

        #region Methods

        /// <summary>
        /// Handles the scenario when DMA shutdown occurs.
        /// </summary>
        private void HandleDMAShutdown()
        {
            Program.Log("DMA shutdown");
            this._inGame = false;
        }

        /// <summary>
        /// Handles the scenario when the game ends.
        /// </summary>
        /// <param name="e">The GameEnded exception instance containing details about the game end.</param>
        private void HandleGameEnded(GameEnded e)
        {
            Program.Log("Game has ended!");

            this._inGame = false;
            Memory.GameStatus = Game.GameStatus.Menu;
            Memory.Restart();
        }

        /// <summary>
        /// Handles unexpected exceptions that occur during the game loop.
        /// </summary>
        /// <param name="ex">The exception instance that was thrown.</param>
        private void HandleUnexpectedException(Exception ex)
        {
            Program.Log($"CRITICAL ERROR - Game ended due to unhandled exception: {ex}");
            this._inGame = false;
        }

        /// <summary>
        /// Waits until game has started before returning to caller.
        /// </summary>
        /// 
        public void WaitForGame()
        {
            while (true)
            {
                try
                {
                    if (!Memory.GetModuleBase())
                    {
                        throw new GameNotRunningException("Process terminated during wait");
                    }

                    if (GetGameWorld() && GetGameInstance() && GetCurrentLevel() && InitActors() && GetLocalPlayer())
                    {
                        if (!Memory.GetModuleBase())
                        {
                            throw new GameNotRunningException("Process terminated during initialization");
                        }

                        Thread.Sleep(1000);
                        Program.Log("Game has started!!");
                        this._inGame = true;
                        Memory.GameStatus = Game.GameStatus.InGame;
                        return;
                    }
                }
                catch (GameNotRunningException)
                {
                    throw; // Propagate up to break out of wait loop
                }
                catch (Exception ex) when (IsExpectedException(ex))
                {
                    Program.Log($"Ignoring expected exception during wait: {ex.Message}");
                }

                Thread.Sleep(500);
            }
        }

        private static bool IsExpectedException(Exception ex)
        {
            return ex is NullReferenceException
                || ex is AccessViolationException
                || ex is DMAException;
        }


        /// <summary>
        /// Gets Game Object Manager ptr
        /// </summary>
        private bool GetGameWorld()
        {
            try
            {
                _gameWorld = Memory.ReadPtr(_squadBase + Offsets.GameObjects.GWorld);
                // Program.Log($"Found Game World at 0x{_gameWorld:X},\n0x{_gameWorld + 0x00F8:X}=0x16,\n0x{_gameWorld + 0x0158:X}=0x28,\n0x{_gameWorld + 0x01A8:X}=0x50,\n0x{_gameWorld + 0x0270:X}=0x370,\n0x{_gameWorld + 0x05E8:X}=0x90,\n0x{_gameWorld + 0x06D0:X}=0xC8");
                return true;
            }
            catch { return false; }
        }
        /// <summary>
        /// Gets GameInstance
        /// </summary>
        private bool GetGameInstance()
        {
            try
            {
                _gameInstance = Memory.ReadPtr(_gameWorld + Offsets.World.OwningGameInstance);
                // Program.Log($"Found Game Instance at 0x{_gameInstance:X}");
                return true;
            }
            catch { return false; }
        }
        /// <summary>
        /// Gets GameInstance
        /// </summary>
        private bool GetCurrentLevel()
        {
            try
            {
                var currentLayer = Memory.ReadPtr(_gameInstance + Offsets.GameInstance.CurrentLayer);
                var currentLevelIdPtr = currentLayer + Offsets.SQLayer.LevelID;
                var currentLevelId = Memory.ReadValue<uint>(currentLevelIdPtr);
                _currentLevel = Memory.GetNamesById([currentLevelId])[currentLevelId];
                Program.Log("Current level is " + _currentLevel);
                return true;
            }
            catch { return false; }
        }
        /// <summary>
        /// Initializes actors list
        /// </summary>
        private bool InitActors()
        {
            try
            {
                var persistentLevel = Memory.ReadPtr(_gameWorld + Offsets.World.PersistentLevel);
                // Program.Log($"Found PersistentLevel at 0x{persistentLevel:X}");
                _actors = new RegistredActors(persistentLevel);
                return true;
            }
            catch { return false; }
        }
        /// <summary>
        /// Gets LocalPlayer
        /// </summary>
        private bool GetLocalPlayer()
        {
            try
            {
                var localPlayers = Memory.ReadPtr(_gameInstance + Offsets.GameInstance.LocalPlayers);
                _localPlayer = Memory.ReadPtr(localPlayers);
                // Program.Log($"Found LocalPlayer at 0x{_localPlayer:X}");
                _localUPlayer = new UActor(_localPlayer);
                _localUPlayer.Team = Team.Unknown;
                GetPlayerController();
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Gets PlayerController
        /// </summary>
        private bool GetPlayerController()
        {
            try
            {
                _playerController = Memory.ReadPtr(_localPlayer + Offsets.UPlayer.PlayerController);

                return true;
            }
            catch { return false; }
        }

        private bool UpdateLocalPlayerInfo()
        {
            try
            {
                if ((DateTime.Now - _lastTeamCheck).TotalMilliseconds > TeamCheckInterval)
                {
                    _lastTeamCheck = DateTime.Now;

                    try
                    {
                        ulong playerState = Memory.ReadPtr(_playerController + Offsets.Controller.PlayerState);
                        ulong squadState = Memory.ReadPtr(_playerController + Offsets.PlayerController.SquadState);

                        if (playerState == 0 || squadState == 0)
                            return false;

                        int teamId = Memory.ReadValue<int>(playerState + Offsets.ASQPlayerState.TeamID);
                        int squadId = Memory.ReadValue<int>(squadState + Offsets.ASQSquadState.SquadId);

                        if (_localUPlayer.TeamID != teamId || _localUPlayer.SquadID != squadId)
                        {
                            _localUPlayer.TeamID = teamId;
                            _localUPlayer.SquadID = squadId;
                        }
                    }
                    catch { return false; }
                }

                GetCameraCache();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets CameraCache
        /// </summary>
        private bool GetCameraCache()
        {
            try
            {
                var cameraInfoScatterMap = new ScatterReadMap(1);
                var cameraManagerRound = cameraInfoScatterMap.AddRound();
                var cameraInfoRound = cameraInfoScatterMap.AddRound();

                var cameraManagerPtr = cameraManagerRound.AddEntry<ulong>(0, 0, _playerController + Offsets.PlayerController.PlayerCameraManager);
                cameraManagerRound.AddEntry<int>(0, 11, _gameWorld + Offsets.World.WorldOrigin);
                cameraManagerRound.AddEntry<int>(0, 12, _gameWorld + Offsets.World.WorldOrigin + 0x4);
                cameraManagerRound.AddEntry<int>(0, 13, _gameWorld + Offsets.World.WorldOrigin + 0x8);
                cameraInfoRound.AddEntry<Vector3>(0, 1, cameraManagerPtr, null, Offsets.Camera.CameraLocation);
                cameraInfoRound.AddEntry<Vector3>(0, 2, cameraManagerPtr, null, Offsets.Camera.CameraRotation);

                cameraInfoScatterMap.Execute();

                if (!cameraInfoScatterMap.Results[0][1].TryGetResult<Vector3>(out var location))
                {
                    return false;
                }
                if (!cameraInfoScatterMap.Results[0][2].TryGetResult<Vector3>(out var rotation))
                {
                    return false;
                }
                if (cameraInfoScatterMap.Results[0][11].TryGetResult<int>(out var absoluteX)
                && cameraInfoScatterMap.Results[0][12].TryGetResult<int>(out var absoluteY)
                && cameraInfoScatterMap.Results[0][13].TryGetResult<int>(out var absoluteZ))
                {
                    _absoluteLocation = new Vector3(absoluteX, absoluteY, absoluteZ);
                    // Program.Log(_absoluteLocation.ToString());
                }
                _localUPlayer.Position = location;
                _localUPlayer.Rotation = new Vector2(rotation.Y, rotation.X);
                _localUPlayer.Rotation3D = rotation;
                return true;
            }
            catch { return false; }
        }

        public void LogVehicles(bool force = false)
        {
            if (!force && _vehiclesLogged)
                return;

            var actorBaseWithName = this._actors.GetActorBaseWithName();
            if (actorBaseWithName.Count > 0)
            {
                var names = Memory.GetNamesById([.. actorBaseWithName.Values.Distinct()]);

                foreach (var nameEntry in names)
                {
                    if (!nameEntry.Value.StartsWith("BP_Soldier"))
                    {
                        Program.Log($"{nameEntry.Key} {nameEntry.Value}");
                    }
                }
                _vehiclesLogged = !force; // Reset only if not forced
            }
            else
            {
                Program.Log("No entries found.");
            }
        }

        public Dictionary<int, int> GetTickets()
        {
            var teamTickets = new Dictionary<int, int>();

            try
            { 

                ulong gameState = Memory.ReadPtr(_gameWorld + Offsets.World.GameState);
                if (gameState == 0)
                    return teamTickets;

                var scatterMap = new ScatterReadMap(1);
                var round = scatterMap.AddRound();

                round.AddEntry<ulong>(0, 0, gameState + Offsets.ASQGameState.TeamStates); // TeamStates array ptr
                round.AddEntry<int>(0, 1, gameState + Offsets.ASQGameState.TeamStates + 0x8); // TeamCount

                scatterMap.Execute();

                if (!scatterMap.Results[0][0].TryGetResult<ulong>(out var teamStatesArray) || teamStatesArray == 0)
                    return teamTickets;
                if (!scatterMap.Results[0][1].TryGetResult<int>(out var teamCount) || teamCount < 2)
                    return teamTickets;

                var teamScatter = new ScatterReadMap(2);
                var teamRound = teamScatter.AddRound();

                teamRound.AddEntry<ulong>(0, 0, teamStatesArray); // Team1
                teamRound.AddEntry<ulong>(1, 1, teamStatesArray + 0x8); // Team2

                teamScatter.Execute();

                if (teamScatter.Results[0][0].TryGetResult<ulong>(out var team1) && team1 != 0)
                {
                    int team1Id = Memory.ReadValue<int>(team1 + Offsets.ASQTeamState.ID);
                    int team1Tickets = Memory.ReadValue<int>(team1 + Offsets.ASQTeamState.Tickets);
                    teamTickets[team1Id] = team1Tickets;
                }

                if (teamScatter.Results[1][1].TryGetResult<ulong>(out var team2) && team2 != 0)
                {
                    int team2Id = Memory.ReadValue<int>(team2 + Offsets.ASQTeamState.ID);
                    int team2Tickets = Memory.ReadValue<int>(team2 + Offsets.ASQTeamState.Tickets);
                    teamTickets[team2Id] = team2Tickets;
                }
            }
            catch { /* Silently fail */ }

            return teamTickets;
        }

        public (int FriendlyTickets, int EnemyTickets) GetTeamTickets()
        {
            var tickets = GetTickets();
            int localTeamId = _localUPlayer?.TeamID ?? -1;

            if (tickets.Count == 0 || localTeamId == -1)
                return (0, 0);

            int friendly = 0;
            int enemy = 0;

            foreach (var team in tickets)
            {
                if (team.Key == localTeamId)
                    friendly = team.Value;
                else
                    enemy = team.Value;
            }

            return (friendly, enemy);
        }

        public (int Kills, int Woundeds) GetStats()
        {
            try
            {
                var ptrScatter = new ScatterReadMap(1);
                var ptrRound = ptrScatter.AddRound();

                ptrRound.AddEntry<ulong>(0, 0, _playerController + Offsets.Controller.PlayerState);
                ptrScatter.Execute();

                if (!ptrScatter.Results[0][0].TryGetResult<ulong>(out var playerState) || playerState == 0)
                    return (0, 0);

                var dataScatter = new ScatterReadMap(1);
                var dataRound = dataScatter.AddRound();

                var playerStateData = playerState + Offsets.ASQPlayerState.PlayerStateData;

                dataRound.AddEntry<int>(0, 0, playerStateData + Offsets.FPlayerStateDataObject.NumKills);
                dataRound.AddEntry<int>(0, 2, playerStateData + Offsets.FPlayerStateDataObject.NumWoundeds);

                dataScatter.Execute();

                if (!dataScatter.Results[0][0].TryGetResult<int>(out var kills) ||
                    !dataScatter.Results[0][2].TryGetResult<int>(out var woundeds))
                {
                    return (0, 0);
                }

                return (kills, woundeds);
            }
            catch
            {
                return (0, 0);
            }
        }

        public void LogTeamInfo()
        {
            if (!_inGame) return;

            Program.Log($"=== TEAM INFO DUMP ===");
            Program.Log($"Local Player TeamID: {_localUPlayer.TeamID}");

            if (_actors?.Actors == null) return;

            foreach (var actor in _actors.Actors.Values)
            {
                if (actor.ActorType == ActorType.Player)
                {
                    Program.Log($"Actor: {actor.Name} | TeamID: {actor.TeamID} | Health: {actor.Health} | Position: {actor.Position}");
                }
            }
            Program.Log($"======================");
        }

    }
    #endregion


    #region Exceptions
    public class GameNotRunningException : Exception
    {
        public GameNotRunningException()
        {
        }

        public GameNotRunningException(string message)
            : base(message)
        {
        }

        public GameNotRunningException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class GameEnded : Exception
    {
        public GameEnded()
        {

        }

        public GameEnded(string message)
            : base(message)
        {
        }

        public GameEnded(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
    #endregion
}