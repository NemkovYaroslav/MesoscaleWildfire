using UnityEditor;
using UnityEngine;

namespace TreeModel.Editor.Helper
{
    public static class MouseUtility
    {
        public static Vector3 GetMouseWorldPosition(float depthFor3DSpace = 10)
        {
            var mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            var worldMouse = Physics.Raycast(mouseRay, out var hitInfo, depthFor3DSpace * 2f) ? hitInfo.point : mouseRay.GetPoint(depthFor3DSpace);
            return worldMouse;
        }

    }
}