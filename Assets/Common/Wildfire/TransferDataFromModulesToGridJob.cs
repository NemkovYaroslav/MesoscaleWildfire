﻿using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Common.Wildfire
{
    public struct TransferDataFromModulesToGridJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<Vector3> centers;
        [ReadOnly] public Matrix4x4 wildfireZoneTransform;

        [WriteOnly] public NativeArray<Vector4> positionEnergyArray;
        
        public void Execute(int index, TransformAccess transform)
        {
            var modulePosition = new Vector4(transform.position.x, transform.position.y, transform.position.z, 1.0f);
            var data = wildfireZoneTransform * (modulePosition + transform.localToWorldMatrix * centers[index]);

            // energy
            data.w = 0.01f;

            positionEnergyArray[index] = data;
        }
    }
}