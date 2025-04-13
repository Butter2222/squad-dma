using SkiaSharp;
using System;

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

        public static SKPaint GetEntityPaint(this UActor actor)
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
                ActorType.Projectile => SKColors.Magenta,
                ActorType.ProjectileAA => SKColors.Cyan,
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

        /// <summary>
        /// Gets projectile drawing paintbrush
        /// </summary>
        public static SKPaint GetProjectilePaint(this UActor actor)
        {
            if (projectilePaint != null)
            {
                return projectilePaint;
            }

            SKPaint basePaint = SKPaints.PaintBase.Clone();
            basePaint.Color = new SKColor(255, 0, 255);
            projectilePaint = basePaint;
            return basePaint;
        }

        /// <summary>
        public static SKPaint GetTextOutlinePaint()
        {
            if (textOutlinePaint != null)
                return textOutlinePaint;

            // Create and cache the outline paint
            textOutlinePaint = new SKPaint
            {
                Color = SKColors.Black, // Outline color
                TextSize = SKPaints.TextBase.TextSize, // Match the text size
                IsStroke = true,
                StrokeWidth = 3, // Thicker outline for better visibility
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                Typeface = SKPaints.TextBase.Typeface // Use the same font
            };

            return textOutlinePaint;
        }

        #endregion
    }
}
