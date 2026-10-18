using System;
using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// Class responsible for handling player statistics
    /// </summary>
    public class PlayerStats
    {
        public const string NAME = "PlayerStats";
        
        private readonly ulong _playerController;
        private readonly ulong _gameState;
        private bool _warned;

        /// <summary>Stats are polled every UI tick, so a missing player state is logged once.</summary>
        private void LogOnce(string message)
        {
            if (_warned) return;
            _warned = true;
            Logger.Error($"[{NAME}] {message}");
        }

        /// <summary>
        /// Constructor for GameStats
        /// </summary>
        /// <param name="playerController">Pointer to the player controller</param>
        public PlayerStats(ulong playerController)
        {
            _playerController = playerController;
        }
        
        /// <summary>
        /// Gets the player's kill count
        /// </summary>
        public int Kills
        {
            get
            {
                try
                {
                    var ptrScatter = new ScatterReadMap(1);
                    var ptrRound = ptrScatter.AddRound();

                    ptrRound.AddEntry<ulong>(0, 0, _playerController + Controller.PlayerState);
                    ptrScatter.Execute();

                    if (!ptrScatter.Results[0][0].TryGetResult<ulong>(out var playerState) || playerState == 0)
                    {
                        LogOnce("Failed to get player state");
                        return 0;
                    }

                    var dataScatter = new ScatterReadMap(1);
                    var dataRound = dataScatter.AddRound();

                    var playerStateData = playerState + ASQPlayerState.PlayerStateData;

                    dataRound.AddEntry<int>(0, 0, playerStateData + FPlayerStateDataObject.NumKills);

                    dataScatter.Execute();

                    if (!dataScatter.Results[0][0].TryGetResult<int>(out var kills))
                    {
                        Logger.Error($"[{NAME}] Failed to read kills value");
                        return 0;
                    }

                    return kills;
                }
                catch (Exception ex)
                {
                    Logger.Error($"[{NAME}] Error getting player kills: {ex.Message}");
                    return 0;
                }
            }
        }
        
        /// <summary>
        /// Gets the player's wounded count
        /// </summary>
        public int Woundeds
        {
            get
            {
                try
                {
                    var ptrScatter = new ScatterReadMap(1);
                    var ptrRound = ptrScatter.AddRound();

                    ptrRound.AddEntry<ulong>(0, 0, _playerController + Controller.PlayerState);
                    ptrScatter.Execute();

                    if (!ptrScatter.Results[0][0].TryGetResult<ulong>(out var playerState) || playerState == 0)
                    {
                        LogOnce("Failed to get player state");
                        return 0;
                    }

                    var dataScatter = new ScatterReadMap(1);
                    var dataRound = dataScatter.AddRound();

                    var playerStateData = playerState + ASQPlayerState.PlayerStateData;

                    dataRound.AddEntry<int>(0, 0, playerStateData + FPlayerStateDataObject.NumWoundeds);

                    dataScatter.Execute();

                    if (!dataScatter.Results[0][0].TryGetResult<int>(out var woundeds))
                    {
                        Logger.Error($"[{NAME}] Failed to read woundeds value");
                        return 0;
                    }

                    return woundeds;
                }
                catch (Exception ex)
                {
                    Logger.Error($"[{NAME}] Error getting player woundeds: {ex.Message}");
                    return 0;
                }
            }
        }
    }
} 