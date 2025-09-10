namespace squad_dma.Source.Squad.Debug
{
    using squad_dma.Source.Misc;
    using squad_dma;

    public class DebugVehicles
    {
        private readonly ulong _playerController;
        private readonly bool _inGame;
        private readonly RegisteredActors _actors;
        private bool _vehiclesLogged;


        public DebugVehicles(ulong playerController, bool inGame, RegisteredActors actors)
        {
            _playerController = playerController;
            _inGame = inGame;
            _actors = actors;
            _vehiclesLogged = false;
        }

        public void LogVehicles(bool force = false)
        {
            if (!force && _vehiclesLogged)
                return;

            var actorBaseWithName = _actors.GetActorBaseWithName();
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

        // Grab Vehicle Team ID
        public void VehicleTeam()
        {
            if (!_inGame || _playerController == 0) return;

            try
            {
                // Get local player info
                ulong playerState = Memory.ReadPtr(_playerController + Offsets.Controller.PlayerState);
                if (playerState == 0)
                {
                    Program.Log("Failed to get player state");
                    return;
                }

                // Get local team
                int localTeamId = Memory.ReadValue<int>(playerState + Offsets.ASQPlayerState.TeamID);
                Program.Log($"Local Team ID: {localTeamId}");

                // Get all actors and filter for vehicles
                var vehicles = _actors.Actors.Where(actor => 
                    actor.Value.Name.Contains("BP_") && 
                    !actor.Value.Name.Contains("BP_Soldier") &&
                    !actor.Value.Name.Contains("BP_PlayerStart"))
                    .ToList();

                Program.Log($"Found {vehicles.Count} vehicles");

                foreach (var vehicle in vehicles)
                {
                    try
                    {
                        var vehicleBase = vehicle.Value.Base;
                        var vehicleName = vehicle.Value.Name;

                        // Read vehicle team ID
                        int teamId = -1;
                        ulong claimedBySquad = Memory.ReadPtr(vehicleBase + Offsets.SQVehicle.ClaimedBySquad);
                        if (claimedBySquad != 0)
                        {
                            teamId = Memory.ReadValue<int>(claimedBySquad + Offsets.ASQSquadState.TeamId);
                        }

                        bool isEnemy = teamId != -1 && teamId != localTeamId;

                        Program.Log($"Vehicle: {vehicleName}");
                        Program.Log($"  Team ID: {teamId}");
                        Program.Log($"  Enemy: {isEnemy}");
                        Program.Log("---");
                    }
                    catch (Exception ex)
                    {
                        Program.Log($"Error processing vehicle {vehicle.Value.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error in VehicleTeam: {ex.Message}");
            }
        }

        public void ListVehicles()
        {
            if (!_inGame || _playerController == 0) return;

            try
            {
                var actorBaseWithName = _actors.GetActorBaseWithName();
                if (actorBaseWithName.Count > 0)
                {
                    var names = Memory.GetNamesById([.. actorBaseWithName.Values.Distinct()]);
                    var actorList = new List<(uint id, string name, ActorType? type, bool isKnown)>();
                    var knownActors = new List<(uint id, string name, ActorType type)>();
                    var unknownActors = new List<(uint id, string name)>();

                    Program.Log("\n=== Complete Actor List (Excluding Players) ===");

                    foreach (var nameEntry in names)
                    {
                        // Filter out only players (BP_Soldier)
                        if (!nameEntry.Value.StartsWith("BP_Soldier"))
                        {
                            // Try to get actor type from TechNames
                            ActorType? actorType = null;
                            bool isKnown = false;
                            
                            if (Names.TechNames.TryGetValue(nameEntry.Value, out var type))
                            {
                                actorType = type;
                                isKnown = true;
                                knownActors.Add((nameEntry.Key, nameEntry.Value, type));
                            }
                            else
                            {
                                unknownActors.Add((nameEntry.Key, nameEntry.Value));
                            }

                            actorList.Add((nameEntry.Key, nameEntry.Value, actorType, isKnown));
                        }
                    }

                    Program.Log($"Total Actors Found: {actorList.Count}");
                    Program.Log($"Known Actors: {knownActors.Count}");
                    Program.Log($"Unknown Actors: {unknownActors.Count}\n");

                    // Display all actors with status indicators
                    var sortedActors = actorList.OrderBy(a => a.name).ToList();

                    Program.Log("=== ALL ACTORS ===");
                    foreach (var actor in sortedActors)
                    {
                        var typeInfo = actor.type.HasValue ? $" ({actor.type})" : "";
                        var status = actor.isKnown ? "[KNOWN]" : "[UNKNOWN]";
                        Program.Log($"{actor.id} {actor.name}{typeInfo} {status}");
                    }

                    // Highlight unknown actors separately
                    if (unknownActors.Any())
                    {
                        Program.Log("\n=== UNKNOWN ACTORS (Missing from Dictionary) ===");
                        Program.Log("These actors are not in our TechNames dictionary and may need to be added:");
                        
                        var sortedUnknown = unknownActors.OrderBy(a => a.name).ToList();
                        foreach (var actor in sortedUnknown)
                        {
                            Program.Log($"  {actor.id} {actor.name}");
                        }
                        
                        Program.Log($"\nTotal Unknown Actors: {unknownActors.Count}");
                        Program.Log("Consider adding these to the Names.TechNames dictionary if they are vehicles/deployables.");
                    }
                    else
                    {
                        Program.Log("\n=== ALL ACTORS ARE KNOWN ===");
                        Program.Log("All actors found are already in our TechNames dictionary!");
                    }

                    Program.Log($"\nTotal: {actorList.Count} actors (excluding players)");
                }
                else
                {
                    Program.Log("No entries found.");
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error in ListVehicles: {ex.Message}");
            }
        }
    }
} 