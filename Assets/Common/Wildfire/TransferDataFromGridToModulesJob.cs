using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Common.Wildfire
{
    public struct TransferDataFromGridToModulesJob : IJobParallelForTransform
    {
        [ReadOnly] public Vector3 textureResolution;
        [ReadOnly] public Matrix4x4 wildfireAreaTransform;
        
        [ReadOnly] public NativeArray<Vector4> textureArray;
        
        [WriteOnly] public NativeArray<float> modulesAmbientTemperatureArray;
        
        public void Execute(int index, TransformAccess transform)
        {
            var posInWildfireArea = wildfireAreaTransform * transform.position;

            posInWildfireArea += new Vector4(0.5f, 0.5f, 0.5f, 0.0f);
            
            posInWildfireArea.x *= textureResolution.x;
            posInWildfireArea.y *= textureResolution.y;
            posInWildfireArea.z *= textureResolution.z;

            if (posInWildfireArea.x < textureResolution.x 
                    && posInWildfireArea.y < textureResolution.y 
                        && posInWildfireArea.z < textureResolution.z 
                    && posInWildfireArea.x >= 0 
                        && posInWildfireArea.y >= 0 
                            && posInWildfireArea.z >= 0
            )
            {
                var i 
                    = (int)posInWildfireArea.x 
                      + (int)posInWildfireArea.y * (int)textureResolution.x 
                        + (int)posInWildfireArea.z * (int)textureResolution.x * (int)textureResolution.y;
                
                modulesAmbientTemperatureArray[index] = textureArray[i].w;
            }
        }
    }
}