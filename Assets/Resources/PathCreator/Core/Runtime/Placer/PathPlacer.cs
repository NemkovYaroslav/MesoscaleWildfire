using Resources.PathCreator.Core.Runtime.Render;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer {

    [ExecuteInEditMode]
    public class PathPlacer : PathSceneTool {

        public GameObject prefab;
        public GameObject holder;
        public float spacing = 3;

        private const float MinSpacing = .1f;

        private void Generate () 
        {
            if (pathCreator != null && prefab != null && holder != null) 
            {
                DestroyObjects ();

                var path = pathCreator.Path;

                spacing = Mathf.Max(MinSpacing, spacing);
                float dst = 0;

                while (dst < path.length) 
                {
                    var point = path.GetPointAtDistance(dst);
                    var rot = path.GetRotationAtDistance(dst);
                    Instantiate(prefab, point, rot, holder.transform);
                    dst += spacing;
                }
            }
        }

        private void DestroyObjects () 
        {
            var numChildren = holder.transform.childCount;
            for (var i = numChildren - 1; i >= 0; i--) 
            {
                DestroyImmediate (holder.transform.GetChild (i).gameObject, false);
            }
        }

        protected override void PathUpdated () 
        {
            if (pathCreator != null) 
            {
                Generate ();
            }
        }
    }
}