using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    [RequireComponent(typeof(Objects.PathCreator))]
    public class ModulePrototypesGenerator : MonoBehaviour
    {
        [HideInInspector] public Objects.PathCreator pathCreator;
        
        [HideInInspector] public bool areRadiiAutoCalculated = true;
        [HideInInspector] public float startSpawnRadius = 0.1f;
        [HideInInspector] public float finalSpawnRadius = 0.0f;
        [HideInInspector] public float constSpawnRadius = 0.1f;

        [SerializeField] private Mesh debugMesh;
        
        private void OnDrawGizmos()
        {
            var children = GetComponentsInChildren<Transform>();
            foreach (var child in children)
            {
                if (child.gameObject.TryGetComponent(out ModulePrototypeData modulePrototypeData))
                {
                    UnityEditor.Handles.color = Color.red;
                    var position = child.position;
                    //var rotation = child.rotation;
                    //var scale = new Vector3(capsule.radius * 2.0f, capsule.height / 2.0f, capsule.radius * 2.0f);
                    var normal = child.forward.normalized;
                    UnityEditor.Handles.DrawSolidDisc(position, normal, modulePrototypeData.radius);
                    
                    //Gizmos.DrawWireMesh(debugMesh, -1, position, rotation, scale);
                }
            }
        }

        public void SortModules()
        {
            var unorderedModulePrototypeDataArray 
                = transform.GetComponentsInChildren<ModulePrototypeData>();
            var orderedModulePrototypeDataArray 
                = unorderedModulePrototypeDataArray.OrderBy(property => property.step).ToArray();
            for (var i = 0; i < orderedModulePrototypeDataArray.Length; i++)
            {
                var modulePrototypeData = orderedModulePrototypeDataArray[i];
                foreach (Transform child in transform)
                {
                    if (child.TryGetComponent(out ModulePrototypeData childModulePlacer))
                    {
                        if (childModulePlacer == modulePrototypeData)
                        {
                            child.SetSiblingIndex(i);
                            break;
                        }
                    }
                }
            }
        } 

        private void InstantiateModulePrototype(float step)
        {
            var prototype = new GameObject(step.ToString(CultureInfo.CurrentCulture), typeof(ModulePrototypeData));

            var path = pathCreator.Path;
            
            var pos = path.GetPointAtTime(step);
            var rot = path.GetRotation(step);
            
            var objectTransform = prototype.GetComponent<Transform>();
            objectTransform.SetPositionAndRotation(pos, rot);
            objectTransform.SetParent(transform);
            
            var prototypeData = prototype.GetComponent<ModulePrototypeData>();
            prototypeData.step = step;

            if (areRadiiAutoCalculated)
            {
                prototypeData.radius = Mathf.Lerp(startSpawnRadius, finalSpawnRadius, step);
            }
            else
            {
                prototypeData.radius = constSpawnRadius;
            }
        }

        private static void DeleteExcessChildren(Transform parent)
        {
            for (var i = 0; i < parent.childCount;)
            {
                var child = parent.GetChild(i).gameObject;
                if (child.TryGetComponent(out ModulePrototypeData _))
                {
                    i++;
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }
        
        public void AddModulePrototypeToPath()
        {
            DeleteExcessChildren(transform);
            
            if (transform.childCount == 0)
            {
                InstantiateModulePrototype(0.0f);
            }
            else
            {
                if (transform.childCount == 1)
                {
                    var prototype = transform.GetChild(0).gameObject;
                    var prototypeData = prototype.GetComponent<ModulePrototypeData>();
                    if (!Mathf.Approximately(prototypeData.step, 0.0f))
                    {
                        InstantiateModulePrototype(0.0f);
                    }
                    if (!Mathf.Approximately(prototypeData.step, 1.0f))
                    {
                        InstantiateModulePrototype(1.0f);
                    }
                }
                else
                {
                    var startPrototype = transform.GetChild(0).gameObject;
                    var startPrototypeData = startPrototype.GetComponent<ModulePrototypeData>();
                    if (!Mathf.Approximately(startPrototypeData.step, 0.0f))
                    {
                        InstantiateModulePrototype(0.0f);
                    }
                    var endPrototype = transform.GetChild(transform.childCount - 1).gameObject;
                    var endPrototypeData = endPrototype.GetComponent<ModulePrototypeData>();
                    if (!Mathf.Approximately(endPrototypeData.step, 1.0f))
                    {
                        InstantiateModulePrototype(1.0f);
                    }
                    
                    var maxDifference = float.MinValue;
                    var average = 0.0f;
                    for (var i = 0; i < transform.childCount - 1; i++)
                    {
                        var firstPrototype = transform.GetChild(i).GetComponent<ModulePrototypeData>();
                        var secondPrototype = transform.GetChild(i + 1).GetComponent<ModulePrototypeData>();
                        
                        var difference = secondPrototype.step - firstPrototype.step;
                        if (difference > maxDifference)
                        {
                            maxDifference = difference;
                            average = (firstPrototype.step + secondPrototype.step) / 2.0f;
                        }
                    }
                    InstantiateModulePrototype(average);
                    
                    SortModules();
                }
            }
        }
        
        private void UpdateModulePrototypesPlacement()
        {
            var path = pathCreator.Path;
                
            foreach (Transform child in transform)
            {
                if (child.gameObject.TryGetComponent(out ModulePrototypeData modulePrototypeData))
                {
                    var t = modulePrototypeData.step;
                    var pos = path.GetPointAtTime(t);
                    var rot = path.GetRotation(t);
                    child.SetPositionAndRotation(pos, rot);
                }
            }
        }

        private void Reset()
        {
            if (pathCreator == null)
            {
                pathCreator = GetComponent<Objects.PathCreator>();
        
                pathCreator.OnPathUpdated -= UpdateModulePrototypesPlacement;
                pathCreator.OnPathUpdated += UpdateModulePrototypesPlacement;
            }
            
            areRadiiAutoCalculated = true;
            startSpawnRadius = 0.1f;
            finalSpawnRadius = 0.0f;
        }
        
        public void ClearModulePrototypes()
        {
            while (transform.childCount > 0)
            {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }
        }
    }
}