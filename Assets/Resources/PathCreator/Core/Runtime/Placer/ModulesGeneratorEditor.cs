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
            var newWoodDensity = EditorGUILayout.FloatField("Wood Density", _modulesGenerator.woodDensity);
            if (!Mathf.Approximately(_modulesGenerator.woodDensity, newWoodDensity))
            {
                _modulesGenerator.woodDensity = newWoodDensity;
            }
            
            EditorGUILayout.Separator();
            
            if (GUILayout.Button("Generate Modules"))
            {
                _modulesGenerator.GenerateModules();
            }
        }
        
        private void Awake()
        {
            if (_modulesGenerator == null)
            {
                _modulesGenerator = (ModulesGenerator) target;
            }
        }
    }
}