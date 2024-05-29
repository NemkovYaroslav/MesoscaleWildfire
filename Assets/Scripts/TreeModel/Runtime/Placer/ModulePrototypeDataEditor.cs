using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace TreeModel.Runtime.Placer
{
    [CustomEditor(typeof(ModulePrototypeData), true)]
    public class ModulePrototypeDataEditor : Editor
    {
        private ModulePrototypeData _modulePrototypePlaceData;
        
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                if (check.changed)
                {
                    if (_modulePrototypePlaceData.transform.parent != null)
                    {
                        var parent = _modulePrototypePlaceData.transform.parent;
                        if (parent.TryGetComponent(out ModulePrototypesGenerator modulePrototypesGenerator))
                        {
                            var path = modulePrototypesGenerator.pathCreator.Path;
                            
                            var t = _modulePrototypePlaceData.step;
                            var pos = path.GetPointAtTime(t);
                            var rot = path.GetRotation(t);
                            _modulePrototypePlaceData.transform.SetPositionAndRotation(pos, rot);
                            _modulePrototypePlaceData.gameObject.name = t.ToString(CultureInfo.CurrentCulture);

                            if (modulePrototypesGenerator.areRadiiAutoCalculated)
                            {
                                _modulePrototypePlaceData.radius 
                                    = Mathf.Lerp(modulePrototypesGenerator.startSpawnRadius, modulePrototypesGenerator.finalSpawnRadius, t);
                            }
                            
                            modulePrototypesGenerator.SortModules();
                        }
                    }
                }
            }
        }
        
        private void Reset()
        {
            if (_modulePrototypePlaceData == null)
            {
                _modulePrototypePlaceData = (ModulePrototypeData) target;
            }
        }
    }
}