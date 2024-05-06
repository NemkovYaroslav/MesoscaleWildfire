using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Common.Wildfire
{
    public struct TransferDataFromGridToModulesJob : IJobParallelForTransform
    {
        [ReadOnly] public Vector3 textureResolution;
        [ReadOnly] public Matrix4x4 wildfireZoneTransform;
        [ReadOnly] public NativeArray<Vector4> textureArray;
        
        [WriteOnly] public NativeArray<float> temperatureArray;
        
        public void Execute(int index, TransformAccess transform)
        {
            var position = wildfireZoneTransform * transform.position;

            position += new Vector4(0.5f, 0.5f, 0.5f, 0.0f);
            
            position.x *= textureResolution.x;
            position.y *= textureResolution.y;
            position.z *= textureResolution.z;

            if (position.x < textureResolution.x 
                    && position.y < textureResolution.y 
                        && position.z < textureResolution.z 
                    && position.x >= 0 
                        && position.y >= 0 
                            && position.z >= 0
            )
            {
                var i = (int)position.x + (int)position.y * (int)textureResolution.x + (int)position.z * (int)textureResolution.x * (int)textureResolution.y;
                temperatureArray[index] = textureArray[i].w;
            }
        }
    }
}