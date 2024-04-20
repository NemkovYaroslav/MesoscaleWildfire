using Unity.VisualScripting;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    public class TreeGenerator : MonoBehaviour
    {
        #region Fields

        private ModuleGenerator _moduleGenerator;

        private bool _isTreeCleaned;

        #endregion
        
        
        #region External Methods

        private void OnDrawGizmos()
        {
            if (_isTreeCleaned)
            {
                var transformsData = transform.GetComponentsInChildren<Transform>();

                foreach (var data in transformsData)
                {
                    if (data.childCount > 0)
                    {
                        for (var i = 0; i < data.childCount - 1; i++)
                        {
                            var currentPosition = data.GetChild(i).position;
                            var nextPosition = data.GetChild(i + 1).position;
                            
                            if (data.parent)
                            {
                                if (data.GetChild(i).GetSiblingIndex() == 0)
                                {
                                    var parentPosition = data.GetChild(i).parent.position;
                                    UnityEditor.Handles.DrawLine(parentPosition, currentPosition);
                                }
                            }
                            
                            UnityEditor.Handles.color = Color.white;
                            UnityEditor.Handles.DrawLine(currentPosition, nextPosition);
                        }
                    }
                }
            }
        }
        
        public void GenerateTreeStructure()
        {
            // clear ModulePlacer component and excess game objects
            var modulePlacers = transform.GetComponentsInChildren<ModulePlacer>();
            foreach (var modulePlacer in modulePlacers)
            {
                var modulePlacerGameObject = modulePlacer.gameObject;
                if (modulePlacer.t == 0 && modulePlacerGameObject.transform.parent != transform)
                {
                    DestroyImmediate(modulePlacerGameObject);
                }
                else
                {
                    DestroyImmediate(modulePlacer);
                }
            }

            // cleat PathCreator component
            var pathCreators = transform.GetComponentsInChildren<Objects.PathCreator>();
            foreach (var pathCreator in pathCreators)
            {
                DestroyImmediate(pathCreator);
            }

            // cleat ModuleGenerator component
            var moduleGenerators = transform.GetComponentsInChildren<ModuleGenerator>();
            foreach (var moduleGenerator in moduleGenerators)
            {
                DestroyImmediate(moduleGenerator);
            }
            
            // enable gizmo
            _isTreeCleaned = true;
            
            
        }

        #endregion
    }
}