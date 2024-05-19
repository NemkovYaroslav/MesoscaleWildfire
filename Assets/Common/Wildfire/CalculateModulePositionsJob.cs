using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Common.Wildfire
{
    public struct CalculateModulePositionsJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<Vector3> centers;
        [ReadOnly] public Matrix4x4 wildfireAreaTransform;
        
        [WriteOnly] public NativeArray<Vector4> modulesPositionTemperatureArray;
        
        public void Execute(int index, TransformAccess transform)
        {
            var moduleHolderPosition = new Vector4(transform.position.x, transform.position.y, transform.position.z, 0.0f);
            
            var modulePositionTemperature = wildfireAreaTransform * (moduleHolderPosition + transform.localToWorldMatrix * centers[index]);
            modulePositionTemperature.w = 0.0f;
            
            modulesPositionTemperatureArray[index] = modulePositionTemperature;
        }
    }
}