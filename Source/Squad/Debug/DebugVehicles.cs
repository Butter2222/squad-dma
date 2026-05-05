namespace squad_dma.Source.Squad.Debug
{
    using squad_dma.Source.Misc;
    using squad_dma;
    using Offsets;

    public class VehicleInfo
    {
        public ulong Address { get; set; }
        public string Name { get; set; }
        public Vector3D Position { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }
        public int TeamID { get; set; }
        public int SquadID { get; set; }
        public ESQVehicleType VehicleType { get; set; }
        public bool IsClaimable { get; set; }
        public bool IsEnterableWithoutClaim { get; set; }
        public bool IsDrivableWithoutClaim { get; set; }
        public bool IsClaimed { get; set; }
        public string ClaimingSquadName { get; set; }
    }

    public class DebugVehicles
    {
        private readonly ulong _playerController;
        private readonly bool _inGame;
        private readonly RegisteredActors _actors;
        private bool _vehiclesLogged;
        private string _lastLoggedVehicleName = string.Empty;
        private DateTime _lastVehicleCheck = DateTime.MinValue;
        private const int VehicleCheckInterval = 1000;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isDetectionEnabled = false;
        private bool _lastInVehicleState = false;


        public DebugVehicles(ulong playerController, bool inGame, RegisteredActors actors)
        {
            _playerController = playerController;
            _inGame = inGame;
            _actors = actors;
            _vehiclesLogged = false;
            _cancellationTokenSource = new CancellationTokenSource();
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
                _vehiclesLogged = true; // Always set to true after logging
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
                        ulong claimedBySquad = Memory.ReadPtr(vehicleBase + ASQVehicle.ClaimedBySquad);
                        if (claimedBySquad != 0)
                        {
                            teamId = Memory.ReadValue<int>(claimedBySquad + ASQSquadState.TeamId);
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

        public void DetectVehicleName()
        {
            try
            {
                if (!_inGame || _playerController == 0)
                    return;

                ulong playerState = Memory.ReadPtr(_playerController + Offsets.Controller.PlayerState);
                if (playerState == 0)
                    return;

                ulong currentSeat = Memory.ReadPtr(playerState + Offsets.ASQPlayerState.CurrentSeat);
                
                bool isInVehicle = currentSeat != 0;
                
                // State change detection: not in vehicle -> in vehicle or vice versa
                if (isInVehicle != _lastInVehicleState)
                {
                    _lastInVehicleState = isInVehicle;
                    
                    if (!isInVehicle)
                    {
                        // Player exited vehicle
                        Program.Log("Vehicle not found. Please enter a vehicle...");
                        _lastLoggedVehicleName = string.Empty;
                    }
                    else
                    {
                        // Player entered vehicle - get vehicle name
                        ulong seatPawn = Memory.ReadPtr(currentSeat + Offsets.USQVehicleSeatComponent.SeatPawn);
                        string vehicleName = GetVehicleName(seatPawn);
                        
                        if (!string.IsNullOrEmpty(vehicleName) && vehicleName != "Unknown")
                        {
                            Program.Log($"Vehicle found. Vehicle name: {vehicleName}");
                            _lastLoggedVehicleName = vehicleName;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error detecting vehicle name: {ex.Message}");
            }
        }

        private string GetVehicleName(ulong vehiclePtr)
        {
            try
            {
                if (vehiclePtr == 0)
                    return "Unknown";

                string vehicleClassName = Memory.GetActorClassName(vehiclePtr);
                return CleanVehicleName(vehicleClassName);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting vehicle name: {ex.Message}");
                return "Error";
            }
        }

        private string CleanVehicleName(string vehicleClassName)
        {
            if (string.IsNullOrEmpty(vehicleClassName))
                return "Unknown";

            return vehicleClassName;
        }

        public void ToggleVehicleDetection(bool enable)
        {
            if (enable && !_isDetectionEnabled)
            {
                _isDetectionEnabled = true;
                _lastInVehicleState = false;
                _lastLoggedVehicleName = string.Empty;
                StartVehicleDetectionLoop();
                Program.Log("Vehicle detection enabled.");
            }
            else if (!enable && _isDetectionEnabled)
            {
                _isDetectionEnabled = false;
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
                Program.Log("Vehicle detection disabled.");
            }
        }

        private void StartVehicleDetectionLoop()
        {
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested && _isDetectionEnabled)
                {
                    try
                    {
                        if ((DateTime.Now - _lastVehicleCheck).TotalMilliseconds >= VehicleCheckInterval)
                        {
                            DetectVehicleName();
                            _lastVehicleCheck = DateTime.Now;
                        }

                        await Task.Delay(100, _cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Vehicle Detection Loop Failed: {ex.Message}");
                        await Task.Delay(1000, _cancellationTokenSource.Token);
                    }
                }
            }, _cancellationTokenSource.Token);
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }

        /// <summary>
        /// Gathers detailed information from all vehicles in the actor list
        /// </summary>
        public void GatherVehicleInformation()
        {
            if (!_inGame || _playerController == 0) return;

            try
            {
                Program.Log("\n=== GATHERING VEHICLE INFORMATION ===");

                // Get all vehicle actors from the registered actors list
                var vehicles = _actors.Actors.Where(actor =>
                    !Names.Deployables.Contains(actor.Value.ActorType) &&
                    actor.Value.ActorType != ActorType.Player)
                    .ToList();

                if (vehicles.Count == 0)
                {
                    Program.Log("No vehicles found in actor list.");
                    return;
                }

                Program.Log($"Found {vehicles.Count} vehicles. Gathering information...\n");

                var vehicleInfoList = new List<VehicleInfo>();

                foreach (var vehicleActor in vehicles)
                {
                    try
                    {
                        var vehicleAddr = vehicleActor.Value.Base;
                        var vehicleInfo = new VehicleInfo
                        {
                            Address = vehicleAddr,
                            Name = vehicleActor.Value.Name,
                            Position = vehicleActor.Value.Position
                        };

                        // Use scatter read for efficient data gathering
                        var scatterMap = new ScatterReadMap(1);
                        var round1 = scatterMap.AddRound();
                        var round2 = scatterMap.AddRound();

                        // Round 1: Read basic vehicle data and VehicleSeats TArray pointer
                        round1.AddEntry<float>(0, 0, vehicleAddr + ASQVehicle.Health);
                        round1.AddEntry<float>(0, 1, vehicleAddr + ASQVehicle.MaxHealth);
                        round1.AddEntry<byte>(0, 2, vehicleAddr + ASQVehicle.VehicleType);
                        round1.AddEntry<bool>(0, 3, vehicleAddr + ASQVehicle.bClaimable);
                        round1.AddEntry<bool>(0, 4, vehicleAddr + ASQVehicle.bEnterableWithoutClaim);
                        round1.AddEntry<bool>(0, 5, vehicleAddr + ASQVehicle.bDrivableWithoutClaim);
                        
                        // Read VehicleSeats TArray pointer and count
                        var vehicleSeatsArrayPtr = round1.AddEntry<ulong>(0, 6, vehicleAddr + ASQVehicle.VehicleSeats);
                        var vehicleSeatsCount = round1.AddEntry<int>(0, 7, vehicleAddr + ASQVehicle.VehicleSeats + 0x8);
                        
                        // Round 2: Read first seat component from array
                        var firstSeatComponentPtr = round2.AddEntry<ulong>(0, 8, vehicleSeatsArrayPtr, null, 0);

                        scatterMap.Execute();

                        // Process results
                        var results = scatterMap.Results[0];

                        if (results.TryGetValue(0, out var healthResult) && healthResult.TryGetResult<float>(out var health))
                            vehicleInfo.Health = health;

                        if (results.TryGetValue(1, out var maxHealthResult) && maxHealthResult.TryGetResult<float>(out var maxHealth))
                            vehicleInfo.MaxHealth = maxHealth;

                        if (results.TryGetValue(2, out var vehicleTypeResult) && vehicleTypeResult.TryGetResult<byte>(out var vehicleType))
                            vehicleInfo.VehicleType = (ESQVehicleType)vehicleType;

                        if (results.TryGetValue(3, out var claimableResult) && claimableResult.TryGetResult<bool>(out var isClaimable))
                            vehicleInfo.IsClaimable = isClaimable;

                        if (results.TryGetValue(4, out var enterableResult) && enterableResult.TryGetResult<bool>(out var isEnterable))
                            vehicleInfo.IsEnterableWithoutClaim = isEnterable;

                        if (results.TryGetValue(5, out var drivableResult) && drivableResult.TryGetResult<bool>(out var isDrivable))
                            vehicleInfo.IsDrivableWithoutClaim = isDrivable;

                        // Use direct reads for team detection (scatter read fails for nested pointers)
                        if (results.TryGetValue(8, out var firstSeatResult) && firstSeatResult.TryGetResult<ulong>(out var firstSeatPtr))
                        {
                            if (firstSeatPtr != 0)
                            {
                                // Read SeatPawn directly (ASQVehicleSeat*)
                                ulong seatPawn = Memory.ReadPtr(firstSeatPtr + USQVehicleSeatComponent.SeatPawn);
                                
                                if (seatPawn != 0)
                                {
                                    // Read team from SeatPawn (ASQPawn base class)
                                    byte teamByte = Memory.ReadValue<byte>(seatPawn + ASQPawn.Team);
                                    ESQTeam sqTeam = (ESQTeam)teamByte;
                                    vehicleInfo.TeamID = sqTeam switch
                                    {
                                        ESQTeam.Team_One => 1,
                                        ESQTeam.Team_Two => 2,
                                        ESQTeam.Team_Neutral => 0,
                                        _ => -1
                                    };
                                }
                                else
                                {
                                    vehicleInfo.TeamID = -1;
                                }
                            }
                            else
                            {
                                vehicleInfo.TeamID = -1;
                            }
                        }
                        else
                        {
                            vehicleInfo.TeamID = -1;
                        }

                        // Check claim status
                        ulong claimedBySquad = Memory.ReadPtr(vehicleAddr + ASQVehicle.ClaimedBySquad);
                        if (claimedBySquad != 0)
                        {
                            vehicleInfo.IsClaimed = true;
                            vehicleInfo.SquadID = Memory.ReadValue<int>(claimedBySquad + ASQSquadState.SquadId);
                            
                            // If we couldn't get team from SeatPawn, try from ClaimedBySquad
                            if (vehicleInfo.TeamID == -1)
                            {
                                int teamIdFromSquad = Memory.ReadValue<int>(claimedBySquad + ASQSquadState.TeamId);
                                vehicleInfo.TeamID = teamIdFromSquad;
                            }
                        }
                        else
                        {
                            vehicleInfo.IsClaimed = false;
                            vehicleInfo.SquadID = -1;
                        }

                        vehicleInfoList.Add(vehicleInfo);
                    }
                    catch (Exception)
                    {
                        // Silently skip vehicles that fail to read (filtered fake entities)
                    }
                }

                // Display gathered information
                Program.Log($"\n=== VEHICLE INFORMATION ({vehicleInfoList.Count} vehicles) ===\n");

                // Get local player position for distance calculation
                ulong playerState = Memory.ReadPtr(_playerController + Offsets.Controller.PlayerState);
                ulong soldierActor = 0;
                Vector3D localPlayerPos = Vector3D.Zero;
                
                if (playerState != 0)
                {
                    soldierActor = Memory.ReadPtr(playerState + Offsets.ASQPlayerState.Soldier);
                    if (soldierActor != 0)
                    {
                        ulong rootComponent = Memory.ReadPtr(soldierActor + Offsets.Actor.RootComponent);
                        if (rootComponent != 0)
                        {
                            double x = Memory.ReadValue<double>(rootComponent + Offsets.USceneComponent.RelativeLocation);
                            double y = Memory.ReadValue<double>(rootComponent + Offsets.USceneComponent.RelativeLocation + 0x8);
                            double z = Memory.ReadValue<double>(rootComponent + Offsets.USceneComponent.RelativeLocation + 0x10);
                            localPlayerPos = new Vector3D(x, y, z);
                        }
                    }
                }

                foreach (var vInfo in vehicleInfoList.OrderBy(v => v.Name))
                {
                    // Calculate distance from local player
                    double distance = 0;
                    if (localPlayerPos != Vector3D.Zero)
                    {
                        double dx = vInfo.Position.X - localPlayerPos.X;
                        double dy = vInfo.Position.Y - localPlayerPos.Y;
                        double dz = vInfo.Position.Z - localPlayerPos.Z;
                        distance = Math.Sqrt(dx * dx + dy * dy + dz * dz) / 100; // Convert to meters
                    }

                    // Determine team string
                    string teamString = vInfo.TeamID switch
                    {
                        1 => "Team 1",
                        2 => "Team 2",
                        0 => "Neutral",
                        _ => "Unknown"
                    };

                    Program.Log($"Vehicle: {vInfo.Name}");
                    Program.Log($"  Address: 0x{vInfo.Address:X}");
                    Program.Log($"  Position: X={vInfo.Position.X:F1}, Y={vInfo.Position.Y:F1}, Z={vInfo.Position.Z:F1}");
                    if (distance > 0)
                        Program.Log($"  Distance: {distance:F1}m");
                    Program.Log($"  Health: {vInfo.Health:F1} / {vInfo.MaxHealth:F1} ({(vInfo.MaxHealth > 0 ? (vInfo.Health / vInfo.MaxHealth * 100) : 0):F1}%)");
                    Program.Log($"  Vehicle Type: {vInfo.VehicleType}");
                    Program.Log($"  Team: {teamString}");
                    Program.Log($"  Claimed: {(vInfo.IsClaimed ? "Yes" : "No")}");
                    
                    if (vInfo.IsClaimed)
                    {
                        Program.Log($"  Squad ID: {vInfo.SquadID}");
                    }
                    
                    Program.Log($"  Claimable: {vInfo.IsClaimable}");
                    Program.Log($"  Enterable Without Claim: {vInfo.IsEnterableWithoutClaim}");
                    Program.Log($"  Drivable Without Claim: {vInfo.IsDrivableWithoutClaim}");
                    Program.Log("---");
                }

                Program.Log($"\n=== END VEHICLE INFORMATION ===\n");
            }
            catch (Exception ex)
            {
                Program.Log($"Error in GatherVehicleInformation: {ex.Message}");
            }
        }
    }
}
