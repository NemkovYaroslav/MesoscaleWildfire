using System;
using System.Collections.Generic;
using Common.Renderer;
using Resources.PathCreator.Core.Runtime.Placer;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Jobs;
using UnityEngine.Rendering;

namespace Common.Wildfire
{
    public class WildfireScript : MonoBehaviour
    {
        [Header("Common Settings")]
        [SerializeField] private ComputeShader computeShader;
        [SerializeField] private Vector3 textureResolution;
        [SerializeField] private Material renderMaterial;
        
        [Header("Solver Settings")]
        [SerializeField] private float torchIntensity;
        [SerializeField] private float diffusionIntensity;
        [SerializeField] private float viscosityIntensity;
        [SerializeField] private int   solverIterations;
        
        [Header("Wind Settings")]
        [SerializeField] private Wind.Wind[] winds;
        
        
        // COMMON VARIABLES
        private Module[] _modules;
        private ModuleRenderer _moduleRenderer;
        private GameObject _torch;
        
        
        // BUFFER FOR GET/SET DATA FROM/TO SHADER
        private ComputeBuffer _modulePositionsForSetData;
        private ComputeBuffer _modulePositionsForGetData;
        private int _modulePositionsDataStride;
        
        /*
        // TREE HIERARCHY
        private List<Module> _orderedModuleList;
        private Dictionary<Rigidbody, Module> _orderedRigidbodyDictionary;
        */
        
        
        // FLUID SOLVER STEPS
        private int _kernelInit;
        private int _kernelUserInput;
        private int _kernelModulesInfluence;
        private int _kernelDiffusionVelocity;
        private int _kernelDivergence;
        private int _kernelPressure;
        private int _kernelSubtractGradient;
        private int _kernelAdvectionVelocity;
        private int _kernelAdvectionTemperature;
        private int _kernelDiffusionTemperature;
        private int _kernelAddFire;
        private int _kernelReadData;
        
        
        // FLUID SOLVER VARIABLES
        private RenderTexture _colorTemperatureTexture;
        private RenderTexture _velocityTexture;
        private RenderTexture _divergenceTexture;
        private RenderTexture _pressureTexture;
        private Texture3D _textureForSampleModulesAmbientTemperature;
        
        
        // FLUID SOLVER VARIABLES NAMING
        private static readonly int WildfireAreaTexture           = Shader.PropertyToID("_MainTex");
        private static readonly int ShaderTextureResolution       = Shader.PropertyToID("texture_resolution");
        private static readonly int ShaderTorchIntensity          = Shader.PropertyToID("torch_intensity");
        private static readonly int ShaderDiffusionIntensity      = Shader.PropertyToID("diffusion_intensity");
        private static readonly int ShaderViscosityIntensity      = Shader.PropertyToID("viscosity_intensity");
        private static readonly int ShaderDeltaTime               = Shader.PropertyToID("delta_time");
        private static readonly int ShaderTorchPosition           = Shader.PropertyToID("torch_position");
        private static readonly int ModulesNumber                 = Shader.PropertyToID("modules_number");
        private static readonly int ShaderColorTemperatureTexture = Shader.PropertyToID("color_temperature_texture");
        private static readonly int PosForGetDataBuffer           = Shader.PropertyToID("pos_for_get_data");
        private static readonly int TextureForSampleTemperature   = Shader.PropertyToID("texture_for_sample_modules_ambient_temperature");
        private static readonly int PosForSetDataBuffer           = Shader.PropertyToID("pos_for_set_data");
        private static readonly int ShaderVelocityTexture         = Shader.PropertyToID("velocity_texture");
        private static readonly int ShaderDivergenceTexture       = Shader.PropertyToID("divergence_texture");
        private static readonly int ShaderPressureTexture         = Shader.PropertyToID("pressure_texture");
        
        
        // transfer data from gpu to cpu
        private NativeArray<Vector4> _getModulesDataArray;
        private NativeArray<float> _modulesAmbientTemperatureArray;
        
        
        // WIND VARIABLES
        //private static readonly int ShaderWindDirection = Shader.PropertyToID("wind_direction");
        //private static readonly int ShaderWindStrength = Shader.PropertyToID("wind_strength");
        private static readonly int ShaderFireDirection = Shader.PropertyToID("fire_direction");
        private static readonly int ShaderFireStrength = Shader.PropertyToID("fire_strength");
        
        
        // create render texture in 3D format
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
        
