using UnityEditor;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    [CustomEditor(typeof(ModulesGenerator), true)]
    public class ModulesGeneratorEditor : Editor
    {
        private ModulesGenerator _modulesGenerator;
        
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Generate Modules On Path"))
            {
                _modulesGenerator.GenerateModulesOnPath();
            }
        }
        
        private void Reset()
        {
            if (_modulesGenerator == null)
            {
                _modulesGenerator = (ModulesGenerator) target;
            }
        }
    }
}