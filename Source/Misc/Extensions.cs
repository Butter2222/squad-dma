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
        public static double ToRadians(this float degrees)
        {
            return (Math.PI / 180) * degrees;
        }
        #endregion

        #region GUI Extensions
        /// <summary>
        /// Convert game position to 'Bitmap' Map Position coordinates.
        /// </summary>
        public static MapPosition ToMapPos(this System.Numerics.Vector3 vector, Map map)
        {
            return new MapPosition()
            {
                X = map.ConfigFile.X + (vector.X * map.ConfigFile.Scale),
                Y = map.ConfigFile.Y + (vector.Y * map.ConfigFile.Scale), // Invert 'Y' unity 0,0 bottom left, C# top left
                Height = vector.Z // Keep as float, calculation done later
            };
        }

        /// <summary>
        /// Gets 'Zoomed' map position coordinates.
        /// </summary>
        public static MapPosition ToZoomedPos(this MapPosition location, MapParameters mapParams)
        {
            return new MapPosition()
            {
                UIScale = mapParams.UIScale,
                TechScale = mapParams.TechScale,
                X = (location.X - mapParams.Bounds.Left) * mapParams.XScale,
                Y = (location.Y - mapParams.Bounds.Top) * mapParams.YScale,
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
                float distance = Vector3.Distance(actor.Position, targetPlayer.Position);
                
                if (distance > 300 * 100)
                    return false;

                Vector3 directionToTarget = targetPlayer.Position - actor.Position;
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
