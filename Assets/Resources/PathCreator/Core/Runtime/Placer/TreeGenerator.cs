using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    public class TreeGenerator : MonoBehaviour
    {
        #region Fields

        private ModuleGenerator _moduleGenerator;

        private bool _isTreeCleaned = true;

        #endregion
        
        
        #region External Methods
        
        
        private void OnDrawGizmos()
        {
            if (_isTreeCleaned)
            {
                var joints = transform.GetComponentsInChildren<FixedJoint>();
                foreach (var joint in joints)
                {
                    UnityEditor.Handles.color = Color.white;
                    var currentPosition = joint.gameObject.transform.position;
                    var nextPosition = joint.connectedBody.gameObject.transform.position;
                    UnityEditor.Handles.DrawLine(currentPosition, nextPosition);
                    
                    var currentNormal = joint.gameObject.transform.rotation * Vector3.forward;
                    if (joint.gameObject.TryGetComponent(out ModuleData moduleData))
                    {
                        var currentRadius = moduleData.Radius;
                        UnityEditor.Handles.color = Color.red;
                        UnityEditor.Handles.DrawSolidDisc(currentPosition, currentNormal, currentRadius);
                        UnityEditor.Handles.color = Color.blue;
                        UnityEditor.Handles.DrawLine(currentPosition, currentPosition + currentNormal.normalized * 0.1f);
                    }
                }
            }
        }
        
        public void GenerateTreeStructure()
        {
            // clear ModulePlacer component and excess game objects
            var modulePlacers = transform.GetComponentsInChildren<ModulePlacer>();
            foreach (var modulePlacer in modulePlacers)
            {
                var modulePlacerGameObject = modulePlacer.gameObject;
                if (modulePlacer.t == 0 && modulePlacerGameObject.transform.parent != transform)
                {
                    DestroyImmediate(modulePlacerGameObject);
                }
                else
                {
                    DestroyImmediate(modulePlacer);
                }
            }

            // cleat PathCreator component
            var pathCreators = transform.GetComponentsInChildren<Objects.PathCreator>();
            foreach (var pathCreator in pathCreators)
            {
                DestroyImmediate(pathCreator);
            }

            // cleat ModuleGenerator component
            var moduleGenerators = transform.GetComponentsInChildren<ModuleGenerator>();
            foreach (var moduleGenerator in moduleGenerators)
            {
                DestroyImmediate(moduleGenerator);
            }

            // add fixed joints to modules
            var transformsData = transform.GetComponentsInChildren<Transform>();
            foreach (var data in transformsData)
            {
                if (data.childCount > 0)
                {
                    for (var i = 0; i < data.childCount; i++)
                    {
                        var currentGameObject = data.GetChild(i).gameObject;
                        if (currentGameObject.transform.GetSiblingIndex() == 0)
                        {
                            if (currentGameObject.transform.parent == transform)
                            {
                                var rootRigidbody = transform.gameObject.AddComponent<Rigidbody>();
                                rootRigidbody.mass = 5000.0f;
                                rootRigidbody.useGravity = false;
                                rootRigidbody.isKinematic = true;
                                rootRigidbody.constraints = RigidbodyConstraints.FreezeAll;

                                var treeBaseRigidbody = currentGameObject.AddComponent<Rigidbody>();
                                treeBaseRigidbody.mass = 2000.0f;
                                rootRigidbody.useGravity = false;
                                rootRigidbody.isKinematic = true;
                                treeBaseRigidbody.constraints = RigidbodyConstraints.FreezeAll;
                            }
                            
                            var currentFixedJoint = currentGameObject.AddComponent<FixedJoint>();
                            if (currentGameObject.transform.parent.TryGetComponent(out Rigidbody parentRigidbody))
                            {
                                currentFixedJoint.connectedBody = parentRigidbody;
                            }
                        }
                        else
                        {
                            var previousGameObject = data.GetChild(i - 1).gameObject;
                            var currentFixedJoint = currentGameObject.AddComponent<FixedJoint>();
                            if (previousGameObject.TryGetComponent(out Rigidbody previousRigidbody))
                            {
                                currentFixedJoint.connectedBody = previousRigidbody;
                            }
                        }
                    }
                }
            }
            
            // delete parent transforms and parent transforms to root
            foreach (var data in transformsData)
            {
                if (data.childCount > 0)
                {
                    data.DetachChildren();
                }
                data.parent = transform;
            }
            
            // fix module rotation
            var joints = transform.GetComponentsInChildren<FixedJoint>();
            foreach (var joint in joints)
            {
                var currentGameObject = joint.gameObject;
                var previousGameObject = joint.connectedBody.gameObject;
                var direction = (currentGameObject.transform.position - previousGameObject.transform.position).normalized;
                if (direction != Vector3.zero)
                {
                    currentGameObject.transform.rotation = Quaternion.LookRotation(direction);
                }
                else
                {
                    currentGameObject.transform.rotation = Quaternion.LookRotation(Vector3.up);
                }
            }
            
            // generate colliders
            foreach (var joint in joints)
            {
                var currentGameObject = joint.gameObject;
                var previousGameObject = joint.connectedBody.gameObject;

                var distance = Vector3.Distance(currentGameObject.transform.position, previousGameObject.transform.position);
                if (previousGameObject.TryGetComponent(out ModuleData data))
                { 
                    var currentCollider = currentGameObject.AddComponent<CapsuleCollider>();
                    if (currentGameObject.TryGetComponent(out ModuleData moduleData))
                    {
                        currentCollider.radius = moduleData.Radius;
                    }
                    currentCollider.height = distance;
                    currentCollider.direction = 2;
                    currentCollider.center = new Vector3(0.0f, 0.0f, - distance / 2.0f); 
                }
                
                // calculate mass
                if (currentGameObject.TryGetComponent(out Rigidbody currentRigidbody))
                {
                    if (currentGameObject.TryGetComponent(out ModuleData currentData))
                    {
                        if (previousGameObject.TryGetComponent(out ModuleData previousData))
                        {
                            var currentRadius = currentData.Radius;
                            var previousRadius = previousData.Radius;
                            var currentVolume 
                                = (Mathf.PI / 3.0f) 
                                  * distance 
                                  * (currentRadius * currentRadius + currentRadius * previousRadius + previousRadius * previousRadius);
                            
                            currentRigidbody.mass = 500.0f * currentVolume;
                        }
                    }
                }
            }
            
            // delete excess components
            var modulesData = transform.GetComponentsInChildren<ModuleData>();
            foreach (var moduleData in modulesData)
            {
                moduleData.gameObject.tag = "Module";
                DestroyImmediate(moduleData);
            }

            // delete tree generator component
            if (gameObject.TryGetComponent(out TreeGenerator treeGenerator))
            {
                treeGenerator.gameObject.tag = "Module";
                DestroyImmediate(treeGenerator);
            }
        }

        #endregion
    }
}