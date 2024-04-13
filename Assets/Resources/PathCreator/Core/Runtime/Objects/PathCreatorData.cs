using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace Resources.PathCreator.Core.Runtime.Objects 
{
    /// Stores state data for the path creator editor
    [System.Serializable]
    public class PathCreatorData 
    {
        #region Events
        
        public event System.Action OnBezierOrVertexPathModified;
        public event System.Action OnBezierCreated;

        #endregion
        
        
        #region Fields
        
        [SerializeField] private BezierPath bezierPath;

        private VertexPath _vertexPath;

        [SerializeField] private bool isVertexPathUpToDated;

        // vertex path settings
        public float vertexPathMaxAngleError = 0.3f;
        public float vertexPathMinVertexSpacing = 0.01f;

        // bezier display settings
        public bool isTransformToolShown = true;
        public bool arePathBoundsShown;
        public bool arePerSegmentBoundsShown;
        public bool areAnchorPointsDisplayed = true;
        public bool areControlPointsDisplayed = true;
        public float bezierHandleScale = 1.0f;
        public bool areGlobalDisplaySettingsFoldedOut;
        public bool isConstantHandleSizeKept;

        // vertex display settings
        public bool areNormalsShownInVertexMode;
        public bool isBezierPathShownInVertexMode;

        // Editor display states
        public bool areDisplayOptionsShown;
        public bool arePathOptionsShown = true;
        public bool areVertexPathDisplayOptionsShown;
        public bool areVertexPathOptionsShown = true;
        public bool areNormalsShown;
        public bool areNormalsHelpInfoShown;
        public int tabIndex;

        #endregion
        
        
        public void Initialize(bool defaultIs2D) 
        {
            if (bezierPath == null)
            {
                CreateBezier(Vector3.zero, defaultIs2D);
            }
            
            isVertexPathUpToDated = false;
            
            Debug.Assert(bezierPath != null, nameof(bezierPath) + " != null");
            
            bezierPath.OnModified -= BezierPathEdited;
            bezierPath.OnModified += BezierPathEdited;
        }

        public void ResetBezierPath(Vector3 centre, bool defaultIs2D = false) 
        {
            CreateBezier(centre, defaultIs2D);
        }

        private void CreateBezier(Vector3 centre, bool defaultIs2D = false) 
        {
            if (bezierPath != null) 
            {
                bezierPath.OnModified -= BezierPathEdited;
            }

            var space = (defaultIs2D) ? PathSpace.XY : PathSpace.XYZ;
            bezierPath = new BezierPath (centre, false, space);

            bezierPath.OnModified += BezierPathEdited;
            
            isVertexPathUpToDated = false;

            if (OnBezierOrVertexPathModified != null) 
            {
                OnBezierOrVertexPathModified ();
            }
            
            if (OnBezierCreated != null) 
            {
                OnBezierCreated ();
            }
        }

        public BezierPath BezierPath 
        {
            get => bezierPath;
            
            set 
            {
                bezierPath.OnModified -= BezierPathEdited;
                
                isVertexPathUpToDated = false;
                
                bezierPath = value;
                
                bezierPath.OnModified += BezierPathEdited;

                if (OnBezierOrVertexPathModified != null) 
                {
                    OnBezierOrVertexPathModified ();
                }
                
                if (OnBezierCreated != null) 
                {
                    OnBezierCreated ();
                }

            }
        }

        // Get the current vertex path
        public VertexPath GetVertexPath(Transform transform)
        {
            // create new vertex path if path was modified since this vertex path was created
            if (isVertexPathUpToDated && _vertexPath != null) return _vertexPath;
            
            isVertexPathUpToDated = true;
            
            _vertexPath = new VertexPath (BezierPath, transform, vertexPathMaxAngleError, vertexPathMinVertexSpacing);
            
            return _vertexPath;
        }

        public void PathTransformed()
        {
            if (OnBezierOrVertexPathModified != null) 
            {
                OnBezierOrVertexPathModified ();
            }
        }

        public void VertexPathSettingsChanged()
        {
            isVertexPathUpToDated = false;
            
            if (OnBezierOrVertexPathModified != null) 
            {
                OnBezierOrVertexPathModified();
            }
        }

        public void PathModifiedByUndo()
        {
            isVertexPathUpToDated = false;
            
            if (OnBezierOrVertexPathModified != null) 
            {
                OnBezierOrVertexPathModified();
            }
        }

        private void BezierPathEdited()
        {
            isVertexPathUpToDated = false;
            
            if (OnBezierOrVertexPathModified != null) 
            {
                OnBezierOrVertexPathModified();
            }
        }
    }
}