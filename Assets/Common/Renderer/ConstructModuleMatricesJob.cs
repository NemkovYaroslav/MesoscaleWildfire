using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Common.Renderer
{
    public struct ConstructModuleMatricesJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<Vector3> centers;
        [ReadOnly] public NativeArray<float> heights;
        [ReadOnly] public NativeArray<float> radii;
        
        [WriteOnly] public NativeArray<Matrix4x4> matrices;
        
        public void Execute(int index, TransformAccess transform)
        {
            var moduleHolderPosition = new Vector4(transform.position.x, transform.position.y, transform.position.z, 1.0f);
            
            var modulePosition = moduleHolderPosition + transform.localToWorldMatrix * centers[index];
            var moduleRotation = transform.rotation * Quaternion.LookRotation(Vector3.up);
            var moduleScale = new Vector3(radii[index] * 2.0f, heights[index] / 2.0f, radii[index] * 2.0f);
            
            matrices[index] = Matrix4x4.TRS(modulePosition, moduleRotation, moduleScale);
        }
    }
}