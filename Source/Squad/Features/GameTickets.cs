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
        private string _lastWarning;
        private const int MaxTeamStates = 8;

        /// <summary>Logs a problem once until it clears, the tickets are polled every UI tick.</summary>
        private void Warn(string key, string message)
        {
            if (_lastWarning == key) return;
            _lastWarning = key;
            Logger.Error($"[{NAME}] {message}");
        }

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

                // The arrays can hold more than the two playing teams (IndexedTeamStates is indexed by team ID,
                // so slot 0 is the neutral team and the real teams follow). Walk all of them and keep the real ones.
                foreach (var arrayOffset in new[] { ASQGameState.IndexedTeamStates, ASQGameState.TeamStates })
                {
                    ulong arrayPtr = Memory.ReadPtr(gameState + arrayOffset);
                    int count = arrayPtr == 0 ? 0 : Memory.ReadValue<int>(gameState + arrayOffset + 0x8);
                    if (count < 1 || count > MaxTeamStates) continue;

                    for (int i = 0; i < count; i++)
                    {
                        ulong team = Memory.ReadPtr(arrayPtr + (uint)(i * 0x8));
                        if (team == 0) continue;

                        int teamId = Memory.ReadValue<int>(team + ASQTeamState.ID);
                        if (teamId <= 0 || teamTickets.ContainsKey(teamId)) continue;

                        teamTickets[teamId] = Memory.ReadValue<int>(team + ASQTeamState.Tickets);
                    }

                    if (teamTickets.Count >= 2) break;
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
                        Warn("none", "No team tickets found");
                        return 0;
                    }
                    if (localTeamId == -1)
                    {
                        Warn("team", "Invalid local team ID for tickets");
                        return 0;
                    }
                    _lastWarning = null;
                    
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
                        Warn("none", "No team tickets found");
                        return 0;
                    }
                    if (localTeamId == -1)
                    {
                        Warn("team", "Invalid local team ID for tickets");
                        return 0;
                    }
                    
                    foreach (var team in tickets)
                    {
                        if (team.Key != localTeamId)
                            return team.Value;
                    }
                    
                    Warn("enemy", "No enemy team found in tickets");
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