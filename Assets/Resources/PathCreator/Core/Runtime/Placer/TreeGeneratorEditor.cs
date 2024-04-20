using UnityEditor;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    [CustomEditor(typeof(TreeGenerator), true)]
    public class TreeGeneratorEditor : Editor
    {
        #region Fields

        private TreeGenerator _treeGenerator;

        #endregion
        
        
        #region External Methods
        
        private void OnEnable()
        {
            if (_treeGenerator == null)
            {
                _treeGenerator = (TreeGenerator)target;
            }
        }
        
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Generate Tree"))
            {
                _treeGenerator.GenerateTreeStructure();
            }
        }
        
        #endregion
    }
}