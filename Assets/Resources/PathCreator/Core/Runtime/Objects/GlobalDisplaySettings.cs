using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Objects
{
    [CreateAssetMenu(fileName = "PathSettings", menuName = "Custom/PathSettings")]
    public class GlobalDisplaySettings : ScriptableObject
    {
        #region Enums
        
        public enum HandleType
        { 
            Sphere,
            Circle,
            Square
        };
        
        #endregion
        

        #region Fields
        
        [Header("Appearance")]
        public float anchorSize = 10.0f;
        public float controlSize = 7.0f;
        
        [Tooltip("Should the path still be drawn when behind objects in the scene?")]
        public bool isVisibleBehindObjects = true;
        [Tooltip("Should the path be drawn even when the path object is not selected?")]
        public bool isVisibleWhenNotSelected = true;
        [Tooltip("If true, control points will be hidden when the control point mode is set to automatic. Otherwise they will inactive, but still visible.")]
        public bool isAutoControlsHided = true;
        public HandleType anchorShape;
        public HandleType controlShape;

        [Header("Anchor Colours")]
        public Color anchor = new Color(0.95f, 0.25f, 0.25f, 0.85f);
        public Color anchorHighlighted = new Color(1.0f, 0.57f, 0.4f);
        public Color anchorSelected = Color.white;

        [Header("Control Colours")]
        public Color control = new Color(0.35f, 0.6f, 1.0f, 0.85f);
        public Color controlHighlighted = new Color(0.8f, 0.67f, 0.97f);
        public Color controlSelected = Color.white;
        public Color handleDisabled = new Color(1.0f, 1.0f, 1.0f, 0.2f);
        public Color controlLine = new Color(0.0f, 0.0f, 0.0f, 0.35f);

        [Header("Bezier Path Colours")]
        public Color bezierPath = Color.green;
        public Color highlightedPath = new Color(1.0f, 0.6f, 0.0f);
        public Color bounds = new Color(1.0f, 1.0f, 1.0f, 0.4f);
        public Color segmentBounds = new Color(1.0f, 1.0f, 1.0f, 0.4f);

        [Header("Vertex Path Colours")]
        public Color vertexPath = Color.white;

        [Header("Normals")]
        public Color normals = Color.yellow;
        [Range(0,1)] public float normalsLength = 0.1f;
        
        #endregion
        

#if UNITY_EDITOR
        
        #region External Methods
        
        public static GlobalDisplaySettings Load() 
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("PathSettings");
            if (guids.Length == 0)
            {
                Debug.LogWarning("Could not find settings asset. Will use default settings instead.");
                return ScriptableObject.CreateInstance<GlobalDisplaySettings>();
            }
            else
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GlobalDisplaySettings>(path);
            }
        }
        
        #endregion
        
#endif
    }
}