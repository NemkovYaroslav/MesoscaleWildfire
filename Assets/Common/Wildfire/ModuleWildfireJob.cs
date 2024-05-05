using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Common.Wildfire
{
    public struct ModuleWildfireJob : IJobParallelForTransform
    {
        [ReadOnly] public Vector3 textureResolution;
        [ReadOnly] public Matrix4x4 wildfireZoneTransform;
        [ReadOnly] public NativeArray<Vector4> textureArray;
        
        //[WriteOnly] public NativeArray<float> temperatureArray;
        
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
                Debug.Log("mod pos: " + new Vector3((int)position.x, (int)position.y, (int)position.z));
            
                var i = (int)position.x + (int)position.y * (int)textureResolution.x + (int)position.z * (int)textureResolution.x * (int)textureResolution.y;

                var array = textureArray.ToArray();

                Debug.Log("i: " + i + " tem pos: " + array[i]);
            }
            else
            {
                Debug.Log("ERROR");
            }
            
            //temperatureArray[index] = textureArray[i].w;
        }
    }
}