using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace WildfireModel.Renderer
{
    [BurstCompile(CompileSynchronously = true)]
    public struct FillMatricesAndPositionsDataJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<Vector3> centers;
        [ReadOnly] public NativeArray<float> heights;
        [ReadOnly] public NativeArray<float> radii;
        [WriteOnly] public NativeArray<Matrix4x4> matrices;
        
        [ReadOnly] public Matrix4x4 wildfireAreaWorldToLocal;
        [WriteOnly] public NativeArray<Vector4> modulesWildfireAreaPosition;
        
        public void Execute(int index, TransformAccess transform)
        {
            var moduleHolderPosition = new Vector4(transform.position.x, transform.position.y, transform.position.z, 1.0f);
            
            // world module position, rotation, scale
            var moduleWorldPosition = moduleHolderPosition + transform.localToWorldMatrix * centers[index];
            var moduleWorldRotation = transform.rotation * Quaternion.LookRotation(Vector3.up);
            var moduleWorldScale = new Vector3(radii[index] * 2.0f, heights[index] / 2.0f, radii[index] * 2.0f);
            
            matrices[index] = Matrix4x4.TRS(moduleWorldPosition, moduleWorldRotation, moduleWorldScale);
            
            // wildfire area local module position
            var moduleWildfireAreaLocalPosition = wildfireAreaWorldToLocal * moduleWorldPosition;
            modulesWildfireAreaPosition[index] = moduleWildfireAreaLocalPosition;
        }
    }
}