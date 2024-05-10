using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Common.Renderer
{
    public struct ModuleRenderJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<Vector3> centers;
        [ReadOnly] public NativeArray<float> heights;
        [ReadOnly] public NativeArray<float> radii;
        
        [WriteOnly] public NativeArray<Matrix4x4> matrices;
        
        public void Execute(int index, TransformAccess transform)
        {
            var modulePosition = new Vector4(transform.position.x, transform.position.y, transform.position.z, 1.0f);
            var colliderPosition = modulePosition + transform.localToWorldMatrix * centers[index];
            
            var colliderRotation = transform.rotation * Quaternion.LookRotation(Vector3.up);
            var colliderScale = new Vector3(radii[index] * 2.0f, heights[index] / 2.0f, radii[index] * 2.0f);
            
            matrices[index] = Matrix4x4.TRS(colliderPosition, colliderRotation, colliderScale);
        }
    }
}