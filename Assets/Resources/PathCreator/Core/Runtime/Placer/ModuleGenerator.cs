using System.Linq;
using Resources.PathCreator.Core.Runtime.Objects;
using Resources.PathCreator.Core.Runtime.Render;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    [RequireComponent(typeof(Objects.PathCreator))]
    public class ModuleGenerator : PathSceneTool 
    {
        #region External Methods

        private void OnDrawGizmos()
        {
            foreach (Transform child in transform)
            {
                UnityEditor.Handles.color = Color.red;
                if (child.gameObject.TryGetComponent(out ModulePlacer placer))
                {
                    var center = child.position;
                    var normal = child.rotation * Vector3.forward;
                    if (child.gameObject.TryGetComponent(out ModuleData data))
                    {
                        var radius = data.Radius;
                        UnityEditor.Handles.DrawSolidDisc(center, normal, radius);
                        UnityEditor.Handles.color = Color.blue;
                        UnityEditor.Handles.DrawLine(center, center + normal.normalized * 0.2f);
                    }
                }
            }
        }

        public void SortModules()
        {
            if (transform.childCount > 2)
            {
                var placers = transform.GetComponentsInChildren<ModulePlacer>();
                var orderedPlacers = placers.OrderBy(property => property.t).ToArray();
                foreach (var placer in orderedPlacers)
                {
                    placer.transform.SetAsLastSibling();
                }
            }
        }
        
        private void InstantiateModule(float t)
        {
            var path = pathCreator.Path;
            var pos = path.GetPointAtTime(t, EndOfPathInstruction.Stop);
            var rot = path.GetRotation(t, EndOfPathInstruction.Stop);
            var obj = new GameObject("Module_" + t, typeof(ModulePlacer), typeof(ModuleData));
            obj.transform.SetPositionAndRotation(pos, rot);
            obj.transform.SetParent(transform);
            obj.GetComponent<ModulePlacer>().t = t;
        }

        public void PlaceModuleOnBranch()
        {
            if (pathCreator != null)
            {
                if (transform.childCount < 1)
                {
                    InstantiateModule(0.0f);
                }
                else
                {
                    if (transform.childCount < 2)
                    {
                        InstantiateModule(1.0f);
                    }
                    else
                    {
                        var maxDifferenceT = float.MinValue;
                        var averageT = 0.0f;

                        for (var i = 0; i < transform.childCount - 1; i++)
                        {
                            var first = transform.GetChild(i);
                            var second = transform.GetChild(i + 1);

                            if (first.TryGetComponent(out ModulePlacer firstPlacer) && second.TryGetComponent(out ModulePlacer secondPlacer))
                            {
                                var firstT = firstPlacer.t;
                                var secondT = secondPlacer.t;

                                var differenceT = secondT - firstT;
                                if (differenceT > maxDifferenceT)
                                {
                                    maxDifferenceT = differenceT;
                                    averageT = (firstT + secondT) / 2.0f;
                                }
                            }
                        }

                        InstantiateModule(averageT);
                        
                        SortModules();
                    }
                }
            }
        }

        public void ClearModules(Transform parent)
        {
            if (parent.TryGetComponent(out ModuleGenerator moduleGenerator))
            {
                while (parent.childCount > 0)
                {
                    DestroyImmediate(parent.GetChild(0).gameObject);
                }
            }
        }
        
        protected override void PathUpdated()
        {
            if (pathCreator != null)
            {
                var path = pathCreator.Path;
                
                foreach (Transform child in transform)
                {
                    if (child.gameObject.TryGetComponent(out ModulePlacer placer))
                    {
                        var t = placer.t;
                        var pos = path.GetPointAtTime(t, EndOfPathInstruction.Stop);
                        var rot = path.GetRotation(t, EndOfPathInstruction.Stop);
                        child.SetPositionAndRotation(pos, rot);
                    }
                }
            }
        }

        #endregion
    }
}
