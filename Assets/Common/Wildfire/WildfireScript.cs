using System;
using System.Collections.Generic;
using Common.Renderer;
using Resources.PathCreator.Core.Runtime.Placer;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Jobs;
using UnityEngine.Rendering;
using Tree = Resources.PathCreator.Core.Runtime.Placer.Tree;

namespace Common.Wildfire
{
    public class WildfireScript : MonoBehaviour
    {
        [Header("Common Settings")]
        public ComputeShader computeShader;
        [SerializeField] private Vector3 textureResolution;
        [SerializeField] private Material renderMaterial;
        
        [Header("Torch Settings")]
        [SerializeField] private GameObject torch;
        
        [Header("Solver Settings")]
        [SerializeField] private float torchIntensity;
        [SerializeField] private float diffusionIntensity;
        [SerializeField] private float viscosityIntensity;
        [SerializeField] private int solverIterations;

        [SerializeField] private Wind.Wind[] winds;
        
        // render textures
        private RenderTexture _colorTemperatureTexture;
        
        private ComputeBuffer _posForSetData;
        private int _posForSetDataStride;

        private ComputeBuffer _posForGetData;
        private int _posForGetDataStride;

        private Texture3D _textureForSampleTemperature;

        private Module[] _modules;
        private List<Module> _orderedModuleList;
        private Dictionary<Rigidbody, Module> _orderedRigidbodyDictionary;
        
        private int _kernelInit;
        private int _kernelUserInput;
        private int _kernelDiffusionTemperature;
        private int _kernelModulesInfluence;
        private int _kernelAddFire;
        private int _kernelReadData;
        
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        
        private static readonly int ShaderTextureResolution  = Shader.PropertyToID("texture_resolution");
        private static readonly int ShaderAreaScale  = Shader.PropertyToID("area_scale");
        
        private static readonly int ShaderTorchIntensity     = Shader.PropertyToID("torch_intensity");
        private static readonly int ShaderDiffusionIntensity = Shader.PropertyToID("diffusion_intensity");
        private static readonly int ShaderViscosityIntensity = Shader.PropertyToID("viscosity_intensity");
        private static readonly int ShaderDeltaTime          = Shader.PropertyToID("delta_time");
        private static readonly int ShaderTorchPosition      = Shader.PropertyToID("torch_position");
        private static readonly int ModulesNumber            = Shader.PropertyToID("modules_number");
        
        private static readonly int ShaderColorTemperatureTexture = Shader.PropertyToID("color_temperature_texture");
        
        private static readonly int PosForGetDataBuffer = Shader.PropertyToID("pos_for_get_data");
        private static readonly int TextureForSampleTemperature = Shader.PropertyToID("texture_for_sample_temperature");
        
        private static readonly int PosForSetDataBuffer = Shader.PropertyToID("pos_for_set_data");
        
        // transfer data from gpu to cpu
        private NativeArray<Vector4> _transferArray;

        private NativeArray<float> _ambientTemperatureArray;
        
        private ModuleRenderer _moduleRenderer;
        
        
        // ANOTHER SHIT
        
        private int _kernelDiffusionVelocity;
        private RenderTexture _velocityTexture;
        private static readonly int ShaderVelocityTexture = Shader.PropertyToID("velocity_texture");

        private int _kernelDivergence;
        private RenderTexture _divergenceTexture;
        private RenderTexture _pressureTexture;
        private static readonly int ShaderDivergenceTexture = Shader.PropertyToID("divergence_texture");
        private static readonly int ShaderPressureTexture = Shader.PropertyToID("pressure_texture");

        private int _kernelPressure;
        private int _kernelSubtractGradient;
        private int _kernelAdvectionVelocity;
        private int _kernelAdvectionTemperature;
        
        //private static readonly int ShaderWindDirection = Shader.PropertyToID("wind_direction");
        //private static readonly int ShaderWindStrength = Shader.PropertyToID("wind_strength");
        
        private static readonly int ShaderFireDirection = Shader.PropertyToID("fire_direction");
        private static readonly int ShaderFireStrength = Shader.PropertyToID("fire_strength");

        private RenderTexture CreateRenderTexture3D(GraphicsFormat format)
        {
            var dataTexture 
                = new RenderTexture((int)textureResolution.x, (int)textureResolution.y, format, 0)
                {
                    volumeDepth       = (int)textureResolution.z,
                    dimension         = TextureDimension.Tex3D,
                    filterMode        = FilterMode.Point,
                    wrapMode          = TextureWrapMode.Clamp,
                    enableRandomWrite = true
                };
            
            dataTexture.Create();

            return dataTexture;
        }
        
