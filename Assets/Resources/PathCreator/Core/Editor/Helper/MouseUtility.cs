using UnityEditor;
using UnityEngine;

namespace Resources.PathCreator.Core.Editor.Helper
{
    public static class MouseUtility
    {
        /// <summary>
        /// Determines mouse position in world. If PathSpace is xy/xz, the position will be locked to that plane.
        /// If PathSpace is xyz, will attempt to raycast to a reasonably close object, or return the position
        /// at depthFor3DSpace distance from the current view
        /// </summary>
        public static Vector3 GetMouseWorldPosition(float depthFor3DSpace = 10)
        {
            var mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            var worldMouse = Physics.Raycast(mouseRay, out var hitInfo, depthFor3DSpace * 2f) ? 
                hitInfo.point : mouseRay.GetPoint(depthFor3DSpace);

            return worldMouse;
        }

    }
}