using Resources.PathCreator.Core.Runtime.Objects;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Render
{
    public abstract class PathSceneTool : MonoBehaviour
    {
        #region Events

        public event System.Action OnDestroyed;

        #endregion


        #region Fields

        [HideInInspector] public Objects.PathCreator pathCreator;
        
        [HideInInspector] public bool isPathAutoUpdated = true;

        #endregion
        
        
        #region External Methods
        
        protected VertexPath Path => pathCreator.Path;
        
        protected abstract void PathUpdated();
        
        public void TriggerUpdate()
        {
            PathUpdated();
        }
        
        protected virtual void OnDestroy() 
        {
            if (OnDestroyed != null) 
            {
                OnDestroyed();
            }
        }

        #endregion
    }
}
