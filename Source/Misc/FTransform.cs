using System.Numerics;

namespace squad_dma
{
    /// <summary>
    /// FTransform struct compatible with Unreal Engine 5 using double precision
    /// </summary>
    public struct FTransform
    {
        public Quaternion Rotation;
        public Vector3 Translation;
        public Vector3 Scale3D;

        public FTransform(Quaternion rotation, Vector3 translation, Vector3 scale3D)
        {
            Rotation = rotation;
            Translation = translation;
            Scale3D = scale3D;
        }

        public Matrix4x4 ToMatrix()
        {
            return Matrix4x4.CreateFromQuaternion(Rotation) *
                   Matrix4x4.CreateScale(Scale3D) *
                   Matrix4x4.CreateTranslation(Translation);
        }

        /// <summary>
        /// Convert to Matrix4x4 with scale included (matching the working code's approach)
        /// </summary>
        public Matrix4x4 ToMatrixWithScale()
        {
            // Create the transformation matrix with scale properly applied
            return Matrix4x4.CreateScale(Scale3D) * 
                   Matrix4x4.CreateFromQuaternion(Rotation) * 
                   Matrix4x4.CreateTranslation(Translation);
        }

        /// <summary>
        /// Convert to Vector3D for compatibility with the project's custom vector types
        /// </summary>
        public Vector3D ToVector3D()
        {
            return new Vector3D(Translation.X, Translation.Y, Translation.Z);
        }

        /// <summary>
        /// Create FTransform from Vector3D
        /// </summary>
        public static FTransform FromVector3D(Vector3D position, Quaternion rotation = default, Vector3 scale = default)
        {
            if (scale == default)
                scale = Vector3.One;
            
            return new FTransform(rotation, new Vector3((float)position.X, (float)position.Y, (float)position.Z), scale);
        }
    }
}


