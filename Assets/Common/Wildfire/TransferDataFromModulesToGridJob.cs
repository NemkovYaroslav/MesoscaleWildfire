using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Common.Wildfire
{
    public struct TransferDataFromModulesToGridJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<Vector3> centers;
        [ReadOnly] public Matrix4x4 wildfireZoneTransform;
        [ReadOnly] public NativeArray<float> releaseTemperatureArray;

        [WriteOnly] public NativeArray<Vector4> positionTemperatureArray;
        
        public void Execute(int index, TransformAccess transform)
        {
            var position = new Vector4(transform.position.x, transform.position.y, transform.position.z, 1.0f);
            var data = wildfireZoneTransform * (position + transform.localToWorldMatrix * centers[index]);
            
            data.w = releaseTemperatureArray[index];
            
            positionTemperatureArray[index] = data;
        }
    }
}