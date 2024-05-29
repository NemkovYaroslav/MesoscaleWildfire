using UnityEditor;

namespace TreeModel.Runtime.Render
{
    [CustomEditor(typeof(PathSceneTool), true)]
    public sealed class PathSceneToolEditor : Editor
    {
        private PathSceneTool _pathTool;
        
        private bool _isSubscribed;
        
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
            TriggerUpdate();
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
            if (_pathTool.pathCreator == null)
            {
                if (_pathTool.GetComponent<Objects.PathCreator>() != null)
                {
                    _pathTool.pathCreator = _pathTool.GetComponent<Objects.PathCreator>();
                }
            }
            return _pathTool.pathCreator != null;
        }
    }
}