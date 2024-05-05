using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Common.Wildfire
{
    public struct ModuleWildfireJob : IJobParallelForTransform
    {
        [ReadOnly] public Vector3 textureResolution;
        
        [ReadOnly] public Matrix4x4 wildfireZoneTransform;
        
        [WriteOnly] public NativeArray<Vector4> texturePositions;
        
        public void Execute(int index, TransformAccess transform)
        {
            var position = wildfireZoneTransform * transform.position;

            position += new Vector4(0.5f, 0.5f, 0.5f, 0.0f);
            
            position.x *= textureResolution.x;
            position.y *= textureResolution.y;
            position.z *= textureResolution.z;
            position.w = 0.0f;

            position -= new Vector4(0.5f, 0.5f, 0.5f, 0.0f);

            texturePositions[index] = position;
        }
    }
}