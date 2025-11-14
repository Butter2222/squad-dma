using System;
using System.Collections.Generic;
using Offsets;
using squad_dma.Source.Misc;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// Class responsible for handling game tickets functionality
    /// </summary>
    public class GameTickets
    {
        public const string NAME = "GameTickets";
        
        private readonly ulong _gameWorld;
        private readonly UActor _localUPlayer;

        /// <summary>
        /// Constructor for GameTickets
        /// </summary>
        /// <param name="gameWorld">Pointer to the game world</param>
        /// <param name="localUPlayer">Reference to the local player</param>
        public GameTickets(ulong gameWorld, UActor localUPlayer)
        {
            _gameWorld = gameWorld;
            _localUPlayer = localUPlayer;
        }

        /// <summary>
        /// Gets all team tickets
        /// </summary>
        /// <returns>Dictionary with team IDs as keys and ticket counts as values</returns>
        public Dictionary<int, int> GetTickets()
        {
            var teamTickets = new Dictionary<int, int>();

            try
            { 
                ulong gameState = Memory.ReadPtr(_gameWorld + World.GameState);
                if (gameState == 0)
                {
                    return teamTickets;
                }

                var scatterMap = new ScatterReadMap(1);
                var round = scatterMap.AddRound();

                round.AddEntry<ulong>(0, 0, gameState + ASQGameState.TeamStates); // TeamStates array ptr
                round.AddEntry<int>(0, 1, gameState + ASQGameState.TeamStates + 0x8); // TeamCount

                scatterMap.Execute();

                if (!scatterMap.Results[0][0].TryGetResult<ulong>(out var teamStatesArray) || teamStatesArray == 0)
                {
                    return teamTickets;
                }
                if (!scatterMap.Results[0][1].TryGetResult<int>(out var teamCount) || teamCount < 2)
                {
                    return teamTickets;
                }

                var teamScatter = new ScatterReadMap(2);
                var teamRound = teamScatter.AddRound();

                teamRound.AddEntry<ulong>(0, 0, teamStatesArray); // Team1
                teamRound.AddEntry<ulong>(1, 1, teamStatesArray + 0x8); // Team2

                teamScatter.Execute();

                if (teamScatter.Results[0][0].TryGetResult<ulong>(out var team1) && team1 != 0)
                {
                    int team1Id = Memory.ReadValue<int>(team1 + ASQTeamState.ID);
                    int team1Tickets = Memory.ReadValue<int>(team1 + ASQTeamState.Tickets);
                    teamTickets[team1Id] = team1Tickets;
                }
                else
                {
                }

                if (teamScatter.Results[1][1].TryGetResult<ulong>(out var team2) && team2 != 0)
                {
                    int team2Id = Memory.ReadValue<int>(team2 + ASQTeamState.ID);
                    int team2Tickets = Memory.ReadValue<int>(team2 + ASQTeamState.Tickets);
                    teamTickets[team2Id] = team2Tickets;
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{NAME}] Error getting team tickets: {ex.Message}");
            }

            return teamTickets;
        }

        /// <summary>
        /// Gets the friendly team's ticket count
        /// </summary>
        public int FriendlyTickets
        {
            get
            {
                try
                {
                    var tickets = GetTickets();
                    int localTeamId = _localUPlayer?.TeamID ?? -1;
                    
                    if (tickets.Count == 0)
                    {
                        Logger.Error($"[{NAME}] No team tickets found for friendly team");
                        return 0;
                    }
                    if (localTeamId == -1)
                    {
                        Logger.Error($"[{NAME}] Invalid local team ID for friendly tickets");
                        return 0;
                    }
                    
                    return tickets.TryGetValue(localTeamId, out int friendlyTickets) ? friendlyTickets : 0;
                }
                catch (Exception ex)
                {
                    Logger.Error($"[{NAME}] Error getting friendly tickets: {ex.Message}");
                    return 0;
                }
            }
        }
        
        /// <summary>
        /// Gets the enemy team's ticket count
        /// </summary>
        public int EnemyTickets
        {
            get
            {
                try
                {
                    var tickets = GetTickets();
                    int localTeamId = _localUPlayer?.TeamID ?? -1;
                    
                    if (tickets.Count == 0)
                    {
                        Logger.Error($"[{NAME}] No team tickets found for enemy team");
                        return 0;
                    }
                    if (localTeamId == -1)
                    {
                        Logger.Error($"[{NAME}] Invalid local team ID for enemy tickets");
                        return 0;
                    }
                    
                    foreach (var team in tickets)
                    {
                        if (team.Key != localTeamId)
                            return team.Value;
                    }
                    
                    Logger.Error($"[{NAME}] No enemy team found in tickets");
                    return 0;
                }
                catch (Exception ex)
                {
                    Logger.Error($"[{NAME}] Error getting enemy tickets: {ex.Message}");
                    return 0;
                }
            }
        }
    }
} 