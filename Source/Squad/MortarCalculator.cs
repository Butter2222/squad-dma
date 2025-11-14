using Offsets;
using squad_dma.Source.Misc;
using System.Numerics;
using System.Linq;

namespace squad_dma.Source.Squad
{
    /// <summary>
    /// Advanced Mortar Calculator for vehicle weapon detection and ballistic calculations
    /// Only tracks vehicle weapons, ignores soldier small arms
    /// Uses Manager's existing functions to avoid duplication
    /// </summary>
    public class MortarCalculator
    {
        #region Private Fields
        private readonly Manager _manager;
        private readonly Game _game;
        private DateTime _lastLogUpdate = DateTime.MinValue;
        
        // Current weapon information
        private string _currentWeaponName = string.Empty;
        private string _lastLoggedWeaponName = string.Empty;
        private bool _isInVehicle = false;
        private bool _lastLoggedVehicleState = false;
        private WeaponData _currentWeaponData = null;
        #endregion

        #region Public Properties
        public string CurrentWeaponName => _currentWeaponName;
        public bool IsInVehicle => _isInVehicle;
        public WeaponData CurrentWeaponData => _currentWeaponData;
        public bool HasBallisticWeapon => _currentWeaponData != null;
        #endregion

        #region Constructor
        public MortarCalculator(Manager manager, Game game = null)
        {
            _manager = manager;
            _game = game;
            
            DetectCurrentWeapon();
        }
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Updates the vehicle weapon detector and logs changes to console
        /// Only tracks vehicle weapons, ignores soldier weapons
        /// </summary>
        public void Update()
        {
            if (!_manager.IsLocalPlayerValid()) return;
            
            // Only update every 500ms to avoid spam
            if ((DateTime.Now - _lastLogUpdate).TotalMilliseconds < 500) return;
            
            DetectCurrentWeapon();
            
            _lastLogUpdate = DateTime.Now;
        }
        
        /// <summary>
        /// Gets the local player's active weapon name (vehicles only)
        /// </summary>
        /// <returns>Current weapon name</returns>
        public string GetActiveWeaponName()
        {
            try
            {
                if (!_manager.IsLocalPlayerValid()) 
                    return "Unknown";

                string weaponName = "";

                // Only track vehicle weapons using Manager's existing functions
                if (_manager.IsInVehicle())
                {
                    _isInVehicle = true;
                    ulong vehicleWeapon = _manager.GetVehicleWeapon();
                    weaponName = GetWeaponName(vehicleWeapon);
                    
                    // Update current weapon tracking
                    _currentWeaponName = weaponName;
                    
                    // Update ballistic data
                    _currentWeaponData = GetWeaponData(weaponName);
                }
                else
                {
                    _isInVehicle = false;
                    weaponName = "No Vehicle";
                    
                    // Clear weapon data when not in vehicle
                    _currentWeaponName = weaponName;
                    _currentWeaponData = null;
                }
                
                // Log to console if weapon or vehicle state changed
                LogWeaponChange();

                return weaponName;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting active weapon: {ex.Message}");
                return "Error";
            }
        }

        /// <summary>
        /// Calculates ballistic trajectory for current weapon
        /// </summary>
        /// <param name="targetPosition">Target world position</param>
        /// <param name="shooterPosition">Shooter world position</param>
        /// <returns>BallisticSolution with firing data</returns>
        public BallisticSolution CalculateTrajectory(Vector3 targetPosition, Vector3 shooterPosition)
        {
            if (_currentWeaponData == null)
                return null;

            try
            {
                var solution = new BallisticSolution();
                
                // Calculate 2D distance on flat ground (ignore Z height differences)
                float deltaX = targetPosition.X - shooterPosition.X;
                float deltaY = targetPosition.Y - shooterPosition.Y;
                float distanceGameUnits = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                float distance = distanceGameUnits / 100f; // Convert from game units (cm) to meters
                solution.Distance = distance;
                
                // Calculate bearing (azimuth) using the correct formula
                double radians = Math.Atan2(deltaY, deltaX);
                double degrees = radians * (180.0 / Math.PI);
                degrees = 90.0 - degrees;
                
                if (degrees < 0) degrees += 360.0;
                
                solution.Bearing = (float)degrees;
                
                // Use flat ground assumption - no height difference
                solution.HeightDifference = 0f;
                
                // Calculate elevation angle
                solution.Elevation = CalculateElevation(distance, solution.HeightDifference, _currentWeaponData);
                
                // Calculate time of flight
                solution.TimeOfFlight = CalculateTimeOfFlight(distance, solution.Elevation, _currentWeaponData);
                
                // Calculate spread radius and elliptical spread
                solution.SpreadEllipse = CalculateSpreadEllipse(distance, solution.Elevation, _currentWeaponData);
                solution.SpreadRadius = Math.Max(solution.SpreadEllipse.Horizontal, solution.SpreadEllipse.Vertical);
                
                // Check if target is in range
                solution.IsInRange = distance >= _currentWeaponData.MinDistance;
                
                // Set weapon info
                solution.WeaponName = _currentWeaponData.Name;
                solution.ExplosionRadius = _currentWeaponData.ExplosionRadius;
                solution.ExplosionDamage = _currentWeaponData.ExplosionDamage;
                solution.DamageFallOff = _currentWeaponData.DamageFallOff;
                
                return solution;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating trajectory: {ex.Message}");
                return null;
            }
        }


        #endregion

        #region Private Methods

        /// <summary>
        /// Gets the gravity value for ballistic calculations based on weapon data
        /// </summary>
        /// <param name="weaponData">Weapon ballistic data</param>
        /// <returns>Gravity value adjusted for weapon's gravity scale</returns>
        private float GetGravityValue(WeaponData weaponData)
        {
            // Base gravity value for Squad (9.78 m/s²)
            const float BASE_GRAVITY = 9.78f;
            return BASE_GRAVITY * weaponData.GravityScale;
        }

        /// <summary>
        /// Converts MOA (Minute of Angle) to radians
        /// </summary>
        /// <param name="moa">MOA value</param>
        /// <returns>MOA in radians</returns>
        private float ConvertMoaToRadians(float moa)
        {
            // MOA to radians: (MOA / 60) × π / 180
            return (moa / 60f) * (float)Math.PI / 180f;
        }

