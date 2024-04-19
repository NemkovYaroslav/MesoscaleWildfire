using Resources.PathCreator.Core.Runtime.Placer;
using UnityEditor;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Render
{
    [CustomEditor(typeof(PathSceneTool), true)]
    public sealed class PathSceneToolEditor : Editor
    {
        #region Fields

        private PathSceneTool _pathTool;
        private bool _isSubscribed;

        #endregion


        #region External Methods

        public override void OnInspectorGUI()
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                DrawDefaultInspector();

                if (check.changed)
                {
                    if (!_isSubscribed)
                    {
                        TryFindPathCreator();
                        Subscribe();
                    }
                }
            }

            if (_pathTool.gameObject.TryGetComponent(out ModuleGenerator moduleGenerator))
            {
                if (GUILayout.Button("Add Module"))
                {
                    if (TryFindPathCreator())
                    {
                        moduleGenerator.PlaceModuleOnBranch();
                    }
                }
            
                if (GUILayout.Button("Remove Modules"))
                {
                    if (TryFindPathCreator())
                    {
                        moduleGenerator.ClearModules(_pathTool.transform);
                    }
                }
                
                if (!moduleGenerator.gameObject.TryGetComponent(out ModulePlacer modulePlacer))
                {
                    if (GUILayout.Button("Generate Tree Prefab"))
                    {
                        if (TryFindPathCreator())
                        {
                            moduleGenerator.GenerateTreePrefab();
                        }
                    }
                }
            }
        }

        private void TriggerUpdate() 
        {
            if (_pathTool.pathCreator != null) 
            {
                _pathTool.TriggerUpdate();
            }
        }

        private void OnPathModified()
        {
            if (_pathTool.isPathAutoUpdated)
            {
                TriggerUpdate();
            }
        }

        private void OnEnable()
        {
            _pathTool = (PathSceneTool)target;
            _pathTool.OnDestroyed += OnToolDestroyed;

            if (TryFindPathCreator())
            {
                Subscribe();
                TriggerUpdate();
            }
        }

        private void OnToolDestroyed() 
        {
            if (_pathTool != null) 
            {
                _pathTool.pathCreator.OnPathUpdated -= OnPathModified;
            }
        }


        private void Subscribe()
        {
            if (_pathTool.pathCreator != null)
            {
                _isSubscribed = true;
                _pathTool.pathCreator.OnPathUpdated -= OnPathModified;
                _pathTool.pathCreator.OnPathUpdated += OnPathModified;
            }
        }

        private bool TryFindPathCreator()
        {
            // Try find a path creator in the scene, if one is not already assigned
            if (_pathTool.pathCreator == null)
            {
                if (_pathTool.GetComponent<Objects.PathCreator>() != null)
                {
                    _pathTool.pathCreator = _pathTool.GetComponent<Objects.PathCreator>();
                }
                else if (FindObjectOfType<Objects.PathCreator>())
                {
                    _pathTool.pathCreator = FindObjectOfType<Objects.PathCreator>();
                }
            }
            return _pathTool.pathCreator != null;
        }

        #endregion
    }
}