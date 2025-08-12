namespace squad_dma.Source.Squad.Debug
{
    using squad_dma.Source.Misc;
    using squad_dma;

    public class DebugVehicles
    {
        private readonly ulong _playerController;
        private readonly bool _inGame;
        private readonly RegistredActors _actors;
        private bool _vehiclesLogged;

        // Vehicle offsets from memory layout
        private const int VehicleHealthOffset = 0x868;
        private const int VehicleMaxHealthOffset = 0x86c;
        private const int VehicleTypeOffset = 0x6f8;
        private const int VehicleClaimedBySquadOffset = 0x530;
        private const int VehicleRootComponentOffset = 0x138; // From Actor class
        private const int SceneComponentLocationOffset = 0x11C; // From USceneComponent class
        private const int PlayerStateSoldierOffset = 0x768; // From ASQPlayerState class
        private const int PlayerStateTeamIdOffset = 0x400; // From ASQPlayerState class
        private const int SquadStateTeamIdOffset = 0x2AC; // From ASQSquadState class

        public DebugVehicles(ulong playerController, bool inGame, RegistredActors actors)
        {
            _playerController = playerController;
            _inGame = inGame;
            _actors = actors;
            _vehiclesLogged = false;
        }

        public void SetInstantSeatSwitch()  // Not Working in online
        {
            if (!_inGame || _playerController == 0) return;

            try
            {
                ulong playerState = Memory.ReadPtr(_playerController + Offsets.Controller.PlayerState);
                ulong currentSeatPtr = Memory.ReadPtr(playerState + 0x750);

                if (currentSeatPtr == 0) return;

                ulong seatConfigPtr = currentSeatPtr + 0x1f8;
                Memory.WriteValue<float>(seatConfigPtr + 0x64, 1.0f);
            }
            catch { /* Silently fail */ }
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
                int localTeamId = Memory.ReadValue<int>(playerState + PlayerStateTeamIdOffset);
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
                        ulong claimedBySquad = Memory.ReadPtr(vehicleBase + VehicleClaimedBySquadOffset);
                        if (claimedBySquad != 0)
                        {
                            teamId = Memory.ReadValue<int>(claimedBySquad + SquadStateTeamIdOffset);
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
                ulong playerState = Memory.ReadPtr(_playerController + Offsets.Controller.PlayerState);
                if (playerState == 0)
                {
                    Program.Log("Failed to get player state");
                    return;
                }

                int localTeamId = Memory.ReadValue<int>(playerState + PlayerStateTeamIdOffset);
                
                var vehiclesByTeam = new Dictionary<int, List<(string name, ActorType type)>>();
                var unclaimedVehicles = new List<(string name, ActorType type)>();
                var totalVehiclesFound = 0;
                
                var processedActors = new HashSet<string>(); // Track actors we've already categorized

                foreach (var actor in _actors.Actors)
                {
                    try
                    {
                        var actorName = actor.Value.Name;
                        var actorBase = actor.Value.Base;

                        if (actorName.Contains("BP_Soldier"))
                            continue;

                        if (Names.TechNames.TryGetValue(actorName, out var actorType) && 
                            squad_dma.Names.Vehicles.Contains(actorType))
                        {
                            totalVehiclesFound++;
                            processedActors.Add(actorName); // Mark as processed
                            
                            int teamId = -1;
                            ulong claimedBySquad = Memory.ReadPtr(actorBase + VehicleClaimedBySquadOffset);
                            if (claimedBySquad != 0)
                            {
                                teamId = Memory.ReadValue<int>(claimedBySquad + SquadStateTeamIdOffset);
                            }

                            if (teamId == -1)
                            {
                                unclaimedVehicles.Add((actorName, actorType));
                            }
                            else
                            {
                                if (!vehiclesByTeam.ContainsKey(teamId))
                                {
                                    vehiclesByTeam[teamId] = new List<(string name, ActorType type)>();
                                }
                                vehiclesByTeam[teamId].Add((actorName, actorType));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Program.Log($"Error processing vehicle {actor.Value.Name}: {ex.Message}");
                    }
                }

                // Collect all remaining actors that weren't categorized as known vehicles
                var remainingActors = new List<string>();
                foreach (var actor in _actors.Actors)
                {
                    var actorName = actor.Value.Name;
                    if (!actorName.Contains("BP_Soldier") && 
                        !actorName.Contains("BP_PlayerStart") && 
                        !Names.TechNames.ContainsKey(actorName))  // Only include actors that aren't in our TechNames dictionary
                    {
                        remainingActors.Add(actorName);
                    }
                }

                Program.Log("\n=== Vehicle Information ===");
                Program.Log($"Local Team ID: {localTeamId}");
                Program.Log($"Total Vehicles Found: {totalVehiclesFound}\n");

                foreach (var team in vehiclesByTeam.OrderBy(t => t.Key))
                {
                    Program.Log($"Team {team.Key} ({team.Value.Count}):");
                    foreach (var vehicle in team.Value)
                    {
                        Program.Log($"  {vehicle.name} ({vehicle.type})");
                    }
                    Program.Log("");
                }

                if (unclaimedVehicles.Any())
                {
                    Program.Log($"Unclaimed Vehicles ({unclaimedVehicles.Count}):");
                    foreach (var vehicle in unclaimedVehicles)
                    {
                        Program.Log($"  {vehicle.name} ({vehicle.type})");
                    }
                    Program.Log("");
                }

                // Export all remaining actors
                if (remainingActors.Any())
                {
                    Program.Log($"Remaining Actors ({remainingActors.Count}):");
                    foreach (var actor in remainingActors.OrderBy(a => a))
                    {
                        Program.Log($"  {actor}");
                    }
                    Program.Log("");
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error in ListVehiclesInfo: {ex.Message}");
            }
        }
    }
} 