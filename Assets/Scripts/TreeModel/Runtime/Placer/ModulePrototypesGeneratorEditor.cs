using UnityEditor;
using UnityEngine;

namespace TreeModel.Runtime.Placer
{
    [CustomEditor(typeof(ModulePrototypesGenerator), true)]
    public class ModulePrototypesGeneratorEditor : Editor
    {
        private ModulePrototypesGenerator _modulePrototypesGenerator;
        
        public override void OnInspectorGUI()
        {
            _modulePrototypesGenerator.isPreviewModeEnabled = 
                EditorGUILayout.Toggle(new GUIContent("Enable Preview Mode"), _modulePrototypesGenerator.isPreviewModeEnabled);
            if (_modulePrototypesGenerator.isPreviewModeEnabled)
            {
                _modulePrototypesGenerator.previewMesh = 
                    (Mesh)EditorGUILayout.ObjectField("Preview Mesh", _modulePrototypesGenerator.previewMesh, typeof(Mesh), false);
            }
            
            EditorGUILayout.Separator();
            
            _modulePrototypesGenerator.areRadiiAutoCalculated =
                EditorGUILayout.Toggle(new GUIContent("Auto Calculate Radii"), _modulePrototypesGenerator.areRadiiAutoCalculated);
            
            if (_modulePrototypesGenerator.areRadiiAutoCalculated)
            {
                var newStartRadius = EditorGUILayout.FloatField("Start Spawn Radius", _modulePrototypesGenerator.startSpawnRadius);
                if (!Mathf.Approximately(_modulePrototypesGenerator.startSpawnRadius, newStartRadius))
                {
                    _modulePrototypesGenerator.startSpawnRadius = newStartRadius;
                }
                
                var newFinalRadius = EditorGUILayout.FloatField("Final Spawn Radius", _modulePrototypesGenerator.finalSpawnRadius);
                if (!Mathf.Approximately(_modulePrototypesGenerator.finalSpawnRadius, newFinalRadius))
                {
                    _modulePrototypesGenerator.finalSpawnRadius = newFinalRadius;
                }
            }
            else
            {
                var newConstRadius = EditorGUILayout.FloatField("Const Spawn Radius", _modulePrototypesGenerator.constSpawnRadius);
                if (!Mathf.Approximately(_modulePrototypesGenerator.constSpawnRadius, newConstRadius))
                {
                    _modulePrototypesGenerator.constSpawnRadius = newConstRadius;
                }
            }
            
            if (GUILayout.Button("Recalculate Prototypes Radii"))
            {
                _modulePrototypesGenerator.UpdateModulePrototypeRadiiData();
            }
            
            EditorGUILayout.Separator();
            
            if (GUILayout.Button("Add Module Prototype"))
            {
                _modulePrototypesGenerator.AddModulePrototypeToPath();
            }
            
            if (GUILayout.Button("Clear Module Prototypes"))
            {
                _modulePrototypesGenerator.ClearModulePrototypes();
            }
        }

        private void Reset()
        {
            if (_modulePrototypesGenerator == null)
            {
                _modulePrototypesGenerator = (ModulePrototypesGenerator) target;
            }
        }
    }
}