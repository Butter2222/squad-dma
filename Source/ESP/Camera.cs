using System;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;


namespace squad_dma
{
    public struct FMatrix
    {
        public float[,] matrix;

        public FMatrix(float[,] values)
        {
            matrix = values;
        }

        public static FMatrix Multiply(FMatrix a, FMatrix b)
        {
            float[,] result = new float[4, 4];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    result[i, j] = a.matrix[i, 0] * b.matrix[0, j] +
                                   a.matrix[i, 1] * b.matrix[1, j] +
                                   a.matrix[i, 2] * b.matrix[2, j] +
                                   a.matrix[i, 3] * b.matrix[3, j];
                }
            }
            return new FMatrix(result);
        }

        public static FMatrix operator *(FMatrix a, FMatrix b)
        {
            return FMatrix.Multiply(a, b);
        }
    }

    public struct MinimalViewInfo
    {
        public Vector3D Location;
        public Vector3D Rotation;
        public float FOV;
        public float AspectRatio;

        //FMinimalViewInfo::CalculateProjectionMatrix
        public FMatrix CalculateProjectionMatrix()
        {
            float halfFOV = Single.DegreesToRadians(Math.Max(0.001f, FOV) / 2f);
            FMatrix projectionMatrix = Camera.CalculateReversedZPerspectiveMatrix(
                halfFOV,
                AspectRatio,
                1f,
                Camera.GNearClippingPlane
            );

            return projectionMatrix;
        }
    }

    public static class Camera
    {
        public static FMatrix ViewProjectionMatrix;
        public static Vector3D Location;
        public static Vector2 ViewportRect;

        public static readonly float GNearClippingPlane = 0.01f; // 0.001f m -> 0.01f cm
        public static readonly float WorldToScreenTolerance = 0.001f; // safe and small enough
        private static readonly FMatrix Planes = new FMatrix(new float[,] {
            { 0, 0, 1, 0 },
            { 1, 0, 0, 0 },
            { 0, 1, 0, 0 },
            { 0, 0, 0, 1 }
        });

        // FInverseRotationMatrix
        public static FMatrix CalculateInverseRotationMatrix(Vector3 rotation)
        {
            float radPitch = Single.DegreesToRadians(rotation.X);
            float radYaw = Single.DegreesToRadians(rotation.Y);
            float radRoll = Single.DegreesToRadians(rotation.Z);
            
            float SP = Single.Sin(radPitch);
            float CP = Single.Cos(radPitch);
            float SY = Single.Sin(radYaw);
            float CY = Single.Cos(radYaw);
            float SR = Single.Sin(radRoll);
            float CR = Single.Cos(radRoll);

            FMatrix mYaw = new FMatrix(new float[4, 4] {
                { CY, -SY, 0, 0 },
                { SY,  CY, 0, 0 },
                { 0,   0,  1, 0 },
                { 0,   0,  0, 1 },
            });

            FMatrix mPitch = new FMatrix(new float[4, 4] {
                { CP,  0, -SP, 0 },
                { 0,   1,  0,  0 },
                { SP,  0,  CP, 0 },
                { 0,   0,  0,  1 },
            });

            FMatrix mRoll = new FMatrix(new float[4, 4] {
                { 1,  0,  0,  0 },
                { 0,  CR, SR, 0 },
                { 0, -SR, CR, 0 },
                { 0,  0,  0,  1 },
            });

            return mYaw * mPitch * mRoll;
        }

        public static FMatrix CalculateTranslationMatrix(Vector3 translation)
        {
            float[,] matrix = new float[4, 4] {
                { 1f,            0f,            0f,            0f },
                { 0f,            1f,            0f,            0f },
                { 0f,            0f,            1f,            0f },
                { translation.X, translation.Y, translation.Z, 1f }
            };

            return new FMatrix(matrix);
        }

        //FReversedZPerspectiveMatrix
        public static FMatrix CalculateReversedZPerspectiveMatrix(float halfFOV, float width, float height, float minZ)
        {
            float[,] matrix = new float[4, 4] {
                { 1f / Single.Tan(halfFOV), 0f,                                   0f,   0f },
                { 0f,                       width / Single.Tan(halfFOV) / height, 0f,   0f },
                { 0f,                       0f,                                   0f,   1f },
                { 0f,                       0f,                                   minZ, 0f }
            };

            return new FMatrix(matrix);
        }

        // UGameplayStatics::CalculateViewProjectionMatricesFromMinimalView
        public static FMatrix CalculateViewProjectionMatrix(MinimalViewInfo viewInfo)
        {
            FMatrix viewRotationMatrix = CalculateInverseRotationMatrix(viewInfo.Rotation.ToVector3()) * Planes;

            FMatrix viewMatrix = CalculateTranslationMatrix(-viewInfo.Location.ToVector3()) * viewRotationMatrix;

            FMatrix projectionMatrix = viewInfo.CalculateProjectionMatrix();

            return viewMatrix * projectionMatrix;
        }

        public static Vector2 WorldToScreen(MinimalViewInfo viewInfo, Vector3D world)
        {
            if (viewInfo.AspectRatio == 0f && ViewportRect.Y > 0f)
                viewInfo.AspectRatio = ViewportRect.X / ViewportRect.Y;

            var vpMatrix = CalculateViewProjectionMatrix(viewInfo);

            float w = (float)(world.X * vpMatrix.matrix[0, 3] + world.Y * vpMatrix.matrix[1, 3] + world.Z * vpMatrix.matrix[2, 3] + vpMatrix.matrix[3, 3]);
            if (w < WorldToScreenTolerance)
                return Vector2.Zero;

            float x = (float)(world.X * vpMatrix.matrix[0, 0] + world.Y * vpMatrix.matrix[1, 0] + world.Z * vpMatrix.matrix[2, 0] + vpMatrix.matrix[3, 0]);
            float y = (float)(world.X * vpMatrix.matrix[0, 1] + world.Y * vpMatrix.matrix[1, 1] + world.Z * vpMatrix.matrix[2, 1] + vpMatrix.matrix[3, 1]);

            float invW = 1f / w;
            float pixelX = (0.5f + x * 0.5f * invW) * ViewportRect.X;
            float pixelY = (0.5f - y * 0.5f * invW) * ViewportRect.Y;

            return new Vector2(pixelX, pixelY);
        }

        /// <summary>
        /// Scope view of the weapon or vehicle sight the player is currently aimed through. The PIP capture renders
        /// from the aim point with its own square FOV, so it reuses the main camera's position and rotation.
        /// </summary>
        public static bool OpticActive;
        public static float OpticFOV;
        public static float MainFOV = 90f;
        public static FMatrix OpticViewProjectionMatrix;

        public static void SetOptic(MinimalViewInfo mainView, float opticFov)
        {
            OpticActive = opticFov > 0f;
            OpticFOV = opticFov;
            if (!OpticActive)
                return;

            mainView.FOV = opticFov;
            mainView.AspectRatio = 1f;
            OpticViewProjectionMatrix = CalculateViewProjectionMatrix(mainView);
        }

        /// <summary>
        /// Projects a world position into normalized device coordinates (-1..1, +Y up) through the main view,
        /// or through the optic view when <paramref name="optic"/> is set. Returns false behind the camera.
        /// </summary>
        public static bool TryProject(Vector3D world, bool optic, out Vector2 ndc, out float w)
        {
            var m = optic ? OpticViewProjectionMatrix.matrix : ViewProjectionMatrix.matrix;
            ndc = default;
            w = 0f;
            if (m == null)
                return false;

            w = (float)(world.X * m[0, 3] + world.Y * m[1, 3] + world.Z * m[2, 3] + m[3, 3]);
            if (w < WorldToScreenTolerance)
                return false;

            float x = (float)(world.X * m[0, 0] + world.Y * m[1, 0] + world.Z * m[2, 0] + m[3, 0]);
            float y = (float)(world.X * m[0, 1] + world.Y * m[1, 1] + world.Z * m[2, 1] + m[3, 1]);

            float invW = 1f / w;
            ndc = new Vector2(x * invW, y * invW);
            return true;
        }

        /// <summary>Horizontal focal scale of the view: lateral cm at depth w map to cm * scale / w in NDC.</summary>
        public static float LateralScale(bool optic)
        {
            float fov = optic ? OpticFOV : MainFOV;
            return 1f / Single.Tan(Single.DegreesToRadians(Math.Max(0.001f, fov) * 0.5f));
        }

        public static Vector2 NdcToViewport(Vector2 ndc)
        {
            return new Vector2((0.5f + ndc.X * 0.5f) * ViewportRect.X, (0.5f - ndc.Y * 0.5f) * ViewportRect.Y);
        }

        public static Vector2 WorldToScreen(Vector3D world)
        {
            return TryProject(world, false, out var ndc, out _) ? NdcToViewport(ndc) : Vector2.Zero;
        }
    }
}