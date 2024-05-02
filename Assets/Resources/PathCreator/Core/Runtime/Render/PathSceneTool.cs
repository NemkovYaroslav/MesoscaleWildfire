using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Render
{
    public abstract class PathSceneTool : MonoBehaviour
    {
        public event System.Action OnDestroyed;

        [HideInInspector] public Objects.PathCreator pathCreator;
        
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
    }
}