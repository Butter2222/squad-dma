using Offsets;

namespace squad_dma.Source.Squad.Debug
{
    public class DebugSoldier : Manager
    {
        public DebugSoldier(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        /// <summary>
        /// Reads and logs just the essential weapon name information
        /// </summary>
        /// <param name="soldierActor">The soldier actor address</param>
        /// <param name="label">Label for logging</param>
        private void ReadWeaponInfo(ulong soldierActor, string label)
        {
            try
            {
                // Get inventory component
                ulong inventoryComponent = Memory.ReadPtr(soldierActor + ASQSoldier.InventoryComponent);
                if (inventoryComponent == 0) return;
                
                ulong currentWeapon = Memory.ReadPtr(inventoryComponent + USQPawnInventoryComponent.CurrentWeapon);
                if (currentWeapon == 0) return;
                
                Program.Log($"{label} Weapon:");
                
                // Get weapon object name 
                try
                {
                    int nameIndex = Memory.ReadValue<int>(currentWeapon + 0x18);
                    Dictionary<uint, string> names = Memory.GetNamesById(new List<uint> { (uint)nameIndex });
                    
                    if (names.ContainsKey((uint)nameIndex))
                    {
                        string weaponName = names[(uint)nameIndex];
                        Program.Log($"  - Object: {weaponName}");
                    }
                }
                catch { /* Silently fail */ }
                
                // Get static info name
                try
                {
                    ulong itemStaticInfo = Memory.ReadPtr(currentWeapon + ASQEquipableItem.ItemStaticInfo);
                    
                    if (itemStaticInfo != 0)
                    {
                        int staticInfoNameIndex = Memory.ReadValue<int>(itemStaticInfo + 0x18);
                        Dictionary<uint, string> names = Memory.GetNamesById(new List<uint> { (uint)staticInfoNameIndex });
                        
                        if (names.ContainsKey((uint)staticInfoNameIndex))
                        {
                            string infoName = names[(uint)staticInfoNameIndex];
                            Program.Log($"  - Static: {infoName}");
                        }
                    }
                }
                catch { /* Silently fail */ }
            }
            catch { /* Silently fail */ }
        }
        
        /// <summary>
        /// Reads and logs the current weapon of the local player and optionally other players
        /// </summary>
        /// <param name="includeOtherPlayers">Whether to include other players' weapons in the log</param>
        public void ReadCurrentWeapons(bool includeOtherPlayers = false)
        {
            try
            {
                if (!IsLocalPlayerValid()) return;
                
                Program.Log("=== READING CURRENT WEAPONS ===");
                
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                
                // Get local player's weapon
                if (soldierActor == 0) return;
                
                ReadWeaponInfo(soldierActor, "Local Player");
                
                // If requested, try to find a few other players by team
                if (includeOtherPlayers)
                {
                    // This is a simplified approach that doesn't rely on traversing the player array
                    int localTeamId = Memory.ReadValue<int>(playerState + ASQPlayerState.TeamID);
                    Program.Log($"Local player is on team: {localTeamId}");
                }
                
                Program.Log("=============================");
            }
            catch { /* Silently fail */ }
        }

        /// <summary>
        /// Logs postprocessing settings and optionally disables low health/bleeding visuals
        /// </summary>
        /*public void LogPostProcessingInfo(bool disableVisuals = false)
        {
            try
            {
                if (!IsLocalPlayerValid())
                {
                    Program.Log("LocalPlayer is not valid - cannot log postprocessing info");
                    return;
                }

                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                
                if (soldierActor == 0)
                {
                    Program.Log("SoldierActor is null - cannot log postprocessing info");
                    return;
                }

                Program.Log("=== POSTPROCESSING INFO ===");

                // Get postprocessing settings addresses
                ulong lowHealthSettings = soldierActor + ASQSoldier.LowHealthPostProcessSettings;
                ulong bleedingSettings = soldierActor + ASQSoldier.BleedingPostProcessSettings;
                ulong suppressionSettings = soldierActor + ASQSoldier.SuppressionPostProcessSettings;

                // Log current values
                float vignetteIntensity = Memory.ReadValue<float>(lowHealthSettings + FPostProcessSettings.VignetteIntensity);
                float colorSaturation = Memory.ReadValue<float>(lowHealthSettings + FPostProcessSettings.ColorSaturation);
                float colorContrast = Memory.ReadValue<float>(lowHealthSettings + FPostProcessSettings.ColorContrast);
                
                Program.Log($"Low Health Settings:");
                Program.Log($"  - VignetteIntensity: {vignetteIntensity}");
                Program.Log($"  - ColorSaturation: {colorSaturation}");
                Program.Log($"  - ColorContrast: {colorContrast}");

                // If requested, disable the visual effects
                if (disableVisuals)
                {
                    // Disable low health effects
                    Memory.WriteValue<float>(lowHealthSettings + FPostProcessSettings.VignetteIntensity, 0.0f);
                    Memory.WriteValue<float>(lowHealthSettings + FPostProcessSettings.ColorSaturation, 1.0f);
                    Memory.WriteValue<float>(lowHealthSettings + FPostProcessSettings.ColorContrast, 1.0f);

                    // Disable bleeding effects
                    Memory.WriteValue<float>(bleedingSettings + FPostProcessSettings.VignetteIntensity, 0.0f);
                    Memory.WriteValue<float>(bleedingSettings + FPostProcessSettings.ColorSaturation, 1.0f);
                    Memory.WriteValue<float>(bleedingSettings + FPostProcessSettings.ColorContrast, 1.0f);

                    Program.Log("Disabled low health and bleeding visual effects");
                }

                Program.Log("=============================");
            }
            catch (Exception ex)
            {
                Program.Log($"Error in LogPostProcessingInfo: {ex.Message}");
            }
        }*/
    }
} 