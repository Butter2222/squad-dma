    using Offsets;

namespace squad_dma.Source.Squad.Debug
{
    public class DebugSoldier : Manager
    {
        public DebugSoldier(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }

        private enum EMovementMode : byte
        {
            MOVE_None = 0,
            MOVE_Walking = 1,
            MOVE_NavWalking = 2,
            MOVE_Falling = 3,
            MOVE_Swimming = 4,
            MOVE_Flying = 5,
            MOVE_Custom = 6,
            MOVE_MAX = 7
        }
        
        /// <summary>
        /// Logs the current values of all features for debugging purposes
        /// </summary>
        public void LogCurrentValues()
        {
            try
            {
                if (!IsLocalPlayerValid())
                {
                    Program.Log("LocalPlayer is not valid - cannot log values");
                    return;
                }
                
                // Get the cached values from one of the features
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                ulong inventoryComponent = Memory.ReadPtr(soldierActor + ASQSoldier.InventoryComponent);
                ulong currentWeapon = inventoryComponent != 0 ? 
                    Memory.ReadPtr(inventoryComponent + USQPawnInventoryComponent.CurrentWeapon) : 0;
                ulong characterMovement = Memory.ReadPtr(soldierActor + Character.CharacterMovement);
                
                if (soldierActor == 0)
                {
                    Program.Log("SoldierActor is null - cannot log values");
                    return;
                }

                Program.Log("=== LOCAL SOLDIER DEBUG VALUES ===");
                
                // Suppression values
                float underSuppressionPercentage = Memory.ReadValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage);
                float maxSuppressionPercentage = Memory.ReadValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage);
                float suppressionMultiplier = Memory.ReadValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier);
                
                Program.Log($"Suppression:");
                Program.Log($"  - UnderSuppressionPercentage: {underSuppressionPercentage}");
                Program.Log($"  - MaxSuppressionPercentage: {maxSuppressionPercentage}");
                Program.Log($"  - SuppressionMultiplier: {suppressionMultiplier}");
                
