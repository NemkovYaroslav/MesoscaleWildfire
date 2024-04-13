using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Objects 
{
    /// This class stores data for the path editor, and provides accessors to get the current vertex and bezier path.
    /// Attach to a GameObject to create a new path editor.
    
    public class PathCreator : MonoBehaviour 
    {
        #region Fields
        
        public event System.Action OnPathUpdated;

        [SerializeField, HideInInspector] private PathCreatorData editorData;
        [SerializeField, HideInInspector] private bool isInitialized;

        private GlobalDisplaySettings _globalEditorDisplaySettings;
        
        #endregion
        
        
        #region External methods

        // Vertex path created from the current bezier path
        public VertexPath Path 
        {
            get
            {
                if (!isInitialized)
                {
                    InitializeEditorData(false);
                }
                
                return editorData.GetVertexPath(transform);
            }
        }

        // The bezier path created in the editor
        public BezierPath BezierPath 
        {
            get
            {
                if (!isInitialized)
                {
                    InitializeEditorData(false);
                }
                
                return editorData.BezierPath;
            }
            
            set 
            {
                if (!isInitialized)
                {
                    InitializeEditorData(false);
                }
                
                editorData.BezierPath = value;
            }
        }
        
        #endregion
        

        #region Internal methods

        /// Used by the path editor to initialise some data
        public void InitializeEditorData (bool in2DMode) 
        {
            if (editorData == null)
            {
                editorData = new PathCreatorData();
            }
            
            editorData.OnBezierOrVertexPathModified -= TriggerPathUpdate;
            editorData.OnBezierOrVertexPathModified += TriggerPathUpdate;

            editorData.Initialize(in2DMode);
            isInitialized = true;
        }

        public PathCreatorData EditorData => editorData;

        private void TriggerPathUpdate() 
        {
            if (OnPathUpdated != null) 
            {
                OnPathUpdated();
            }
        }

#if UNITY_EDITOR

        // Draw the path when path objected is not selected (if enabled in settings)
        private void OnDrawGizmos () 
        {
            // Only draw path gizmo if the path object is not selected
            // (editor script is responsible for drawing when selected)
            var selectedObj = UnityEditor.Selection.activeGameObject;
            
            if (selectedObj == gameObject) return;

            if (Path == null) return;
            
            Path.UpdateTransform (transform);

            if (_globalEditorDisplaySettings == null) 
            {
                _globalEditorDisplaySettings = GlobalDisplaySettings.Load();
            }

            if (!_globalEditorDisplaySettings.isVisibleWhenNotSelected) return;
            
            Gizmos.color = _globalEditorDisplaySettings.bezierPath;

            for (var i = 0; i < Path.NumPoints; i++) 
            {
                var nextI = i + 1;
                if (nextI >= Path.NumPoints) 
                {
                    if (Path.isClosedLoop) 
                    {
                        nextI %= Path.NumPoints;
                    } 
                    else 
                    {
                        break;
                    }
                }
                Gizmos.DrawLine (Path.GetPoint (i), Path.GetPoint (nextI));
            }
        }
#endif
        #endregion
    }
}