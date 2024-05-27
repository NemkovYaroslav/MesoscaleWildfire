using UnityEditor.Presets;
using UnityEngine;
using UnityEngine.VFX;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    public class ModulesGenerator : MonoBehaviour
    {
        private ModulePrototypesGenerator _modulePrototypesGenerator;

        public float woodDensity = 800.0f;

        public Preset visualEffectPreset;
        
        private void OnDrawGizmos()
        {
            var joints = transform.GetComponentsInChildren<FixedJoint>();
        
            // fix prototypes rotation
            foreach (var joint in joints)
            {
                var currentPrototype = joint.gameObject;
                var previousPrototype = joint.connectedBody.gameObject;

                var currentPrototypePosition = currentPrototype.transform.position;
                var previousPrototypePosition = previousPrototype.transform.position;
                
                var currentPrototypeNormal = currentPrototype.transform.forward;

                UnityEditor.Handles.color = Color.yellow;
                UnityEditor.Handles.DrawLine(currentPrototypePosition, previousPrototypePosition);

                if (currentPrototype.TryGetComponent(out CapsuleCollider prototypeCollider))
                {
                    UnityEditor.Handles.color = Color.red;
                    var radius = prototypeCollider.radius;
                    UnityEditor.Handles.DrawSolidDisc(currentPrototypePosition, currentPrototypeNormal, radius);
                    
                    if (currentPrototype.TryGetComponent(out Rigidbody prototypeRigidbody))
                    {
                        UnityEditor.Handles.color = Color.green;
                        var centerMass = prototypeRigidbody.worldCenterOfMass;
                        UnityEditor.Handles.DrawWireCube(centerMass, Vector3.one * 0.1f);
                    }
                }
                
                UnityEditor.Handles.color = Color.blue;
                UnityEditor.Handles.DrawLine(currentPrototypePosition, currentPrototypePosition + currentPrototypeNormal * 0.1f);
            }
        }
        
        public void GenerateModules()
        {
            // destroy ModulePrototypesGenerator component
            var modulePrototypesGeneratorComponents = transform.GetComponentsInChildren<ModulePrototypesGenerator>();
            foreach (var modulePrototypesGeneratorComponent in modulePrototypesGeneratorComponents)
            {
                DestroyImmediate(modulePrototypesGeneratorComponent);
            }
            
            // destroy PathCreator component
            var pathCreatorComponents = transform.GetComponentsInChildren<Objects.PathCreator>();
            foreach (var pathCreatorComponent in pathCreatorComponents)
            {
                DestroyImmediate(pathCreatorComponent);
            }
            
            var children = transform.GetComponentsInChildren<Transform>();
            
            // add colliders to prototypes
            foreach (var child in children)
            {
                if (child.childCount > 0)
                {
                    for (var i = child.childCount - 1; i > 0; i--)
                    {
                        var currentPrototype = child.GetChild(i).gameObject;
                        var previousPrototype = child.GetChild(i - 1).gameObject;
                        
                        var currentPrototypePosition = currentPrototype.transform.position;
                        var previousPrototypePosition = previousPrototype.transform.position;

                        var distance = Vector3.Distance(currentPrototypePosition, previousPrototypePosition);

                        var currentPrototypeData = currentPrototype.GetComponent<ModulePrototypeData>();
                        var previousPrototypeData = previousPrototype.GetComponent<ModulePrototypeData>();
                        var targetRadius = (currentPrototypeData.radius + previousPrototypeData.radius) / 2.0f;
                        
                        var currentPrototypeCollider = currentPrototype.AddComponent<CapsuleCollider>();
                        currentPrototypeCollider.radius = targetRadius;
                        currentPrototypeCollider.height = distance;
                        currentPrototypeCollider.direction = 2;
                        currentPrototypeCollider.center = new Vector3(0.0f, 0.0f, -distance / 2.0f);

                        currentPrototypeCollider.enabled = false;
                        
                        // add tag to prototype
                        currentPrototype.tag = "Module";
                    }
                }
            }

            // add rigidbodies to prototypes
            foreach (var child in children)
            {
                if (child.TryGetComponent(out ModulePrototypeData prototypeData))
                {
                    if (!Mathf.Approximately(prototypeData.step, 0.0f))
                    {
                        if (!child.gameObject.TryGetComponent(out Rigidbody _))
                        {
                            var prototype = child.gameObject;
                            
                            var prototypeRigidbody = prototype.AddComponent<Rigidbody>();

                            var prototypeCollider = prototype.GetComponent<CapsuleCollider>();

                            var targetMass = Mathf.PI * Mathf.Pow(prototypeCollider.radius, 2.0f) * prototypeCollider.height;
                            prototypeRigidbody.mass = targetMass * woodDensity;

                            prototypeRigidbody.automaticInertiaTensor = false;
                        }
                    }
                }
                else
                {
                    // add rigidbody to root
                    if (child == transform)
                    {
                        var rootPrototype = child.gameObject;
                        
                        if (!rootPrototype.TryGetComponent(out Rigidbody _))
                        {
                            var prototypeRigidbody = child.gameObject.AddComponent<Rigidbody>();
                            prototypeRigidbody.useGravity = false;
                            prototypeRigidbody.isKinematic = true;
                            prototypeRigidbody.constraints = RigidbodyConstraints.FreezeAll;
                        }

                        // add component to root
                        //rootPrototype.AddComponent<Tree>();
                        
                        // add tag to root
                        rootPrototype.tag = "Tree";
                    }
                }
            }
            
            // add fixed joints
            foreach (var child in children)
            {
                if (child.childCount > 0)
                {
                    for (var i = child.childCount - 1; i > 0; i--)
                    {
                        var currentPrototype = child.GetChild(i).gameObject;
                        var previousPrototype = child.GetChild(i - 1).gameObject;

                        var previousPrototypeData = previousPrototype.GetComponent<ModulePrototypeData>();

                        Rigidbody previousPrototypeRigidbody;
                        if (!Mathf.Approximately(previousPrototypeData.step, 0.0f))
                        {
                            previousPrototypeRigidbody = previousPrototype.GetComponent<Rigidbody>();
                        }
                        else
                        {
                            previousPrototypeRigidbody = previousPrototype.transform.parent.gameObject.GetComponent<Rigidbody>();
                        }

                        if (!currentPrototype.TryGetComponent(out FixedJoint _))
                        {
                            var currentPrototypeJoint = currentPrototype.AddComponent<FixedJoint>();
                            currentPrototypeJoint.connectedBody = previousPrototypeRigidbody;
                            currentPrototypeJoint.connectedMassScale = 0.75f;
                        }
                    }
                }
            }
            
            // add particle system
            foreach (var child in children)
            {
                if (child.childCount > 0)
                {
                    for (var i = child.childCount - 1; i > 0; i--)
                    {
                        var currentPrototype = child.GetChild(i).gameObject;
                        var visualEffect = currentPrototype.AddComponent<VisualEffect>();
                        visualEffectPreset.ApplyTo(visualEffect);

                        var capsuleCollider = currentPrototype.GetComponent<CapsuleCollider>();
                        
                        visualEffect.SetFloat("cone radius", capsuleCollider.radius);
                        visualEffect.SetFloat("cone height", capsuleCollider.height);
                    }
                }
            }
            
            // delete parent transforms and parent transforms to root
            foreach (var child in children)
            {
                if (child.childCount > 0)
                {
                    child.DetachChildren();
                }
                child.parent = transform;
            }

            var joints = transform.GetComponentsInChildren<FixedJoint>();
            
            // fix prototypes rotation
            foreach (var joint in joints)
            {
                var currentPrototype = joint.gameObject;
                var previousPrototype = joint.connectedBody.gameObject;
                
                var direction = (currentPrototype.transform.position - previousPrototype.transform.position).normalized;
                
                currentPrototype.transform.rotation = Quaternion.LookRotation(direction);
            }
            
            var modulePrototypesData = transform.GetComponentsInChildren<ModulePrototypeData>();
            foreach (var modulePrototypeData in modulePrototypesData)
            {
                if (Mathf.Approximately(modulePrototypeData.step, 0.0f))
                {
                    // destroy zero position modules
                    DestroyImmediate(modulePrototypeData.gameObject);
                }
                else
                {
                    // add new module component
                    modulePrototypeData.gameObject.AddComponent<Module>();
                    
                    // destroy excess component
                    DestroyImmediate(modulePrototypeData);
                }
            }
        }
    }
}