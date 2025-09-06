using SkiaSharp;
using System;
using System.Numerics;

namespace squad_dma
{
    /// <summary>
    /// Extension methods go here.
    /// </summary>
    public static class Extensions
    {
        private static SKPaint textOutlinePaint = null;
        private static SKPaint projectilePaint = null;
        private static Dictionary<Team, SKPaint> teamEntityPaints = [];
        private static Dictionary<Team, SKPaint> teamTextPaints = [];

        #region Vector Conversion Extensions
        /// <summary>
        /// Convert System.Numerics.Vector3 to Vector3D
        /// </summary>
        public static Vector3D ToVector3D(this Vector3 vector)
        {
            return new Vector3D(vector.X, vector.Y, vector.Z);
        }

        /// <summary>
        /// Convert Vector3D to System.Numerics.Vector3
        /// </summary>
        public static Vector3 ToVector3(this Vector3D vector)
        {
            return new Vector3((float)vector.X, (float)vector.Y, (float)vector.Z);
        }

        /// <summary>
        /// Convert System.Numerics.Vector2 to Vector2D
        /// </summary>
        public static Vector2D ToVector2D(this Vector2 vector)
        {
            return new Vector2D(vector.X, vector.Y);
        }

        /// <summary>
        /// Convert Vector2D to System.Numerics.Vector2
        /// </summary>
        public static Vector2 ToVector2(this Vector2D vector)
        {
            return new Vector2((float)vector.X, (float)vector.Y);
        }
        #endregion

        #region Generic Extensions
        /// <summary>
        /// Restarts a timer from 0. (Timer will be started if not already running)
        /// </summary>
        public static void Restart(this System.Timers.Timer t)
        {
            t.Stop();
            t.Start();
        }

        /// <summary>
        /// Converts 'Degrees' to 'Radians'.
        /// </summary>
        public static double ToRadians(this double degrees)
        {
            return (Math.PI / 180.0) * degrees;
        }
        #endregion

        #region GUI Extensions
        /// <summary>
        /// Convert game position to 'Bitmap' Map Position coordinates.
        /// </summary>
        public static MapPosition ToMapPos(this Vector3D vector, Map map)
        {
            if (map?.ConfigFile == null)
                return new MapPosition();

            double worldX = vector.X - Memory.AbsoluteLocation.X;
            double worldY = vector.Y - Memory.AbsoluteLocation.Y;

            return new MapPosition()
            {
                X = map.ConfigFile.X + (worldX * map.ConfigFile.Scale),
                Y = map.ConfigFile.Y + (worldY * map.ConfigFile.Scale),
                Height = vector.Z
            };
        }

        /// <summary>
        /// Gets 'Zoomed' map position coordinates.
        /// </summary>
        public static MapPosition ToZoomedPos(this MapPosition location, MapParameters mapParams)
        {
            double finalX = (location.X - mapParams.Bounds.Left) * mapParams.XScale;
            double finalY = (location.Y - mapParams.Bounds.Top) * mapParams.YScale;
            
            return new MapPosition()
            {
                UIScale = mapParams.UIScale,
                TechScale = mapParams.TechScale,
                X = finalX,
                Y = finalY,
                Height = location.Height
            };
        }

        public static SKPaint GetActorPaint(this UActor actor)
        {
            SKColor color = actor.IsInMySquad() ? SKPaints.Squad
                          : actor.IsFriendly() ? SKPaints.Friendly
                          : SKPaints.Enemy;

            if (teamEntityPaints.TryGetValue(actor.Team, out SKPaint cachedPaint))
            {
                cachedPaint.Color = color;
                return cachedPaint;
            }

            SKPaint newPaint = SKPaints.PaintBase.Clone();
            newPaint.Color = color;
            return newPaint;
        }

        public static SKPaint GetTextPaint(this UActor actor)
        {
            SKColor textColor = actor.ActorType switch
            {
                ActorType.Player => actor.IsFriendly() ? SKColors.Blue : SKColors.Red,
                ActorType.Projectile => SKColors.Orange,
                ActorType.ProjectileAA => SKColors.Orange,
                ActorType.ProjectileSmall => SKPaints.SmallProjectile,
                _ when Names.Vehicles.Contains(actor.ActorType) => SKPaints.Vehicle,
                _ => SKPaints.DefaultTextColor // Default
            };

            if (!teamTextPaints.TryGetValue(actor.Team, out SKPaint paint))
            {
                paint = SKPaints.TextBase.Clone();
                paint.Color = textColor;
                teamTextPaints[actor.Team] = paint;
            }
            else if (paint.Color != textColor)
            {
                paint.Color = textColor; 
            }

            return paint;
        }

        public static bool IsLookingAtPlayer(this UActor actor, UActor targetPlayer, float maxAngle = 5f)
        {
            if (actor?.Position == null || actor?.Rotation == null || targetPlayer?.Position == null || actor.IsFriendly()) 
                return false;

            try
            {
                Vector3 actorPos = actor.Position.ToVector3();
                Vector3 targetPos = targetPlayer.Position.ToVector3();
                float distance = Vector3.Distance(actorPos, targetPos);
                
                if (distance > 300 * 100)
                    return false;

                Vector3 directionToTarget = targetPos - actorPos;
                directionToTarget = Vector3.Normalize(directionToTarget);

                float radians = (float)actor.Rotation.X.ToRadians();
                Vector3 forwardVector = new Vector3(
                    (float)Math.Cos(radians),
                    (float)Math.Sin(radians),
                    0
                );

                float dotProduct = Vector3.Dot(directionToTarget, forwardVector);
                float angle = (float)(Math.Acos(dotProduct) * (180.0 / Math.PI));

                float angleThreshold = 31.3573f - 3.51726f * (float)Math.Log(Math.Abs(0.626957f - 15.6948f * distance));
                if (angleThreshold < 1f)
                    angleThreshold = 1f; 

                float finalThreshold = Math.Min(angleThreshold, maxAngle);

                return angle <= finalThreshold;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}