        private void ShaderDispatch(int kernel)
        {
            computeShader.Dispatch(
                kernel,
                (int)textureResolution.x / 8,
                (int)textureResolution.y / 8,
                (int)textureResolution.z / 8
            );
        }
        
        private void ShaderSetTexturesData(int kernel)
        {
            computeShader.SetTexture(kernel, ShaderColorTemperatureTexture, _colorTemperatureTexture);
        }

        private void Start()
        {
            computeShader.SetVector(ShaderTextureResolution, textureResolution);
            
            computeShader.SetVector(ShaderAreaScale, transform.lossyScale);
            
            _colorTemperatureTexture = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            
            _kernelInit                 = computeShader.FindKernel("kernel_init");
            _kernelUserInput            = computeShader.FindKernel("kernel_user_input");
            _kernelDiffusionTemperature = computeShader.FindKernel("kernel_diffusion_temperature");
            _kernelModulesInfluence     = computeShader.FindKernel("kernel_modules_influence");
            
            
            
            // ANOTHER SHIT
            
            _kernelDiffusionVelocity = computeShader.FindKernel("kernel_diffusion_velocity");
            _velocityTexture = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            computeShader.SetTexture(_kernelInit, ShaderVelocityTexture, _velocityTexture);
            computeShader.SetTexture(_kernelDiffusionVelocity, ShaderVelocityTexture, _velocityTexture);
            
            _kernelDivergence = computeShader.FindKernel("kernel_divergence");
            _divergenceTexture = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            _pressureTexture = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            computeShader.SetTexture(_kernelInit, ShaderDivergenceTexture, _divergenceTexture);
            computeShader.SetTexture(_kernelInit, ShaderPressureTexture, _pressureTexture);
            computeShader.SetTexture(_kernelDivergence, ShaderDivergenceTexture, _divergenceTexture);
            computeShader.SetTexture(_kernelDivergence, ShaderPressureTexture, _pressureTexture);
            computeShader.SetTexture(_kernelDivergence, ShaderVelocityTexture, _velocityTexture);
            
            _kernelPressure = computeShader.FindKernel("kernel_pressure");
            computeShader.SetTexture(_kernelPressure, ShaderDivergenceTexture, _divergenceTexture);
            computeShader.SetTexture(_kernelPressure, ShaderPressureTexture, _pressureTexture);
            computeShader.SetTexture(_kernelPressure, ShaderVelocityTexture, _velocityTexture);
            
            _kernelSubtractGradient = computeShader.FindKernel("kernel_subtract_gradient");
            computeShader.SetTexture(_kernelSubtractGradient, ShaderPressureTexture, _pressureTexture);
            computeShader.SetTexture(_kernelSubtractGradient, ShaderVelocityTexture, _velocityTexture);

            _kernelAdvectionVelocity = computeShader.FindKernel("kernel_advection_velocity");
            computeShader.SetTexture(_kernelAdvectionVelocity, ShaderVelocityTexture, _velocityTexture);
            
            _kernelAdvectionTemperature = computeShader.FindKernel("kernel_advection_temperature");
            computeShader.SetTexture(_kernelAdvectionTemperature, ShaderVelocityTexture, _velocityTexture);
            computeShader.SetTexture(_kernelAdvectionTemperature, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            
            _kernelAddFire = computeShader.FindKernel("kernel_add_fire");
            computeShader.SetTexture(_kernelAddFire, ShaderVelocityTexture, _velocityTexture);
            
            _kernelReadData = computeShader.FindKernel("kernel_read_data");
            
            ShaderSetTexturesData(_kernelInit);
            ShaderSetTexturesData(_kernelUserInput);
            ShaderSetTexturesData(_kernelDiffusionTemperature);
            ShaderSetTexturesData(_kernelModulesInfluence);
            
            ShaderSetTexturesData(_kernelReadData);
            
            renderMaterial.SetTexture(MainTex, _colorTemperatureTexture);
            
            _moduleRenderer = GameObject.Find("ModuleRenderer").gameObject.GetComponent<ModuleRenderer>();
            _modules = new Module[_moduleRenderer.modulesCount];
            for (var i = 0; i < _moduleRenderer.modulesCount; i++)
            {
                _modules[i] = _moduleRenderer.transformsArray[i].GetComponent<Module>();
            }
            computeShader.SetInt(ModulesNumber, _moduleRenderer.modulesCount);
            
            // fields to transfer data from grid to modules
            _transferArray
                = new NativeArray<Vector4>(
                    (int)textureResolution.x 
                        * (int)textureResolution.y 
                            * (int)textureResolution.z,
                    Allocator.Persistent
                );
            
            _posForGetDataStride = sizeof(float) * 4;
            
            _posForSetDataStride = sizeof(float) * 4;
            
            _textureForSampleTemperature = new Texture3D(
                _colorTemperatureTexture.width,
                _colorTemperatureTexture.height,
                _colorTemperatureTexture.volumeDepth,
                _colorTemperatureTexture.graphicsFormat,
                TextureCreationFlags.None
            );
            
            ///*
            var trees = GameObject.FindGameObjectsWithTag("Tree");
            _orderedModuleList = new List<Module>(_moduleRenderer.modulesCount);
            _orderedRigidbodyDictionary = new Dictionary<Rigidbody, Module>(_moduleRenderer.modulesCount);
            foreach (var tree in trees)
            {
                var parent = tree.transform;
                foreach (Transform child in parent)
                {
                    var module = child.gameObject.GetComponent<Module>();

                    if (child.GetSiblingIndex() > 0)
                    {
                        // used to order modules structure
                        _orderedModuleList.Add(module);
                    }
                
                    // used for search connected modules
                    _orderedRigidbodyDictionary.Add(module.rigidBody, module);
                }
            }

            /*
            for (var i = _orderedModuleList.Count - 1; i >= 0; i--)
            {
                var currentModule = _orderedModuleList[i];
                Debug.Log("parent: " + currentModule.transform.parent.gameObject.name 
                                     + " first: " + currentModule.gameObject.name 
                                        + " second: " + _orderedRigidbodyDictionary[currentModule.fixedJoint.connectedBody].gameObject.name);
            }
            */
            //*/
            
            // modules ambient temperature
            _ambientTemperatureArray 
                = new NativeArray<float>(
                    _moduleRenderer.modulesCount,
                    Allocator.Persistent
                );
            
            AsyncGPUReadbackCallback += TransferTextureData;
            AsyncGPUReadback.RequestIntoNativeArray(
                ref _transferArray,
                _colorTemperatureTexture,
                0,
                AsyncGPUReadbackCallback
            );
            
            ShaderDispatch(_kernelInit);
        }
        
        private static event Action<AsyncGPUReadbackRequest> AsyncGPUReadbackCallback;

        private void TransferDataFromShader()
        {
            if (_moduleRenderer != null)
            {
                if (_moduleRenderer.modulesCount > 0)
                {
                    // output
                    var positionTemperatureArray = new NativeArray<Vector4>(
                        _moduleRenderer.modulesCount,
                        Allocator.TempJob,
                        NativeArrayOptions.UninitializedMemory
                    );
                
                    // fill the output temperature array
                    var job = new TransferDataFromGridJob()
                    {
                        // unchangeable
                        centers = _moduleRenderer.centers,
                        wildfireAreaTransform = transform.worldToLocalMatrix,
                        
                        // result
                        posTempArray = positionTemperatureArray
                    };
                    var handle = job.Schedule(_moduleRenderer.transformsArray);
                    handle.Complete();
                    
                    if (_posForGetData == null)
                    {
                        _posForGetData = new ComputeBuffer(_moduleRenderer.modulesCount, _posForGetDataStride);
                    }
                    _posForGetData.SetData(positionTemperatureArray);
                    
                    computeShader.SetBuffer(_kernelReadData, PosForGetDataBuffer, _posForGetData);
                    
                    _textureForSampleTemperature.SetPixelData(_transferArray, 0);
                    _textureForSampleTemperature.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                    
                    computeShader.SetTexture(_kernelReadData, TextureForSampleTemperature, _textureForSampleTemperature);
                    
                    computeShader.Dispatch(
                        _kernelReadData,
                        1,
                        1,
                        1
                    );
                    
                    var temp = new Vector4[_moduleRenderer.modulesCount];
                    _posForGetData.GetData(temp);

                    for (var i = 0; i < temp.Length; i++)
                    {
                        _ambientTemperatureArray[i] = temp[i].w;
                        
                        //Debug.Log("cell temperature: " + temp[i].w);
                    }
                    
                    positionTemperatureArray.Dispose();
                }
            }
        }
        
        private void TransferTextureData(AsyncGPUReadbackRequest request)
        {
            if (request.done && !request.hasError)
            {
                // read data about modules temperature
                if (_colorTemperatureTexture != null)
                {
                    TransferDataFromShader();
                    
                    AsyncGPUReadback.RequestIntoNativeArray(
                        ref _transferArray,
                        _colorTemperatureTexture,
                        0,
                        AsyncGPUReadbackCallback
                    );
                }
            }
        }
        
        private void CleanupPosForGetData()
        {
            if (_posForGetData != null)
            {
                _posForGetData.Release();
            }
            _posForGetData = null;
        }
        
        private void CleanupPosForSetData()
        {
            if (_posForSetData != null)
            {
                _posForSetData.Release();
            }
            _posForSetData = null;
        }
        
        // transfer data from modules to grid
        private void TransferDataFromModulesToGrid()
        {
            if (_moduleRenderer)
            {
                if (_moduleRenderer.transformsArray.length > 0)
                {
                    // MAIN SIMULATION LOGIC
                    
                    
                    // tree diffusion
                    
                    for (var i = _orderedModuleList.Count - 1; i >= 0; i--)
                    {
                        var module = _orderedModuleList[i];
                        var neighbour = _orderedRigidbodyDictionary[module.fixedJoint.connectedBody];
                        
                        var middleRadius = (module.capsuleCollider.radius + neighbour.capsuleCollider.radius) / 2.0f;
                        var surfaceArea = Mathf.PI * Mathf.Pow(middleRadius, 2);

                        var heatPerSecond = 0.2f * surfaceArea * Mathf.Abs(module.temperature - neighbour.temperature) / 0.0001f;
                        var heat = heatPerSecond * Time.fixedDeltaTime;
                        
                        if (module.temperature > neighbour.temperature)
                        {
                            var transferredTemperature = heat / (2000.0f * neighbour.rigidBody.mass);
                            
                            module.temperature -= transferredTemperature;
                            neighbour.temperature += transferredTemperature;
                        }
                        if (neighbour.temperature > module.temperature)
                        {
                            var transferredTemperature = heat / (2000.0f * module.rigidBody.mass);
                            
                            module.temperature += transferredTemperature;
                            neighbour.temperature -= transferredTemperature;
                        }
                    }
                    
                    
                    // tree conduction
                    
                    // input
                    var updatedAmbientTemperatureArray = new NativeArray<float>(
                        _moduleRenderer.modulesCount,
                        Allocator.TempJob
                    );
                    
                    for (var i = 0; i < _moduleRenderer.modulesCount; i++)
                    {
                        // module take heat by combustion
                        
                        var transferAmbientTemperature = 0.0f;

                        var lostMass = _modules[i].CalculateLostMass();
                        if (!Mathf.Approximately(lostMass, 0))
                        {
                            var releaseTemperature = (lostMass * 150000.0f) / 1000.0f;

                            transferAmbientTemperature = releaseTemperature;
                            
                            _modules[i].RecalculateCharacteristics(lostMass);
                            
                            Debug.Log("transfer: " + releaseTemperature * 1000.0f);
                        }
                        
                        updatedAmbientTemperatureArray[i] = transferAmbientTemperature;
                        
                        
                        // module lost heat by air

                        var ambientTemperature = _ambientTemperatureArray[i];
                        
                        Debug.Log("mod: " + _modules[i].temperature * 1000.0f + " amb: " + ambientTemperature * 1000.0f);
                        
                        var temperatureDifference = ambientTemperature - _modules[i].temperature;
                        var transferredTemperature = 0.001f * temperatureDifference;
                        
                        _modules[i].temperature += transferredTemperature;
                    }
                    
                    // output
                    var positionTemperatureArray = new NativeArray<Vector4>(
                        _moduleRenderer.modulesCount,
                        Allocator.TempJob,
                        NativeArrayOptions.UninitializedMemory
                    );
                    
                    // job process
                    var job = new TransferDataFromModulesJob()
                    {
                        // input
                        centers = _moduleRenderer.centers,
                        wildfireZoneTransform = transform.worldToLocalMatrix,
                        
                        // changeable
                        releaseTemperatureArray = updatedAmbientTemperatureArray,
                        
                        // result
                        positionTemperatureArray = positionTemperatureArray
                    };
                    var handle = job.Schedule(_moduleRenderer.transformsArray);
                    handle.Complete();
                    
                    if (_posForSetData == null)
                    {
                        _posForSetData = new ComputeBuffer(_moduleRenderer.modulesCount, _posForSetDataStride);
                    }
                    _posForSetData.SetData(positionTemperatureArray);
                    
                    computeShader.SetBuffer(_kernelModulesInfluence, PosForSetDataBuffer, _posForSetData);
                    
                    computeShader.Dispatch(
                        _kernelModulesInfluence,
                        1,
                        1,
                        1
                    );
                    
                    positionTemperatureArray.Dispose();
                    updatedAmbientTemperatureArray.Dispose();
                }
            }
        }

        private void SimulateTreesDynamics()
        {
            if (winds.Length > 0 && _moduleRenderer)
            {
                // wind
                var windForce = winds[0].GetWindForceAtPosition();
                
                /*
                for (var i = 0; i < _moduleRenderer.modulesCount; i++)
                {
                    _modules[i].rigidBody.AddForce(windForce * 100.0f, ForceMode.Force);

                    //var position = _modules[i].rigidBody.position;
                    //Debug.DrawLine(position, position + windForce.normalized * 0.1f, Color.red, 1.0f);
                }
                */
                
                //var windDirection = windForce.normalized;
                //var windStrength = windForce.magnitude;
                
                //computeShader.SetVector(ShaderWindDirection, windDirection);
                //computeShader.SetFloat(ShaderWindStrength, windStrength);

                // fire
                var fireForce = winds[1].GetWindForceAtPosition();
                
                var fireDirection = fireForce.normalized;
                var fireStrength = fireForce.magnitude;
                
                computeShader.SetVector(ShaderFireDirection, fireDirection);
                computeShader.SetFloat(ShaderFireStrength, fireStrength);
                
                ShaderDispatch(_kernelAddFire);
            }
        }

        private void FixedUpdate()
        {
            computeShader.SetFloat(ShaderDeltaTime, Time.fixedDeltaTime);
            computeShader.SetFloat(ShaderTorchIntensity, torchIntensity);
            computeShader.SetFloat(ShaderDiffusionIntensity, diffusionIntensity);
            computeShader.SetFloat(ShaderViscosityIntensity, viscosityIntensity);

            var torchPosition = transform.InverseTransformPoint(torch.transform.position);
            computeShader.SetVector(ShaderTorchPosition, torchPosition);
            
            if (Input.GetMouseButton(0))
            {
                computeShader.Dispatch(
                    _kernelUserInput,
                    1,
                    1,
                    1
                );
            }
            
            // transfer data from modules to grid
            TransferDataFromModulesToGrid();
            
            SimulateTreesDynamics();
            
            // DIFFUSE VELOCITY
            for (var i = 0; i < solverIterations; i++)
            {
                ShaderDispatch(_kernelDiffusionVelocity);
            }
            
            // PROJECT
            ShaderDispatch(_kernelDivergence);
            for (var i = 0; i < solverIterations; i++)
            {
                ShaderDispatch(_kernelPressure);
            }
            ShaderDispatch(_kernelSubtractGradient);
            
            // ADVECT VELOCITY
            ShaderDispatch(_kernelAdvectionVelocity);
            
            // PROJECT
            ShaderDispatch(_kernelDivergence);
            for (var i = 0; i < solverIterations; i++)
            {
                ShaderDispatch(_kernelPressure);
            }
            ShaderDispatch(_kernelSubtractGradient);
            
            
            // DIFFUSE TEMPERATURE
            for (var i = 0; i < solverIterations; i++)
            {
                ShaderDispatch(_kernelDiffusionTemperature);
            }
            
            // ADVECT TEMPERATURE
            ShaderDispatch(_kernelAdvectionTemperature);
            
            // READ DATA
            if (_posForGetData != null)
            {
                ShaderDispatch(_kernelReadData);
            }
        }
        
        private void OnDestroy()
        {
            AsyncGPUReadbackCallback -= TransferTextureData;

            _ambientTemperatureArray.Dispose();
            
            CleanupPosForGetData();
            CleanupPosForSetData();
        }
    }
}