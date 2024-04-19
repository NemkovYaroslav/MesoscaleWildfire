using Resources.PathCreator.Core.Runtime.Objects;
using UnityEditor;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    [CustomEditor(typeof(ModulePlacer), true)]
    public class ModulePlacerEditor : Editor
    {
        #region Fields

        private ModulePlacer _modulePlacer;

        #endregion
        
        
        #region External Methods

        private void OnEnable()
        {
            if (_modulePlacer == null)
            {
                _modulePlacer = (ModulePlacer)target;
            }
        }

        public override void OnInspectorGUI()
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                DrawDefaultInspector();
                
                if (check.changed)
                {
                    if (_modulePlacer.transform.parent != null)
                    {
                        var parent = _modulePlacer.transform.parent;
                        if (parent.TryGetComponent(out ModuleGenerator moduleGenerator))
                        {
                            var path = moduleGenerator.pathCreator.Path;
                            var t = _modulePlacer.t;
                            var pos = path.GetPointAtTime(t, EndOfPathInstruction.Stop);
                            var rot = path.GetRotation(t, EndOfPathInstruction.Stop);
                            _modulePlacer.transform.SetPositionAndRotation(pos, rot);
                            _modulePlacer.gameObject.name = "m_" + t;
                            moduleGenerator.SortModules();
                        }
                    }
                }
            }
        }
        
        #endregion
    }
}