using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    //[RequireComponent(typeof(ModulePrototypesGenerator))]
    public class ModulesGenerator : MonoBehaviour
    {
        private ModulePrototypesGenerator _modulePrototypesGenerator;
        
        private void OnDrawGizmos()
        {
            var children = transform.GetComponentsInChildren<Transform>();
            foreach (var child in children)
            {
                if (child.childCount > 0)
                {
                    // draw modules connected line
                    UnityEditor.Handles.color = Color.yellow;
                    var parentPosition = child.position;
                    var currentPosition = child.GetChild(0).position;
                    UnityEditor.Handles.DrawLine(parentPosition, currentPosition);
                    UnityEditor.Handles.color = Color.white;
                    for (var i = 0; i < child.childCount - 1; i++)
                    {
                        currentPosition = child.GetChild(i).position;
                        var nextPosition = child.GetChild(i + 1).position;
                        UnityEditor.Handles.DrawLine(currentPosition, nextPosition);
                    }
                }
                
                ///*
                // draw module radius and look direction
                UnityEditor.Handles.color = Color.red;
                var position = child.position;
                var normal = child.forward.normalized;
                if (child.TryGetComponent(out ModulePrototypeData modulePrototypeData))
                {
                    var currentRad = modulePrototypeData.radius;
                    UnityEditor.Handles.DrawSolidDisc(position, normal, currentRad);
                    UnityEditor.Handles.color = Color.blue;
                    UnityEditor.Handles.DrawLine(position, position + normal * 0.1f);
                }
                //*/
            }
        }
        
        public void GenerateModulesOnPath()
        {
            // destroy ModulePrototypesGenerator component
            var modulePrototypesGeneratorComponents = transform.GetComponentsInChildren<ModulePrototypesGenerator>();
            foreach (var modulePrototypesGeneratorComponent in modulePrototypesGeneratorComponents)
            {
                DestroyImmediate(modulePrototypesGeneratorComponent);
            }
            
            // destroy PathCreator component
            var pathCreatorComponents = transform.GetComponentsInChildren<Objects.PathCreator>();
            foreach (var pathCreatorComponent in pathCreatorComponents)
            {
                DestroyImmediate(pathCreatorComponent);
            }
            
            // copy previous radius data
            var children = transform.GetComponentsInChildren<Transform>();
            foreach (var child in children)
            {
                if (child.childCount > 0)
                {
                    for (var i = child.childCount - 1; i > 0; i--)
                    {
                        var previousModule = child.GetChild(i - 1).gameObject;
                        var previousModuleRadius = previousModule.GetComponent<ModulePrototypeData>().radius;

                        var module = child.GetChild(i).gameObject;
                        module.GetComponent<ModulePrototypeData>().previousRadius = previousModuleRadius;
                    }
                }
            }
            
            // destroy zero position modules
            var modulePrototypesData = transform.GetComponentsInChildren<ModulePrototypeData>();
            foreach (var modulePrototypeData in modulePrototypesData)
            {
                if (Mathf.Approximately(modulePrototypeData.step, 0.0f))
                {
                    DestroyImmediate(modulePrototypeData.gameObject);
                }
            }
            
            // change modules rotation
            foreach (var child in children)
            {
                if (child.childCount > 0)
                {
                    for (var i = 0; i < child.childCount - 1; i++)
                    {
                        var module = child.GetChild(i).gameObject;
                        var nextModule = child.GetChild(i + 1).gameObject;

                        var direction = (module.transform.position - nextModule.transform.position).normalized;
                        
                        
                    }
                }
            }
            
            
        }
    }
}