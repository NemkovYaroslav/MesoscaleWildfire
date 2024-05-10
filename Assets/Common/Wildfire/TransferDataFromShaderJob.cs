using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Common.Wildfire
{
    public struct TransferDataFromShaderJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<Vector3> centers;
        [ReadOnly] public Matrix4x4 wildfireAreaTransform;
        
        [WriteOnly] public NativeArray<Vector4> posTempArray;
        
        public void Execute(int index, TransformAccess transform)
        {
            var position = new Vector4(transform.position.x, transform.position.y, transform.position.z, 0.0f);
            var data = wildfireAreaTransform * (position + transform.localToWorldMatrix * centers[index]);
            data.w = 0.0f;
            posTempArray[index] = data;
        }
    }
}