        // dispatch method for grid operations
        private void ShaderDispatch(int kernel)
        {
            computeShader.Dispatch(
                kernel,
                (int)textureResolution.x / 8,
                (int)textureResolution.y / 8,
                (int)textureResolution.z / 8
            );
        }

        
        private void Start()
        {
            // CREATE MAIN TEXTURES FOR FLUID SOLVER
            _colorTemperatureTexture = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            _velocityTexture         = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            _divergenceTexture       = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            _pressureTexture         = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            
            
            // SET TEXTURE FOR TEMPERATURE RENDERING (RAY MARCHING DEBUG)
            renderMaterial.SetTexture(WildfireAreaTexture, _colorTemperatureTexture);
            
            
            // FILL MAIN STEPS OF FLUID SOLVER
            _kernelInit                 = computeShader.FindKernel("kernel_init");
            _kernelUserInput            = computeShader.FindKernel("kernel_user_input");
            _kernelModulesInfluence     = computeShader.FindKernel("kernel_modules_influence");
            _kernelDiffusionVelocity    = computeShader.FindKernel("kernel_diffusion_velocity");
            _kernelDivergence           = computeShader.FindKernel("kernel_divergence");
            _kernelPressure             = computeShader.FindKernel("kernel_pressure");
            _kernelSubtractGradient     = computeShader.FindKernel("kernel_subtract_gradient");
            _kernelAdvectionVelocity    = computeShader.FindKernel("kernel_advection_velocity");
            _kernelAdvectionTemperature = computeShader.FindKernel("kernel_advection_temperature");
            _kernelDiffusionTemperature = computeShader.FindKernel("kernel_diffusion_temperature");
            _kernelAddFire              = computeShader.FindKernel("kernel_add_fire");
            _kernelReadData             = computeShader.FindKernel("kernel_read_data");
            
            
            // SET MAIN TEXTURES TO FLUID SOLVER
            computeShader.SetTexture(_kernelInit, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            computeShader.SetTexture(_kernelInit, ShaderVelocityTexture, _velocityTexture);
            computeShader.SetTexture(_kernelInit, ShaderDivergenceTexture, _divergenceTexture);
            computeShader.SetTexture(_kernelInit, ShaderPressureTexture, _pressureTexture);
            computeShader.SetTexture(_kernelUserInput, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            computeShader.SetTexture(_kernelModulesInfluence, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            computeShader.SetTexture(_kernelDiffusionVelocity, ShaderVelocityTexture, _velocityTexture);
            computeShader.SetTexture(_kernelDivergence, ShaderDivergenceTexture, _divergenceTexture);
            computeShader.SetTexture(_kernelDivergence, ShaderPressureTexture, _pressureTexture);
            computeShader.SetTexture(_kernelDivergence, ShaderVelocityTexture, _velocityTexture);
            computeShader.SetTexture(_kernelPressure, ShaderDivergenceTexture, _divergenceTexture);
            computeShader.SetTexture(_kernelPressure, ShaderPressureTexture, _pressureTexture);
            computeShader.SetTexture(_kernelPressure, ShaderVelocityTexture, _velocityTexture);
            computeShader.SetTexture(_kernelSubtractGradient, ShaderPressureTexture, _pressureTexture);
            computeShader.SetTexture(_kernelSubtractGradient, ShaderVelocityTexture, _velocityTexture);
            computeShader.SetTexture(_kernelAdvectionVelocity, ShaderVelocityTexture, _velocityTexture);
            computeShader.SetTexture(_kernelAdvectionTemperature, ShaderVelocityTexture, _velocityTexture);
            computeShader.SetTexture(_kernelAdvectionTemperature, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            computeShader.SetTexture(_kernelDiffusionTemperature, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            computeShader.SetTexture(_kernelAddFire, ShaderVelocityTexture, _velocityTexture);
            computeShader.SetTexture(_kernelReadData, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            
            
            // INITIALIZE COMMON VARIABLES
            _torch          = GameObject.FindWithTag("Torch");
            _moduleRenderer = GameObject.FindWithTag("ModulesRenderer").GetComponent<ModuleRenderer>();
            _modules        = new Module[_moduleRenderer.modulesCount];
            
            for (var i = 0; i < _moduleRenderer.modulesCount; i++)
            {
                _modules[i] = _moduleRenderer.transformsArray[i].GetComponent<Module>();
            }
            
            
            // SET VARIABLES FOR FLUID SOLVER
            computeShader.SetVector(ShaderTextureResolution, textureResolution);
            computeShader.SetInt(ModulesNumber, _moduleRenderer.modulesCount);
            
            
            // ARRAY FOR ASYNC GET DATA FROM FLUID SOLVER
            _getModulesDataArray
                = new NativeArray<Vector4>(
                    (int)textureResolution.x * (int)textureResolution.y * (int)textureResolution.z,
                    Allocator.Persistent
                );
            
            // used to interpolate current modules temperature
            _textureForSampleModulesAmbientTemperature = new Texture3D(
                _colorTemperatureTexture.width, _colorTemperatureTexture.height, _colorTemperatureTexture.volumeDepth,
                _colorTemperatureTexture.graphicsFormat,
                TextureCreationFlags.None
            );
            
            _modulePositionsDataStride = sizeof(float) * 4;
            
            _modulesAmbientTemperatureArray 
                = new NativeArray<float>(
                    _moduleRenderer.modulesCount,
                    Allocator.Persistent
                );
            
            AsyncGPUReadbackCallback += ReadDataFromGrid;
            AsyncGPUReadback.RequestIntoNativeArray(
                ref _getModulesDataArray,
                _colorTemperatureTexture,
                0,
                AsyncGPUReadbackCallback
            );
            
            
            // INITIALIZE VARIABLES IN FLUID SOLVER
            ShaderDispatch(_kernelInit);
        }
        
        // ASYNC EVENT FOR REAL-TIME READ DATA FROM FLUID SOLVER
        private static event Action<AsyncGPUReadbackRequest> AsyncGPUReadbackCallback;

        private void TransferDataFromGridToModules()
        {
            if (_moduleRenderer)
            {
                if (_moduleRenderer.modulesCount > 0)
                {
                    var modulesPositionTemperatureArray = new NativeArray<Vector4>(
                        _moduleRenderer.modulesCount,
                        Allocator.TempJob,
                        NativeArrayOptions.UninitializedMemory
                    );
                    
                    var job = new CalculateModulePositionsJob()
                    {
                        centers = _moduleRenderer.centers,
                        wildfireAreaTransform = transform.worldToLocalMatrix,
                        modulesPositionTemperatureArray = modulesPositionTemperatureArray
                    };
                    var handle = job.Schedule(_moduleRenderer.transformsArray);
                    handle.Complete();
                    
                    if (_modulePositionsForGetData == null)
                    {
                        _modulePositionsForGetData = new ComputeBuffer(_moduleRenderer.modulesCount, _modulePositionsDataStride);
                    }
                    _modulePositionsForGetData.SetData(modulesPositionTemperatureArray);
                    
                    computeShader.SetBuffer(_kernelReadData, PosForGetDataBuffer, _modulePositionsForGetData);
                    
                    _textureForSampleModulesAmbientTemperature.SetPixelData(_getModulesDataArray, 0);
                    _textureForSampleModulesAmbientTemperature.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                    
                    computeShader.SetTexture(_kernelReadData, TextureForSampleTemperature, _textureForSampleModulesAmbientTemperature);
                    
                    computeShader.Dispatch(
                        _kernelReadData,
                        _moduleRenderer.modulesCount / 8,
                        1,
                        1
                    );
                    
                    var temp = new Vector4[_moduleRenderer.modulesCount];
                    _modulePositionsForGetData.GetData(temp);

                    for (var i = 0; i < temp.Length; i++)
                    {
                        _modulesAmbientTemperatureArray[i] = temp[i].w;
                    }
                    
                    modulesPositionTemperatureArray.Dispose();
                }
            }
        }
        
        private void ReadDataFromGrid(AsyncGPUReadbackRequest request)
        {
            if (request.done && !request.hasError)
            {
                if (_colorTemperatureTexture != null)
                {
                    // transfer data from fluid solver to modules
                    TransferDataFromGridToModules();
                    
                    // the event triggers itself upon completion
                    AsyncGPUReadback.RequestIntoNativeArray(
                        ref _getModulesDataArray,
                        _colorTemperatureTexture,
                        0,
                        AsyncGPUReadbackCallback
                    );
                }
            }
        }
        
        private void CleanupPosForGetData()
        {
            if (_modulePositionsForGetData != null)
            {
                _modulePositionsForGetData.Release();
            }
            _modulePositionsForGetData = null;
        }
        
        private void CleanupPosForSetData()
        {
            if (_modulePositionsForSetData != null)
            {
                _modulePositionsForSetData.Release();
            }
            _modulePositionsForSetData = null;
        }
        
        // transfer data from modules to grid
        private void TransferDataFromModulesToGrid()
        {
            if (_moduleRenderer)
            {
                if (_moduleRenderer.modulesCount > 0)
                {
                    // MODULES TEMPERATURE DIFFUSION IN TREE HIERARCHY
                    for (var i = _moduleRenderer.orderedModuleList.Count - 1; i >= 0; i--)
                    {
                        var module = _moduleRenderer.orderedModuleList[i];
                        var neighbour = module.neighbourModule;

                        if (neighbour)
                        {
                            var middleTemperature = (module.temperature + neighbour.temperature) / 2.0f;
                            var transferredTemperature = 0.001f * middleTemperature;
                            if (module.temperature > neighbour.temperature)
                            {
                                module.temperature -= transferredTemperature;
                                neighbour.temperature += transferredTemperature;
                            }
                            if (neighbour.temperature > module.temperature)
                            {
                                module.temperature += transferredTemperature;
                                neighbour.temperature -= transferredTemperature;
                            }
                        }
                    }
                    
                    // MODULES COMBUSTION
                    
                    var updatedAmbientTemperatureArray = new NativeArray<float>(
                        _moduleRenderer.modulesCount,
                        Allocator.TempJob
                    );
                    
                    for (var i = 0; i < _moduleRenderer.modulesCount; i++)
                    {
                        // module take heat by combustion
                        
                        var transferAmbientTemperature = 0.0f;

                        if (_modules[i].temperature < _modules[i].stopCombustionMass)
                        {
                            var lostMass = _modules[i].CalculateLostMass();
                            if (!Mathf.Approximately(lostMass, 0))
                            {
                                var releaseTemperature = (lostMass * 75000.0f) / 1000.0f;

                                transferAmbientTemperature = releaseTemperature;
                            
                                _modules[i].RecalculateCharacteristics(lostMass);
                            }
                        }
                        
                        var ambientTemperature = _modulesAmbientTemperatureArray[i];
                        Debug.Log("mod: " + _modules[i].temperature * 1000.0f + " amb: " + ambientTemperature * 1000.0f);
                        if (ambientTemperature > _modules[i].temperature)
                        {
                            var temperatureDifference = ambientTemperature - _modules[i].temperature;
                            var transferredTemperature = 0.01f * temperatureDifference;

                            _modules[i].temperature += transferredTemperature;
                            transferAmbientTemperature -= transferredTemperature;
                        }
                        if (_modules[i].temperature > ambientTemperature)
                        {
                            var temperatureDifference = _modules[i].temperature - ambientTemperature;
                            var transferredTemperature = 0.001f * temperatureDifference;

                            transferAmbientTemperature += transferredTemperature;
                            _modules[i].temperature -= transferredTemperature;
                        }
                        
                        updatedAmbientTemperatureArray[i] = transferAmbientTemperature;
                        
                    }
                    
                    var positionTemperatureArray = new NativeArray<Vector4>(
                        _moduleRenderer.modulesCount,
                        Allocator.TempJob,
                        NativeArrayOptions.UninitializedMemory
                    );
                    
                    var job = new TransferDataFromModulesJob()
                    {
                        centers = _moduleRenderer.centers,
                        wildfireZoneTransform = transform.worldToLocalMatrix,
                        releaseTemperatureArray = updatedAmbientTemperatureArray,
                        positionTemperatureArray = positionTemperatureArray
                    };
                    var handle = job.Schedule(_moduleRenderer.transformsArray);
                    handle.Complete();
                    
                    if (_modulePositionsForSetData == null)
                    {
                        _modulePositionsForSetData = new ComputeBuffer(_moduleRenderer.modulesCount, _modulePositionsDataStride);
                    }
                    _modulePositionsForSetData.SetData(positionTemperatureArray);
                    
                    computeShader.SetBuffer(_kernelModulesInfluence, PosForSetDataBuffer, _modulePositionsForSetData);
                    
                    computeShader.Dispatch(
                        _kernelModulesInfluence,
                        _moduleRenderer.modulesCount / 8,
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
                
                ///*
                for (var i = 0; i < _moduleRenderer.modulesCount; i++)
                {
                    _modules[i].rigidBody.AddForce(windForce * 1000.0f, ForceMode.Force);

                    //var position = _modules[i].rigidBody.position;
                    //Debug.DrawLine(position, position + windForce.normalized * 0.1f, Color.red, 1.0f);
                }
                //*/
                
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
            computeShader.SetFloat(ShaderDiffusionIntensity, diffusionIntensity);
            computeShader.SetFloat(ShaderViscosityIntensity, viscosityIntensity);
            
            if (Input.GetMouseButton(0))
            {
                computeShader.SetFloat(ShaderTorchIntensity, torchIntensity);
                
                var torchPosition = transform.InverseTransformPoint(_torch.transform.position);
                computeShader.SetVector(ShaderTorchPosition, torchPosition);
                
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
            
            // FLUID SOLVER STEPS
            // diffuse velocity
            for (var i = 0; i < solverIterations; i++)
            {
                ShaderDispatch(_kernelDiffusionVelocity);
            }
            // project velocity
            ShaderDispatch(_kernelDivergence);
            for (var i = 0; i < solverIterations; i++)
            {
                ShaderDispatch(_kernelPressure);
            }
            ShaderDispatch(_kernelSubtractGradient);
            // advect velocity
            ShaderDispatch(_kernelAdvectionVelocity);
            // project velocity
            ShaderDispatch(_kernelDivergence);
            for (var i = 0; i < solverIterations; i++)
            {
                ShaderDispatch(_kernelPressure);
            }
            ShaderDispatch(_kernelSubtractGradient);
            // diffuse temperature
            for (var i = 0; i < solverIterations; i++)
            {
                ShaderDispatch(_kernelDiffusionTemperature);
            }
            // advect temperature
            ShaderDispatch(_kernelAdvectionTemperature);
            
            
            // READ DATA
            if (_modulePositionsForGetData != null)
            {
                ShaderDispatch(_kernelReadData);
            }
        }
        
        private void OnDestroy()
        {
            AsyncGPUReadbackCallback -= ReadDataFromGrid;

            _modulesAmbientTemperatureArray.Dispose();
            
            CleanupPosForGetData();
            CleanupPosForSetData();
        }
    }
}