        /// <summary>
        /// Calculates a dynamic fallback spread value based on weapon characteristics
        /// </summary>
        /// <param name="distance">Distance to target in meters</param>
        /// <param name="weaponData">Weapon ballistic data</param>
        /// <returns>Fallback spread value in meters</returns>
        private float GetDynamicFallbackSpread(float distance, WeaponData weaponData)
        {
            // Calculate a reasonable fallback based on weapon's MOA and distance
            // This provides a basic angular spread calculation as fallback
            float moaRad = ConvertMoaToRadians(weaponData.Moa);
            float angularSpread = distance * (float)Math.Tan(moaRad);
            
            // Ensure minimum spread for very precise weapons or close distances
            return Math.Max(angularSpread, 1.0f);
        }

        /// <summary>
        /// Detects the current weapon and updates weapon name
        /// </summary>
        private void DetectCurrentWeapon()
        {
            try
            {
                _currentWeaponName = GetActiveWeaponName();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error detecting current weapon: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs weapon changes to console (vehicles only)
        /// INFO: Only when ballistic weapon detected
        /// DEBUG: Everything else
        /// </summary>
        private void LogWeaponChange()
        {
            try
            {
                bool weaponChanged = _currentWeaponName != _lastLoggedWeaponName;
                bool vehicleStateChanged = _isInVehicle != _lastLoggedVehicleState;
                
                if (weaponChanged || vehicleStateChanged)
                {
                    if (_isInVehicle)
                    {
                        if (_currentWeaponData != null)
                        {
                            // INFO: Only for ballistic weapons
                            Logger.Info($"[Ballistic Weapon Detected] : {_currentWeaponName}");
                            Logger.Debug($"[Ballistic] Weapon: {_currentWeaponData.Name}, Type: {_currentWeaponData.Type}, Velocity: {_currentWeaponData.Velocity}m/s, Min Distance: {_currentWeaponData.MinDistance}m");
                        }
                        else
                        {
                            // DEBUG: Non-ballistic vehicle weapons
                            Logger.Debug($"[Vehicle Weapon] Current Weapon: {_currentWeaponName}");
                        }
                    }
                    else
                    {
                        // DEBUG: Vehicle exit messages
                        if (_lastLoggedVehicleState)
                        {
                            Logger.Debug($"[Vehicle Weapon] Exited vehicle - No vehicle weapon");
                        }
                    }
                    
                    _lastLoggedWeaponName = _currentWeaponName;
                    _lastLoggedVehicleState = _isInVehicle;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error logging weapon change: {ex.Message}");
            }
        }


        /// <summary>
        /// Gets weapon name from weapon pointer
        /// </summary>
        /// <param name="weaponPointer">Weapon memory pointer</param>
        /// <returns>Cleaned weapon name</returns>
        private string GetWeaponName(ulong weaponPointer)
        {
            try
            {
                if (weaponPointer == 0)
                    return "No Vehicle Weapon";

                // Get weapon class name
                string weaponClassName = Memory.GetActorClassName(weaponPointer);
                
                // Clean up the weapon name for display
                return CleanWeaponName(weaponClassName);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting weapon name: {ex.Message}");
                return "Error";
            }
        }

        /// <summary>
        /// Cleans up weapon class name for display
        /// </summary>
        /// <param name="weaponClassName">Raw weapon class name from memory</param>
        /// <returns>Cleaned weapon name</returns>
        private string CleanWeaponName(string weaponClassName)
        {
            if (string.IsNullOrEmpty(weaponClassName))
                return "Unknown";

            // Remove common prefixes and suffixes
            string cleanName = weaponClassName;
            
            // Remove blueprint prefixes
            if (cleanName.StartsWith("BP_"))
                cleanName = cleanName.Substring(3);
            
            // Remove common suffixes
            if (cleanName.EndsWith("_C"))
                cleanName = cleanName.Substring(0, cleanName.Length - 2);
            
            // Replace underscores with spaces
            cleanName = cleanName.Replace("_", " ");
            
            return cleanName;
        }

        /// <summary>
        /// Gets weapon ballistic data for a detected weapon
        /// </summary>
        /// <param name="weaponName">Detected weapon name</param>
        /// <returns>WeaponData if found, null otherwise</returns>
        private WeaponData GetWeaponData(string weaponName)
        {
            if (string.IsNullOrEmpty(weaponName))
                return null;

            // Exact weapon name mappings
            string lowerName = weaponName.ToLower();
            
            // Direct mapping: detected weapon name -> ballistic data
            if (lowerName.Contains("m1937mortar"))
                return WeaponDatabase.Weapons.FirstOrDefault(w => w.Name == "Mortar");
            
            if (lowerName.Contains("m252mortar"))
                return WeaponDatabase.Weapons.FirstOrDefault(w => w.Name == "Mortar");

            if (lowerName.Contains("pp87mortar"))
                return WeaponDatabase.Weapons.FirstOrDefault(w => w.Name == "Mortar");

            if (lowerName.Contains("hellcannon"))
                return WeaponDatabase.Weapons.FirstOrDefault(w => w.Name == "HellCannon");

            if (lowerName.Contains("bm21grad"))
                return WeaponDatabase.Weapons.FirstOrDefault(w => w.Name == "BM-21Grad");

            if (lowerName.Contains("ub32"))
                return WeaponDatabase.Weapons.FirstOrDefault(w => w.Name == "Tech.UB-32");

            if (lowerName.Contains("M1064A3 M121mortar"))
                return WeaponDatabase.Weapons.FirstOrDefault(w => w.Name == "M1064M121");

            // Try to find weapon by exact name match first
            var exactMatch = WeaponDatabase.Weapons.FirstOrDefault(w => 
                w.Name.Equals(weaponName, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
                return exactMatch;

            // Try partial name matching
            var partialMatch = WeaponDatabase.Weapons.FirstOrDefault(w => 
                lowerName.Contains(w.Name.ToLower()) || w.Name.ToLower().Contains(lowerName));
            if (partialMatch != null)
                return partialMatch;

            return null;
        }

        /// <summary>
        /// Calculates elevation angle for ballistic trajectory
        /// </summary>
        /// <param name="distance">Horizontal distance to target</param>
        /// <param name="heightDifference">Height difference (positive = target higher)</param>
        /// <param name="weaponData">Weapon ballistic data</param>
        /// <returns>Elevation angle in degrees</returns>
        private float CalculateElevation(float distance, float heightDifference, WeaponData weaponData)
        {
            try
            {
                float velocity = weaponData.Velocity;
                float gravity = GetGravityValue(weaponData);
                
                // Adjust for height offset
                heightDifference += weaponData.HeightOffset;
                
                // Correct ballistic elevation formula:
                // θ = arctan((v² ± √(v⁴ - g × (g × x² + 2 × y × v²))) / (g × x))
                float v2 = velocity * velocity;
                float v4 = v2 * v2;
                float gx = gravity * distance;
                float gx2 = gravity * distance * distance;
                float gyv2 = gravity * heightDifference * v2;
                
                // Calculate discriminant: v⁴ - g × (g × x² + 2 × y × v²)
                float discriminant = v4 - gravity * (gx2 + 2 * gyv2);
                
                if (discriminant < 0)
                    return weaponData.MinElevation[0]; // Out of range, return min elevation
                
                float sqrtDiscriminant = (float)Math.Sqrt(discriminant);
                
                // Calculate both high and low angle solutions
                // θ_high = arctan((v² + √discriminant) / (g × x))
                // θ_low = arctan((v² - √discriminant) / (g × x))
                float highAngleRad = (float)Math.Atan((v2 + sqrtDiscriminant) / gx);
                float lowAngleRad = (float)Math.Atan((v2 - sqrtDiscriminant) / gx);
                
                // Convert to degrees
                float highAngle = highAngleRad * 180f / (float)Math.PI;
                float lowAngle = lowAngleRad * 180f / (float)Math.PI;
                
                // Choose angle based on weapon type
                float selectedAngle;
                if (weaponData.AngleType == "high")
                {
                    // For mortars, prefer the higher angle that's within limits
                    // Check if high angle is within weapon limits
                    if (highAngle >= weaponData.MinElevation[0] && highAngle <= weaponData.MinElevation[1])
                    {
                        selectedAngle = highAngle;
                    }
                    else if (lowAngle >= weaponData.MinElevation[0] && lowAngle <= weaponData.MinElevation[1])
                    {
                        selectedAngle = lowAngle;
                    }
                    else
                    {
                        // If neither is in range, use the closest to the preferred range
                        selectedAngle = Math.Max(weaponData.MinElevation[0], Math.Min(weaponData.MinElevation[1], highAngle));
                    }
                }
                else
                {
                    // For direct-fire weapons, prefer the lower angle
                    selectedAngle = lowAngle;
                    selectedAngle = Math.Max(weaponData.MinElevation[0], Math.Min(weaponData.MinElevation[1], selectedAngle));
                }
                
                return selectedAngle;
            }
            catch
            {
                return weaponData.MinElevation[0];
            }
        }

        /// <summary>
        /// Calculates time of flight for projectile
        /// </summary>
        /// <param name="distance">Distance to target</param>
        /// <param name="elevation">Elevation angle in degrees</param>
        /// <param name="weaponData">Weapon ballistic data</param>
        /// <returns>Time of flight in seconds</returns>
        private float CalculateTimeOfFlight(float distance, float elevation, WeaponData weaponData)
        {
            try
            {
                float elevationRad = elevation * (float)Math.PI / 180f;
                float velocity = weaponData.Velocity;
                float gravity = GetGravityValue(weaponData);
                float heightDifference = 0f; // Flat ground assumption
                
                // Correct ballistic time of flight formula:
                // t = (v × sin(θ) + √((v × sin(θ))² + 2 × g × y)) / g
                float vSinTheta = velocity * (float)Math.Sin(elevationRad);
                float discriminant = vSinTheta * vSinTheta + 2f * gravity * heightDifference;
                
                // Ensure discriminant is not negative
                if (discriminant < 0)
                    discriminant = 0f;
                
                float timeOfFlight = (vSinTheta + (float)Math.Sqrt(discriminant)) / gravity;
                
                // Account for deceleration if present
                if (weaponData.Deceleration > 0 && weaponData.DecelerationTime > 0)
                {
                    // Simplified deceleration model
                    timeOfFlight *= (1 + weaponData.Deceleration / velocity * weaponData.DecelerationTime);
                }
                
                return Math.Max(0.1f, timeOfFlight);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating time of flight: {ex.Message}");
                return 1.0f;
            }
        }

        /// <summary>
        /// Calculates the maximum distance a weapon can fire based on its ballistic properties
        /// </summary>
        /// <param name="weaponData">Weapon ballistic data</param>
        /// <returns>Maximum distance in meters</returns>
        public float CalculateMaxDistance(WeaponData weaponData)
        {
            try
            {
                float velocity = weaponData.Velocity;
                float gravity = GetGravityValue(weaponData);
                
                // If there's no deceleration (most weapons)
                if (weaponData.DecelerationDistance == 0)
                {
                    // Standard maximum range for projectile with no deceleration
                    // Formula: R = v² / g (at 45° angle for maximum range)
                    return (velocity * velocity) / gravity;
                }
                
                // For weapons with deceleration (like UB-32), calculate dynamic max distance
                // Calculate distance due to deceleration (only the velocity difference part)
                float velocityDifference = weaponData.Velocity - (weaponData.Velocity - weaponData.Deceleration * weaponData.DecelerationTime);
                float decelerationDistance = velocityDifference * weaponData.DecelerationTime;
                
                // Calculate distance at constant velocity like if the whole trajectory was at cruise speed
                float finalVelocity = weaponData.Velocity - weaponData.Deceleration * weaponData.DecelerationTime;
                float cruiseDistance = (finalVelocity * finalVelocity) / gravity;
                
                // Add both parts of the distance
                return decelerationDistance + cruiseDistance;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating max distance: {ex.Message}");
                return 1250f; // Fallback
            }
        }

        /// <summary>
        /// Calculate the weapon velocity based on the distance to the target.
        /// If the projectile has a deceleration phase, considers both deceleration and post-deceleration velocity.
        /// </summary>
        /// <param name="distance">Distance between weapon and target in meters</param>
        /// <param name="weaponData">Weapon ballistic data</param>
        /// <returns>Calculated velocity for the given distance</returns>
        public float GetVelocityForDistance(float distance, WeaponData weaponData)
        {
            try
            {
                // If there's no deceleration, return the constant velocity
                if (weaponData.DecelerationDistance == 0)
                {
                    return weaponData.Velocity;
                }
                
                // If the distance is within the deceleration phase
                if (distance <= weaponData.DecelerationDistance)
                {
                    float discriminant = (float)Math.Sqrt(weaponData.Velocity * weaponData.Velocity + 2 * weaponData.Deceleration * distance);
                    float t = (-weaponData.Velocity + discriminant) / weaponData.Deceleration;
                    return weaponData.Velocity - weaponData.Deceleration * t;
                }
                
                // If the distance is beyond the deceleration phase
                float finalVelocity = weaponData.Velocity - weaponData.Deceleration * weaponData.DecelerationTime;
                float distanceAfterDeceleration = distance - weaponData.DecelerationDistance;
                float timeAfterDeceleration = distanceAfterDeceleration / finalVelocity;
                float totalTime = weaponData.DecelerationTime + timeAfterDeceleration;
                
                // Calculate the average velocity
                return distance / totalTime;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating velocity for distance: {ex.Message}");
                return weaponData.Velocity; // Fallback to initial velocity
            }
        }

        /// <summary>
        /// Calculate distance at which a given damage will be dealt
        /// Based on Squad's damage falloff formula
        /// </summary>
        /// <param name="maxDamage">Maximum damage of the weapon</param>
        /// <param name="startRadius">Inner explosion radius</param>
        /// <param name="endRadius">Outer explosion radius</param>
        /// <param name="falloff">Damage falloff factor</param>
        /// <param name="distanceFromImpact">Distance from impact point</param>
        /// <param name="targetDamage">Target damage amount (e.g., 100 for kill)</param>
        /// <returns>Distance at which target damage will be dealt</returns>
        public float CalculateDistanceForDamage(int maxDamage, float startRadius, float endRadius, float falloff, float distanceFromImpact, int targetDamage)
        {
            try
            {
                const float PLAYERSIZE = 1.8f; // Standard player size in meters
                
                // Calculate the radius at which target damage occurs
                float radius = endRadius - (float)Math.Pow((float)targetDamage / maxDamage, 1f / falloff) * (endRadius - startRadius);
                
                // Calculate the distance from impact where this damage occurs
                float distance = (float)Math.Sqrt(-Math.Pow(distanceFromImpact - PLAYERSIZE, 2) + Math.Pow(radius, 2));
                
                return Math.Max(0f, distance);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating distance for damage: {ex.Message}");
                return endRadius; // Fallback to outer radius
            }
        }

        #region Weapon Ranging Data Arrays
        
        // Tech.Mortar ranging data: distance -> degrees
        private static readonly double[] TechMortarDistances = { 50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200, 1250 };
        private static readonly double[] TechMortarDegrees = { 83.8, 82.9, 80.5, 78.0, 75.7, 73.2, 70.5, 68.0, 65.0, 62.0, 58.4, 53.8, 48.2, 40.0 };

        // Regular Mortar ranging data: distance -> milliradians
        private static readonly double[] MortarDistances = { 50, 100, 150, 200, 250, 300, 350, 400, 450, 500, 550, 600, 650, 700, 750, 800, 850, 900, 950, 1000, 1050, 1100, 1150, 1200, 1250 };
        private static readonly double[] MortarMilliradians = { 1579, 1558, 1538, 1517, 1496, 1475, 1453, 1431, 1409, 1387, 1364, 1341, 1317, 1292, 1267, 1240, 1212, 1183, 1152, 1118, 1081, 1039, 988, 918, 800 };

        // UB-32 ranging data: distance -> degrees
        private static readonly double[] UB32Distances = { 50, 250, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200, 1300, 1400, 1500, 1600, 1700, 1800, 1900, 2000 };
        private static readonly double[] UB32Degrees = { 0, 0, 2.5, 5, 5, 7.5, 8.8, 10, 12, 13.5, 15.5, 16.3, 18, 20, 22.5, 25, 27.5, 29.8, 32 };

        // HellCannon ranging data: distance -> degrees
        // Note: HellCannon has non-monotonic ranging - same distance can have different angles
        // Using the high-angle trajectory (more common for mortars)
        private static readonly double[] HellCannonDistances = { 150, 200, 300, 400, 500, 600, 700, 800, 850, 875, 900, 925 };
        private static readonly double[] HellCannonDegrees = { 85, 83.5, 80.5, 77, 73.5, 70, 65, 60, 55, 50, 45, 40 };

        // BM-21Grad ranging data: distance -> degrees
        private static readonly double[] BM21GradDistances = { 1000, 1050, 1100, 1150, 1200, 1250, 1300, 1350, 1400, 1450, 1500, 1550, 1600, 1650, 1700, 1750, 1800, 1850, 1900, 1950, 2000, 2050 };
        private static readonly double[] BM21GradDegrees = { 14.7, 15.5, 16.3, 17.2, 18, 18.9, 19.8, 20.7, 21.7, 22.7, 23.7, 24.7, 25.9, 27, 28.2, 29.6, 31, 32.6, 34.4, 36.5, 39.4, 45 };
        
        #endregion

        /// <summary>
        /// Universal function to get weapon ranging data based on weapon name
        /// </summary>
        /// <param name="weaponName">Name of the weapon (e.g., "Tech.Mortar", "Mortar", "UB-32")</param>
        /// <param name="meters">Distance in meters</param>
        /// <returns>Ranging value (degrees or milliradians) or -1 if weapon not found</returns>
        public static double GetWeaponRanging(string weaponName, int meters)
        {
            if (string.IsNullOrEmpty(weaponName))
                return -1;

            switch (weaponName)
            {
                case "Tech.Mortar":
                    return MetersToDegrees(meters, TechMortarDistances, TechMortarDegrees);
                
                case "Mortar":
                    return MetersToMilliradians(meters, MortarDistances, MortarMilliradians);
                
                case "HellCannon":
                    return MetersToDegrees(meters, HellCannonDistances, HellCannonDegrees);
                
                case "BM-21Grad":
                    return MetersToDegrees(meters, BM21GradDistances, BM21GradDegrees);
                
                case "UB-32":
                case "Tech.UB-32":
                    return MetersToDegrees(meters, UB32Distances, UB32Degrees);
                
                default:
                    return -1; // Weapon not supported
            }
        }

        /// <summary>
        /// Gets the ranging unit for a weapon from WeaponData
        /// </summary>
        /// <param name="weaponName">Name of the weapon</param>
        /// <returns>Unit string ("°" or "mils")</returns>
        public static string GetWeaponRangingUnit(string weaponName)
        {
            if (string.IsNullOrEmpty(weaponName))
                return "unknown";

            // Find the weapon data to get the unit
            var weaponData = WeaponDatabase.Weapons.FirstOrDefault(w => w.Name == weaponName);
            if (weaponData == null)
                return "unknown";

            // Convert weapon data unit to display unit
            switch (weaponData.Unit.ToLower())
            {
                case "deg":
                case "degree":
                case "degrees":
                    return "°";
                
                case "mil":
                case "mils":
                case "milliradian":
                case "milliradians":
                    return "mils";
                
                default:
                    return weaponData.Unit; // Return as-is if unknown
            }
        }

        /// <summary>
        /// Converts meters to Tech.Mortar degrees using exact game data
        /// </summary>
        /// <param name="meters">Distance in meters</param>
        /// <returns>Elevation in degrees</returns>
        public static double MetersToTechMortarDegrees(int meters)
        {
            return MetersToDegrees(meters, TechMortarDistances, TechMortarDegrees);
        }

        /// <summary>
        /// Converts meters to regular Mortar milliradians using exact game data
        /// </summary>
        /// <param name="meters">Distance in meters</param>
        /// <returns>Elevation in milliradians</returns>
        public static double MetersToMortarMilliradians(int meters)
        {
            return MetersToMilliradians(meters, MortarDistances, MortarMilliradians);
        }

        /// <summary>
        /// Converts meters to HellCannon degrees using exact game data
        /// </summary>
        /// <param name="meters">Distance in meters</param>
        /// <returns>Elevation in degrees</returns>
        public static double MetersToHellCannonDegrees(int meters)
        {
            return MetersToDegrees(meters, HellCannonDistances, HellCannonDegrees);
        }

        /// <summary>
        /// Converts meters to BM-21Grad degrees using exact game data
        /// </summary>
        /// <param name="meters">Distance in meters</param>
        /// <returns>Elevation in degrees</returns>
        public static double MetersToBM21GradDegrees(int meters)
        {
            return MetersToDegrees(meters, BM21GradDistances, BM21GradDegrees);
        }

        /// <summary>
        /// Universal function to convert meters to degrees using linear interpolation
        /// </summary>
        /// <param name="meters">Distance in meters</param>
        /// <param name="distanceTable">Array of distance values</param>
        /// <param name="degreeTable">Array of corresponding degree values</param>
        /// <returns>Elevation in degrees</returns>
        public static double MetersToDegrees(int meters, double[] distanceTable, double[] degreeTable)
        {
            if (distanceTable.Length != degreeTable.Length || distanceTable.Length == 0)
                return 0.0;

            if (meters <= distanceTable[0]) return degreeTable[0];
            if (meters >= distanceTable[distanceTable.Length - 1]) return degreeTable[degreeTable.Length - 1];

            // Linear interpolation between known values
            for (int i = 0; i < distanceTable.Length - 1; i++)
            {
                if (meters >= distanceTable[i] && meters <= distanceTable[i + 1])
                {
                    double t = (meters - distanceTable[i]) / (distanceTable[i + 1] - distanceTable[i]);
                    return degreeTable[i] + t * (degreeTable[i + 1] - degreeTable[i]);
                }
            }

            return degreeTable[degreeTable.Length - 1]; // fallback
        }

        /// <summary>
        /// Universal function to convert meters to milliradians using linear interpolation
        /// </summary>
        /// <param name="meters">Distance in meters</param>
        /// <param name="distanceTable">Array of distance values</param>
        /// <param name="milliradianTable">Array of corresponding milliradian values</param>
        /// <returns>Elevation in milliradians</returns>
        public static double MetersToMilliradians(int meters, double[] distanceTable, double[] milliradianTable)
        {
            if (distanceTable.Length != milliradianTable.Length || distanceTable.Length == 0)
                return 0.0;

            if (meters <= distanceTable[0]) return milliradianTable[0];
            if (meters >= distanceTable[distanceTable.Length - 1]) return milliradianTable[milliradianTable.Length - 1];

            // Linear interpolation between known values
            for (int i = 0; i < distanceTable.Length - 1; i++)
            {
                if (meters >= distanceTable[i] && meters <= distanceTable[i + 1])
                {
                    double t = (meters - distanceTable[i]) / (distanceTable[i + 1] - distanceTable[i]);
                    return milliradianTable[i] + t * (milliradianTable[i + 1] - milliradianTable[i]);
                }
            }

            return milliradianTable[milliradianTable.Length - 1]; // fallback
        }

        /// <summary>
        /// Calculates the spread radius based on weapon MOA and trajectory
        /// </summary>
        /// <param name="distance">Distance to target in meters</param>
        /// <param name="elevation">Elevation angle in degrees</param>
        /// <param name="weaponData">Weapon ballistic data</param>
        /// <returns>Spread radius in meters</returns>
        private float CalculateSpreadRadius(float distance, float elevation, WeaponData weaponData)
        {
            try
            {
                var spread = CalculateSpreadEllipse(distance, elevation, weaponData);
                // Return the larger of horizontal or vertical spread as the radius
                // This gives us a circular approximation of the elliptical spread
                return Math.Max(spread.Horizontal, spread.Vertical);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating spread radius: {ex.Message}");
                // Dynamic fallback based on weapon's MOA and distance
                return GetDynamicFallbackSpread(distance, weaponData);
            }
        }

        /// <summary>
        /// Calculates the elliptical spread dimensions for a weapon using proper ballistic physics
        /// Based on Squad's actual spread mechanics - NEVER a perfect circle!
        /// </summary>
        /// <param name="distance">Distance to target in meters</param>
        /// <param name="elevation">Elevation angle in degrees</param>
        /// <param name="weaponData">Weapon ballistic data</param>
        /// <returns>SpreadEllipse with horizontal and vertical dimensions</returns>
        public SpreadEllipse CalculateSpreadEllipse(float distance, float elevation, WeaponData weaponData)
        {
            try
            {
                // Get dynamic velocity for this distance (handles deceleration)
                float velocity = GetVelocityForDistance(distance, weaponData);
                float gravity = GetGravityValue(weaponData);
                float moa = weaponData.Moa;
                
                // Convert MOA to radians using utility function
                float moaRad = ConvertMoaToRadians(moa);
                float elevationRad = elevation * (float)Math.PI / 180f;
                
                // Calculate time of flight for this trajectory
                float timeOfFlight = CalculateTimeOfFlight(distance, elevation, weaponData);
                
                // Calculate VERTICAL SPREAD using proper ballistic formula
                // This is the distance variation caused by elevation angle imprecision
                float verticalSpread = GetVerticalSpread(elevationRad, velocity, moaRad, gravity);
                
                // Calculate HORIZONTAL SPREAD using trajectory path length and arc length formula
                // This is the lateral spread caused by bearing angle imprecision
                float horizontalSpread = GetHorizontalSpread(elevationRad, velocity, moaRad, gravity);
                
                return new SpreadEllipse
                {
                    Horizontal = horizontalSpread,
                    Vertical = verticalSpread,
                    EllipseAngle = elevation // The ellipse angle is the elevation angle
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating spread ellipse: {ex.Message}");
                // Dynamic fallback based on weapon's MOA and distance
                float fallbackSpread = GetDynamicFallbackSpread(distance, weaponData);
                return new SpreadEllipse { Horizontal = fallbackSpread, Vertical = fallbackSpread };
            }
        }

        /// <summary>
        /// Calculate vertical spread using proper ballistic physics
        /// This represents how much the impact distance varies due to elevation angle imprecision
        /// Based on the formula: ±MOA to elevation and calculate the distance difference
        /// </summary>
        /// <param name="elevationRad">Elevation angle in radians</param>
        /// <param name="velocity">Projectile velocity</param>
        /// <param name="moaRad">MOA in radians</param>
        /// <param name="gravity">Gravity value</param>
        /// <returns>Vertical spread in meters</returns>
        private float GetVerticalSpread(float elevationRad, float velocity, float moaRad, float gravity)
        {
            // Calculate the range at slightly different elevation angles (±MOA/2)
            float minElevation = elevationRad - moaRad / 2f;
            float maxElevation = elevationRad + moaRad / 2f;
            
            // Use the range formula: distance = v² × sin(2θ) / g
            float minDistance = (velocity * velocity * (float)Math.Sin(2f * minElevation)) / gravity;
            float maxDistance = (velocity * velocity * (float)Math.Sin(2f * maxElevation)) / gravity;
            
            // The vertical spread is the difference in impact distances
            // This represents how much shots can spread forward/backward from the target
            return Math.Abs(maxDistance - minDistance);
        }

        /// <summary>
        /// Calculate horizontal spread using universal ballistic physics
        /// This represents the lateral spread caused by bearing angle imprecision
        /// Works dynamically for all weapon types based on their ballistic properties
        /// </summary>
        /// <param name="elevationRad">Elevation angle in radians</param>
        /// <param name="velocity">Projectile velocity</param>
        /// <param name="moaRad">MOA in radians</param>
        /// <param name="gravity">Gravity value</param>
        /// <returns>Horizontal spread in meters</returns>
        private float GetHorizontalSpread(float elevationRad, float velocity, float moaRad, float gravity)
        {
            // Universal horizontal spread calculation using trajectory path length
            // This works for all weapon types: mortars, rockets, artillery, etc.
            
            // Calculate the trajectory path length using the formula:
            // L = v²/g × (sin(θ) + cos²(θ) × tanh⁻¹(sin(θ)))
            float sinTheta = (float)Math.Sin(elevationRad);
            float cosTheta = (float)Math.Cos(elevationRad);
            
            // Handle edge case where sinTheta = 1 (90° elevation) to avoid tanh⁻¹(1) = ∞
            float tanhInv;
            if (Math.Abs(sinTheta - 1.0f) < 0.001f)
            {
                // For very high angles, use approximation
                tanhInv = 2.0f; // Approximate value for tanh⁻¹(1)
            }
            else
            {
                tanhInv = (float)Math.Atanh(sinTheta);
            }
            
            float pathLength = (velocity * velocity / gravity) * (sinTheta + cosTheta * cosTheta * tanhInv);
            
            // Calculate horizontal spread using arc length formula:
            // arc_length = θ/360 × 2πr
            // where θ is MOA in degrees and r is the trajectory path length
            float moaDegrees = moaRad * (180f / (float)Math.PI); // Convert MOA from radians to degrees
            float horizontalSpread = (moaDegrees / 360f) * 2f * (float)Math.PI * pathLength;
            
            return horizontalSpread;
        }

        #endregion
    }

    #region Data Structures

    /// <summary>
    /// Ballistic solution for trajectory calculation
    /// </summary>
    public class BallisticSolution
    {
        public float Distance { get; set; }
        public float Bearing { get; set; }
        public float Elevation { get; set; }
        public float TimeOfFlight { get; set; }
        public float HeightDifference { get; set; }
        public bool IsInRange { get; set; }
        public string WeaponName { get; set; } = string.Empty;
        public float[] ExplosionRadius { get; set; } = new float[2];
        public int ExplosionDamage { get; set; }
        public float DamageFallOff { get; set; }
        public float SpreadRadius { get; set; }
        public SpreadEllipse SpreadEllipse { get; set; } = new SpreadEllipse();
    }

    /// <summary>
    /// Elliptical spread dimensions for ballistic weapons
    /// </summary>
    public class SpreadEllipse
    {
        public float Horizontal { get; set; }
        public float Vertical { get; set; }
        public float EllipseAngle { get; set; } // Angle of the ellipse in degrees
        public float SemiMajorAxis => Math.Max(Horizontal, Vertical);
        public float SemiMinorAxis => Math.Min(Horizontal, Vertical);
    }

    /// <summary>
    /// Weapon ballistic data
    /// </summary>
    public class WeaponData
    {
        public string Name { get; set; } = string.Empty;
        public float Velocity { get; set; }
        public float Deceleration { get; set; }
        public float DecelerationTime { get; set; }
        public float DecelerationDistance { get; set; }
        public float GravityScale { get; set; }
        public float[] MinElevation { get; set; } = new float[2];
        public string Unit { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string AngleType { get; set; } = string.Empty;
        public int ElevationPrecision { get; set; }
        public float MinDistance { get; set; }
        public float Moa { get; set; }
        public int ExplosionDamage { get; set; }
        public float[] ExplosionRadius { get; set; } = new float[2];
        public float ExplosionDistanceFromImpact { get; set; }
        public float DamageFallOff { get; set; }
        public float HeightOffset { get; set; }
        public ShellData[] Shells { get; set; } = null;
    }

    /// <summary>
    /// Shell-specific data for weapons with multiple shell types
    /// </summary>
    public class ShellData
    {
        public float Moa { get; set; }
        public int ExplosionDamage { get; set; }
        public float[] ExplosionRadius { get; set; } = new float[2];
        public float ExplosionDistanceFromImpact { get; set; }
        public float DamageFallOff { get; set; }
    }

    /// <summary>
    /// Weapon database containing all ballistic data
    /// </summary>
    public static class WeaponDatabase
    {
        public static readonly WeaponData[] Weapons = new WeaponData[]
        {
            new WeaponData
            {
                Name = "Mortar",
                Velocity = 110,
                Deceleration = 0,
                DecelerationTime = 0,
                DecelerationDistance = 0,
                GravityScale = 1,
                MinElevation = new float[] { 45, 88.875f },
                Unit = "mil",
                Type = "deployables",
                AngleType = "high",
                ElevationPrecision = 0,
                MinDistance = 51,
                Moa = 50,
                ExplosionDamage = 350,
                ExplosionRadius = new float[] { 0, 40 },
                ExplosionDistanceFromImpact = 1,
                DamageFallOff = 7,
                HeightOffset = 1
            },
            new WeaponData
            {
                Name = "UB-32",
                Velocity = 300,
                Deceleration = 50,
                DecelerationTime = 2,
                DecelerationDistance = 500, // Calculated: velocity * decelerationTime - 0.5 * deceleration * decelerationTime^2
                GravityScale = 2,
                MinElevation = new float[] { -25, 35 },
                Unit = "deg",
                Type = "deployables",
                AngleType = "low",
                ElevationPrecision = 1,
                MinDistance = 0,
                Moa = 300,
                ExplosionDamage = 115,
                ExplosionRadius = new float[] { 5, 18 },
                ExplosionDistanceFromImpact = 0.2f,
                DamageFallOff = 1,
                HeightOffset = 1
            },
            new WeaponData
            {
                Name = "HellCannon",
                Velocity = 95,
                Deceleration = 0,
                DecelerationTime = 0,
                DecelerationDistance = 0,
                GravityScale = 1,
                MinElevation = new float[] { 10, 85 },
                Unit = "deg",
                Type = "deployables",
                AngleType = "high",
                ElevationPrecision = 1,
                MinDistance = 160,
                Moa = 100,
                ExplosionDamage = 125,
                ExplosionRadius = new float[] { 1, 50 },
                ExplosionDistanceFromImpact = 2,
                DamageFallOff = 1,
                HeightOffset = 1.5f
            },
            new WeaponData
            {
                Name = "Tech.Mortar",
                Velocity = 110,
                Deceleration = 0,
                DecelerationTime = 0,
                DecelerationDistance = 0,
                GravityScale = 1,
                MinElevation = new float[] { -45, 135 },
                Unit = "deg",
                Type = "vehicles",
                AngleType = "high",
                ElevationPrecision = 1,
                MinDistance = 51,
                Moa = 50,
                ExplosionDamage = 350,
                ExplosionRadius = new float[] { 0, 40 },
                ExplosionDistanceFromImpact = 0.5f,
                DamageFallOff = 7,
                HeightOffset = 2.5f
            },
            new WeaponData
            {
                Name = "Tech.UB-32",
                Velocity = 300,
                Deceleration = 50,
                DecelerationTime = 2,
                DecelerationDistance = 500, // Same as UB-32
                GravityScale = 2,
                MinElevation = new float[] { -45, 135 },
                Unit = "deg",
                Type = "vehicles",
                AngleType = "low",
                ElevationPrecision = 1,
                MinDistance = 0,
                Moa = 300,
                ExplosionDamage = 115,
                ExplosionRadius = new float[] { 5, 18 },
                ExplosionDistanceFromImpact = 0.2f,
                DamageFallOff = 1,
                HeightOffset = 2.5f
            },
            new WeaponData
            {
                Name = "BM-21Grad",
                Velocity = 200,
                Deceleration = 0,
                DecelerationTime = 0,
                DecelerationDistance = 0,
                GravityScale = 2,
                MinElevation = new float[] { -45, 135 },
                Unit = "deg",
                Type = "vehicles",
                AngleType = "low",
                ElevationPrecision = 1,
                MinDistance = 0,
                Moa = 200,
                ExplosionDamage = 140,
                ExplosionRadius = new float[] { 1, 35 },
                ExplosionDistanceFromImpact = 2,
                DamageFallOff = 1,
                HeightOffset = 3
            },
            new WeaponData
            {
                Name = "M1064M121",
                Velocity = 142,
                Deceleration = 0,
                DecelerationTime = 0,
                DecelerationDistance = 0,
                GravityScale = 1,
                MinElevation = new float[] { 45, 85.3f },
                Unit = "deg",
                Type = "vehicles",
                AngleType = "high",
                ElevationPrecision = 1,
                MinDistance = 340,
                Moa = 50,
                ExplosionDamage = 100,
                ExplosionRadius = new float[] { 10, 60 },
                ExplosionDistanceFromImpact = 10,
                DamageFallOff = 1.3f,
                HeightOffset = 3,
                Shells = new ShellData[]
                {
                    new ShellData
                    {
                        Moa = 40,
                        ExplosionDamage = 400,
                        ExplosionRadius = new float[] { 0, 40 },
                        ExplosionDistanceFromImpact = 1,
                        DamageFallOff = 7
                    },
                    new ShellData
                    {
                        Moa = 50,
                        ExplosionDamage = 100,
                        ExplosionRadius = new float[] { 10, 60 },
                        ExplosionDistanceFromImpact = 10,
                        DamageFallOff = 1.3f
                    }
                }
            },
            new WeaponData
            {
                Name = "Mk19",
                Velocity = 235,
                Deceleration = 0,
                DecelerationTime = 0,
                DecelerationDistance = 0,
                GravityScale = 1,
                MinElevation = new float[] { -45, 85.3f },
                Unit = "deg",
                Type = "vehicles",
                AngleType = "low",
                ElevationPrecision = 1,
                MinDistance = 10,
                Moa = 50,
                ExplosionDamage = 115,
                ExplosionRadius = new float[] { 1, 15 },
                ExplosionDistanceFromImpact = 0.08f,
                DamageFallOff = 1,
                HeightOffset = 2.5f
            },
            // Modded weapons
            new WeaponData
            {
                Name = "M109",
                Velocity = 225,
                Deceleration = 0,
                DecelerationTime = 0,
                DecelerationDistance = 0,
                GravityScale = 2,
                MinElevation = new float[] { -45, 135 },
                Unit = "deg",
                Type = "modded",
                AngleType = "low",
                ElevationPrecision = 1,
                MinDistance = 0,
                Moa = 1.5f,
                ExplosionDamage = 2000,
                ExplosionRadius = new float[] { 40, 75 },
                ExplosionDistanceFromImpact = 1,
                DamageFallOff = 3.5f,
                HeightOffset = 3
            },
            new WeaponData
            {
                Name = "T62.DUMP.TRUCK",
                Velocity = 210,
                Deceleration = 0,
                DecelerationTime = 0,
                DecelerationDistance = 0,
                GravityScale = 2,
                MinElevation = new float[] { -45, 135 },
                Unit = "deg",
                Type = "modded",
                AngleType = "low",
                ElevationPrecision = 1,
                MinDistance = 0,
                Moa = 100,
                ExplosionDamage = 400,
                ExplosionRadius = new float[] { 20, 40 },
                ExplosionDistanceFromImpact = 1,
                DamageFallOff = 1,
                HeightOffset = 3
            },
            new WeaponData
            {
                Name = "HIMARS",
                Velocity = 250,
                Deceleration = 0,
                DecelerationTime = 0,
                DecelerationDistance = 0,
                GravityScale = 2,
                MinElevation = new float[] { -3, 84.7f },
                Unit = "deg",
                Type = "modded",
                AngleType = "low",
                ElevationPrecision = 1,
                MinDistance = 0,
                Moa = 15,
                ExplosionDamage = 300,
                ExplosionRadius = new float[] { 1.5f, 50 },
                ExplosionDistanceFromImpact = 3,
                DamageFallOff = 1,
                HeightOffset = 3
            },
            new WeaponData
            {
                Name = "TOS-1A",
                Velocity = 100,
                Deceleration = 0,
                DecelerationTime = 0,
                DecelerationDistance = 0,
                GravityScale = 1,
                MinElevation = new float[] { 0, 80 },
                Unit = "deg",
                Type = "modded",
                AngleType = "low",
                ElevationPrecision = 1,
                MinDistance = 0,
                Moa = 500,
                ExplosionDamage = 150,
                ExplosionRadius = new float[] { 15, 25 },
                ExplosionDistanceFromImpact = 0.2f,
                DamageFallOff = 1,
                HeightOffset = 3
            },
            new WeaponData
            {
                Name = "MTLB_FAB500",
                Velocity = 95,
                Deceleration = 0,
                DecelerationTime = 0,
                DecelerationDistance = 0,
                GravityScale = 1,
                MinElevation = new float[] { -45, 85.3f },
                Unit = "deg",
                Type = "modded",
                AngleType = "low",
                ElevationPrecision = 1,
                MinDistance = 75,
                Moa = 150,
                ExplosionDamage = 4500,
                ExplosionRadius = new float[] { 0.1f, 50 },
                ExplosionDistanceFromImpact = 2,
                DamageFallOff = 4,
                HeightOffset = 3
            }
        };
    }

    #endregion

    #region Usage Example
    
    /// <summary>
    /// Example usage for integrating vehicle ballistic calculations into your radar/ESP system
    /// Only works when player is in a vehicle with ballistic weapons
    /// </summary>
    public static class BallisticExample
    {
        /// <summary>
        /// Example of how to use vehicle ballistic calculations in your ESP/radar
        /// Only displays ballistic info when in vehicle with supported weapon
        /// </summary>
        /// <param name="mortarCalculator">MortarCalculator instance</param>
        /// <param name="targetPosition">Enemy/target position from radar</param>
        /// <param name="playerPosition">Local player position</param>
        /// <returns>Formatted string for display on radar, empty if not in ballistic vehicle</returns>
        public static string GetBallisticDisplayText(MortarCalculator mortarCalculator, Vector3 targetPosition, Vector3 playerPosition)
        {
            // Only show ballistic info if in vehicle with ballistic weapon
            if (!mortarCalculator.IsInVehicle || !mortarCalculator.HasBallisticWeapon)
                return string.Empty;
                
            var solution = mortarCalculator.CalculateTrajectory(targetPosition, playerPosition);
            if (solution == null || !solution.IsInRange)
                return $"❌ OUT OF RANGE";
                
            // Return formatted ballistic solution for display
            return $"🎯 {solution.Distance:F0}m | {solution.Bearing:F0}° | {solution.Elevation:F1}° | {solution.TimeOfFlight:F1}s | {solution.ExplosionDamage}dmg";
        }
    }
    
    #endregion
}
