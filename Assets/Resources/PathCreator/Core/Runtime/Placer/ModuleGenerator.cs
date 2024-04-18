using System.Collections.Generic;
using Resources.PathCreator.Core.Runtime.Objects;
using Resources.PathCreator.Core.Runtime.Render;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    [RequireComponent(typeof(Objects.PathCreator))]
    public class ModuleGenerator : PathSceneTool 
    {
        #region Fields
        
        public GameObject branch;
        
        public GameObject module;
        
        public readonly LinkedList<ModulePlacer> modules;

        #endregion
        

        #region External Methods
        
        private ModuleGenerator()
        {
            modules = new LinkedList<ModulePlacer>();
        }

        private GameObject InstantiateModule(float t)
        {
            var path = pathCreator.Path;
            
            var pos = path.GetPointAtTime(t, EndOfPathInstruction.Stop);
            var rot = path.GetRotation(t, EndOfPathInstruction.Stop);
            var obj = Instantiate(module, pos, rot, branch.transform);
            
            obj.tag = "Module";

            var placer = obj.AddComponent<ModulePlacer>();
            placer.t = t;
            
            return obj;
        }

        public void AddModuleOnBranch()
        {
            if (pathCreator != null && module != null && branch != null)
            {
                if (modules.Count == 0)
                {
                    var obj = InstantiateModule(0.0f);
                    if (obj.TryGetComponent(out ModulePlacer placer))
                    {
                        modules.AddFirst(placer);
                    }
                }
                else
                {
                    if (modules.Count == 1)
                    {
                        var obj = InstantiateModule(1.0f);
                        if (obj.TryGetComponent(out ModulePlacer placer))
                        {
                            modules.AddLast(placer);
                        }
                    }
                    else
                    {
                        if (modules.Count > 1)
                        {
                            var maxDifference = float.MinValue;
                            var average = 0.0f;
                            var previous = modules.First;
                        
                            var node = modules.First;
                            while (node != null && node.Next != null)
                            {
                                var current = node.Value.t;
                                var next = node.Next.Value.t;
                                var difference = next - current;
                                if (difference > maxDifference)
                                {
                                    maxDifference = difference;
                                    average = (current + next) / 2.0f;
                                    previous = node;
                                }
                                node = node.Next;
                            }
                        
                            var obj = InstantiateModule(average);
                            if (obj.TryGetComponent(out ModulePlacer placer))
                            {
                                modules.AddAfter(previous, placer);
                            }
                        } 
                    }
                }
            }
        }

        public void ClearModules()
        {
            while (transform.childCount > 0)
            {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }
            modules.Clear();
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
