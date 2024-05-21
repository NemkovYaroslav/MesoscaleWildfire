﻿using Common.Renderer;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    public class Module : MonoBehaviour
    {
        private const float IgnitionTemperature = 0.15f;
        private const float AttenuationTemperature = 0.45f;
        private const float WoodDensity = 800.0f;
        
        public float temperature = 0.0f;
        public float stopCombustionMass = 0.0f;

        public Transform transForm;
        public Rigidbody rigidBody;
        public CapsuleCollider capsuleCollider;
        public FixedJoint fixedJoint;
        
        public Module neighbourModule;

        private ModuleRenderer _moduleRenderer;
        
        public bool isTrunk;

        private void Awake()
        {
            transForm       = transform;
            rigidBody       = GetComponent<Rigidbody>();
            capsuleCollider = GetComponent<CapsuleCollider>();
            fixedJoint      = GetComponent<FixedJoint>();

            // get neighbour from joint
            if (fixedJoint)
            {
                if (fixedJoint.connectedBody.TryGetComponent(out Module module))
                {
                    neighbourModule = module;
                }
            }

            _moduleRenderer = GameObject.FindWithTag("ModulesRenderer").GetComponent<ModuleRenderer>();
            
            stopCombustionMass = rigidBody.mass * 0.1f;
        }
        
        public float CalculateLostMass()
        {
            var surfaceArea = 2.0f * Mathf.PI * capsuleCollider.radius * capsuleCollider.height;

            var reactionRate = 1.0f;
            
            if (temperature < IgnitionTemperature)
            {
                reactionRate = 0.0f;
            }
            if (temperature > IgnitionTemperature && temperature < AttenuationTemperature)
            {
                var argument = (temperature - IgnitionTemperature) / (AttenuationTemperature - IgnitionTemperature);
                reactionRate = 3.0f * Mathf.Pow(argument, 2.0f) - 2.0f * Mathf.Pow(argument, 3.0f);
            }
            if (temperature > AttenuationTemperature)
            {
                reactionRate = 1;
            }

            const float thickness = 0.01f;
            
            var lostMass = reactionRate * surfaceArea * thickness;

            return lostMass;
        }

        public void RecalculateCharacteristics(float lostMass)
        {
            var mass = rigidBody.mass - lostMass;
            
            rigidBody.mass = mass;
            
            capsuleCollider.radius = Mathf.Sqrt(mass / (Mathf.PI * WoodDensity * capsuleCollider.height));
        }

        private void OnDestroy()
        {
            //var index = _moduleRenderer.orderedModuleList.IndexOf(this);
            //_moduleRenderer.transformAccessArray.RemoveAtSwapBack(index);
            //_moduleRenderer.orderedModuleList.RemoveAt(index);
        }
    }
}