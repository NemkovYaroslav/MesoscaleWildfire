
using System.Collections.Generic;
using System.IO;
using Resources.PathCreator.Core.Runtime.Objects;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Render
{
    [RequireComponent(typeof(Objects.PathCreator))]
    public class CylinderMeshCreator : PathSceneTool
    {
        #region Fields
        
        public float thickness = 0.15f;
        [Range (3, 30)] public int resolutionU = 10;
        [Min (0)] public float resolutionV = 20.0f;
        
        public Material material;
        
        [SerializeField, HideInInspector] private GameObject meshHolder;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        
        #endregion
        
        
        #region External Methods
        
        protected override void PathUpdated() 
        {
            if (pathCreator != null) 
            {
                AssignMeshComponents();
                AssignMaterials();
                CreateMesh();
            }
        }

        private void CreateMesh () 
        {
            var verts = new List<Vector3> ();
            var triangles = new List<int> ();

            var numCircles = Mathf.Max(2, Mathf.RoundToInt(Path.length * resolutionV) + 1);
            const EndOfPathInstruction pathInstruction = EndOfPathInstruction.Stop;

            for (var s = 0; s < numCircles; s++) 
            {
                var segmentPercent = s / (numCircles - 1f);
                var centerPos = Path.GetPointAtTime(segmentPercent, pathInstruction);
                var norm = Path.GetNormal(segmentPercent, pathInstruction);
                var forward = Path.GetDirection(segmentPercent, pathInstruction);
                var tangentOrWhatEver = Vector3.Cross(norm, forward);

                for (var currentRes = 0; currentRes < resolutionU; currentRes++) 
                {
                    var angle = ((float) currentRes / resolutionU) * (Mathf.PI * 2.0f);

                    var xVal = Mathf.Sin (angle) * thickness;
                    var yVal = Mathf.Cos (angle) * thickness;

                    var point = (norm * xVal) + (tangentOrWhatEver * yVal) + centerPos;
                    verts.Add (point);

                    //! Adding the triangles
                    if (s < numCircles - 1) 
                    {
                        var startIndex = resolutionU * s;
                        triangles.Add (startIndex + currentRes);
                        triangles.Add (startIndex + (currentRes + 1) % resolutionU);
                        triangles.Add (startIndex + currentRes + resolutionU);

                        triangles.Add (startIndex + (currentRes + 1) % resolutionU);
                        triangles.Add (startIndex + (currentRes + 1) % resolutionU + resolutionU);
                        triangles.Add (startIndex + currentRes + resolutionU);
                    }

                }
            }

            if (_mesh == null) 
            {
                _mesh = new Mesh ();
            } 
            else 
            {
                _mesh.Clear ();
            }

            _mesh.SetVertices(verts);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateNormals();
        }
        
        // Add MeshRenderer and MeshFilter components to this game object if not already attached
        private void AssignMeshComponents() 
        {

            if (meshHolder == null) 
            {
                meshHolder = new GameObject("Mesh Holder");
            }

            meshHolder.transform.rotation = Quaternion.identity;
            meshHolder.transform.position = Vector3.zero;
            meshHolder.transform.localScale = Vector3.one;

            // Ensure mesh renderer and filter components are assigned
            if (!meshHolder.gameObject.GetComponent<MeshFilter>()) 
            {
                meshHolder.gameObject.AddComponent<MeshFilter>();
            }
            if (!meshHolder.GetComponent<MeshRenderer>()) 
            {
                meshHolder.gameObject.AddComponent<MeshRenderer>();
            }

            _meshRenderer = meshHolder.GetComponent<MeshRenderer>();
            _meshFilter = meshHolder.GetComponent<MeshFilter>();
            if (_mesh == null) 
            {
                _mesh = new Mesh();
            }
            _meshFilter.sharedMesh = _mesh;
        }

        private void AssignMaterials () 
        {
            if (material != null) 
            {
                _meshRenderer.sharedMaterial = material;
            }
        }
        
        #endregion
    }
}
