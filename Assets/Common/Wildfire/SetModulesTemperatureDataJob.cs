using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Common.Wildfire
{
    public struct SetModulesTemperatureDataJob : IJobParallelForTransform
    {
        [ReadOnly]  public NativeArray<Vector4> modulePositionsArray;
        [ReadOnly]  public NativeArray<float>   releaseTemperatureArray;
        [WriteOnly] public NativeArray<Vector4> modulesWildfireAreaPositionTemperatureArray;
        
        public void Execute(int index, TransformAccess transform)
        {
            var data = modulePositionsArray[index];
            data.w = releaseTemperatureArray[index];
            modulesWildfireAreaPositionTemperatureArray[index] = data;
        }
    }
}