                // Interaction distances
                float useInteractDistance = Memory.ReadValue<float>(soldierActor + ASQSoldier.UseInteractDistance);
                float interactableRadiusMultiplier = Memory.ReadValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier);
                
                Program.Log($"Interaction Distances:");
                Program.Log($"  - UseInteractDistance: {useInteractDistance}");
                Program.Log($"  - InteractableRadiusMultiplier: {interactableRadiusMultiplier}");
                
                // Speed hack
                float timeDilation = Memory.ReadValue<float>(soldierActor + Actor.CustomTimeDilation);
                
                Program.Log($"Speed Hack:");
                Program.Log($"  - CustomTimeDilation: {timeDilation}");
                
                // AirStuck
                if (characterMovement != 0)
                {
                    byte movementMode = Memory.ReadValue<byte>(characterMovement + CharacterMovementComponent.MovementMode);
                    byte replicatedMovementMode = Memory.ReadValue<byte>(characterMovement + Character.ReplicatedMovementMode);
                    byte replicateMovement = Memory.ReadValue<byte>(soldierActor + Actor.bReplicateMovement);
                    float maxFlySpeed = Memory.ReadValue<float>(characterMovement + CharacterMovementComponent.MaxFlySpeed);
                    float maxCustomMovementSpeed = Memory.ReadValue<float>(characterMovement + CharacterMovementComponent.MaxCustomMovementSpeed);
                    float maxAcceleration = Memory.ReadValue<float>(characterMovement + CharacterMovementComponent.MaxAcceleration);
                    
                    Program.Log($"AirStuck:");
                    Program.Log($"  - MovementMode: {movementMode} ({(EMovementMode)movementMode})");
                    Program.Log($"  - ReplicatedMovementMode: {replicatedMovementMode} ({(EMovementMode)replicatedMovementMode})");
                    Program.Log($"  - bReplicateMovement: {replicateMovement}");
                    Program.Log($"  - MaxFlySpeed: {maxFlySpeed}");
                    Program.Log($"  - MaxCustomMovementSpeed: {maxCustomMovementSpeed}");
                    Program.Log($"  - MaxAcceleration: {maxAcceleration}");
                }
                
                // HideActor
                byte hideActorReplicateMovement = Memory.ReadValue<byte>(soldierActor + Actor.bReplicateMovement);
                byte hidden = Memory.ReadValue<byte>(soldierActor + Actor.bHidden);
                
                Program.Log($"HideActor:");
                Program.Log($"  - bReplicateMovement: {hideActorReplicateMovement}");
                Program.Log($"  - bHidden: {hidden}");
                
                // Collision
                ulong rootComponent = Memory.ReadPtr(soldierActor + Actor.RootComponent);
                if (rootComponent != 0)
                {
                    ulong bodyInstanceAddr = rootComponent + 0x2c8;
                    byte collisionEnabled = Memory.ReadValue<byte>(bodyInstanceAddr + 0x20);
                    
                    Program.Log($"Collision:");
                    Program.Log($"  - CollisionEnabled: {collisionEnabled}");
                }
                
                // Quick Zoom
                ulong cameraManager = Memory.ReadPtr(_playerController + PlayerController.PlayerCameraManager);
                if (cameraManager != 0)
                {
                    float defaultFOV = Memory.ReadValue<float>(cameraManager + PlayerCameraManager.DefaultFOV);
                    
                    Program.Log($"Quick Zoom:");
                    Program.Log($"  - DefaultFOV: {defaultFOV}");
                }
                
                // Current Weapon Info
                if (currentWeapon != 0)
                {
                    Program.Log($"Current Weapon:");
                    
                    // Shooting in Main Base
                    ulong currentItemStaticInfo = Memory.ReadPtr(inventoryComponent + ASQSoldier.CurrentItemStaticInfo);
                    if (currentItemStaticInfo != 0)
                    {
                        bool usableInMainBase = Memory.ReadValue<bool>(currentItemStaticInfo + ASQSoldier.bUsableInMainBase);
                        
                        Program.Log($"Shooting in Main Base:");
                        Program.Log($"  - bUsableInMainBase: {usableInMainBase}");
                    }
                    
                    // Rapid Fire
                    ulong weaponConfigOffset = currentWeapon + ASQWeapon.WeaponConfig;
                    float timeBetweenShots = Memory.ReadValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenShots);
                    float timeBetweenSingleShots = Memory.ReadValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenSingleShots);
                    
                    Program.Log($"Rapid Fire:");
                    Program.Log($"  - TimeBetweenShots: {timeBetweenShots}");
                    Program.Log($"  - TimeBetweenSingleShots: {timeBetweenSingleShots}");
                    
                    // Infinite Ammo
                    byte infiniteAmmo = Memory.ReadValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteAmmo);
                    byte infiniteMags = Memory.ReadValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteMags);
                    byte createProjectileOnServer = Memory.ReadValue<byte>(weaponConfigOffset + FSQWeaponData.bCreateProjectileOnServer);
                    
                    Program.Log($"Infinite Ammo:");
                    Program.Log($"  - bInfiniteAmmo: {infiniteAmmo}");
                    Program.Log($"  - bInfiniteMags: {infiniteMags}");
                    Program.Log($"  - bCreateProjectileOnServer: {createProjectileOnServer}");
                    
                    // Quick Swap
                    float equipDuration = Memory.ReadValue<float>(currentWeapon + ASQEquipableItem.EquipDuration);
                    float unequipDuration = Memory.ReadValue<float>(currentWeapon + ASQEquipableItem.UnequipDuration);
                    float cachedEquipDuration = Memory.ReadValue<float>(currentWeapon + ASQEquipableItem.CachedEquipDuration);
                    float cachedUnequipDuration = Memory.ReadValue<float>(currentWeapon + ASQEquipableItem.CachedUnequipDuration);
                    
                    Program.Log($"Quick Swap:");
                    Program.Log($"  - EquipDuration: {equipDuration}");
                    Program.Log($"  - UnequipDuration: {unequipDuration}");
                    Program.Log($"  - CachedEquipDuration: {cachedEquipDuration}");
                    Program.Log($"  - CachedUnequipDuration: {cachedUnequipDuration}");
                }
                
                Program.Log("=============================");
            }
            catch (Exception ex)
            {
                Program.Log($"Error in LogCurrentValues: {ex.Message}");
            }
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