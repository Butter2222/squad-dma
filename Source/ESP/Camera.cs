using System;
using System.Numerics;

namespace squad_dma
{
    public struct ViewMatrix
    {
        public float[,] matrix;

        public ViewMatrix(float[,] values)
        {
            matrix = values;
        }
    }

    public struct MinimalViewInfo
    {
        public Vector3 Location;
        public Vector3 Rotation;
        public float FOV;
    }

    public static class Camera
    {
        private static readonly float DEG_TO_RAD = (float)(Math.PI / 180.0);

        public static ViewMatrix CreateMatrix(Vector3 rot, Vector3 origin)
        {
            float radPitch = rot.X * DEG_TO_RAD;
            float radYaw = rot.Y * DEG_TO_RAD;
            float radRoll = rot.Z * DEG_TO_RAD;

            float SP = (float)Math.Sin(radPitch);
            float CP = (float)Math.Cos(radPitch);
            float SY = (float)Math.Sin(radYaw);
            float CY = (float)Math.Cos(radYaw);
            float SR = (float)Math.Sin(radRoll);
            float CR = (float)Math.Cos(radRoll);

            float[,] matrix = new float[4, 4];
            matrix[0, 0] = CP * CY;
            matrix[0, 1] = CP * SY;
            matrix[0, 2] = SP;
            matrix[0, 3] = 0f;

            matrix[1, 0] = SR * SP * CY - CR * SY;
            matrix[1, 1] = SR * SP * SY + CR * CY;
            matrix[1, 2] = -SR * CP;
            matrix[1, 3] = 0f;

            matrix[2, 0] = -(CR * SP * CY + SR * SY);
            matrix[2, 1] = CY * SR - CR * SP * SY;
            matrix[2, 2] = CR * CP;
            matrix[2, 3] = 0f;

            matrix[3, 0] = origin.X;
            matrix[3, 1] = origin.Y;
            matrix[3, 2] = origin.Z;
            matrix[3, 3] = 1f;

            return new ViewMatrix(matrix);
        }

        public static Vector2 WorldToScreen(MinimalViewInfo viewInfo, Vector3 world)
        {
            Vector3 screenLocation = Vector3.Zero;
            Vector3 rot = new Vector3(viewInfo.Rotation.X, viewInfo.Rotation.Y, viewInfo.Rotation.Z);
            Vector3 camPos = viewInfo.Location;

            ViewMatrix tempMatrix = CreateMatrix(rot, Vector3.Zero);

            Vector3 vAxisX = new Vector3(tempMatrix.matrix[0, 0], tempMatrix.matrix[0, 1], tempMatrix.matrix[0, 2]);
            Vector3 vAxisY = new Vector3(tempMatrix.matrix[1, 0], tempMatrix.matrix[1, 1], tempMatrix.matrix[1, 2]);
            Vector3 vAxisZ = new Vector3(tempMatrix.matrix[2, 0], tempMatrix.matrix[2, 1], tempMatrix.matrix[2, 2]);

            Vector3 vDelta = world - camPos;
            Vector3 vTransformed = new Vector3(
                Vector3.Dot(vDelta, vAxisY),
                Vector3.Dot(vDelta, vAxisZ),
                Vector3.Dot(vDelta, vAxisX)
            );

            if (vTransformed.Z < 1f)
                vTransformed.Z = 1f;

            const float FOV_DEG_TO_RAD = (float)(Math.PI / 360.0);
            int centreX = Screen.PrimaryScreen.Bounds.Width / 2;
            int centreY = Screen.PrimaryScreen.Bounds.Height / 2;

            screenLocation.X = centreX + vTransformed.X * (centreX / (float)Math.Tan(viewInfo.FOV * FOV_DEG_TO_RAD)) / vTransformed.Z;
            screenLocation.Y = centreY - vTransformed.Y * (centreX / (float)Math.Tan(viewInfo.FOV * FOV_DEG_TO_RAD)) / vTransformed.Z;

            return new Vector2(screenLocation.X, screenLocation.Y);
        }
    }
}