using System;
using Common.Renderer;
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
        private ModuleRenderer _moduleRenderer;
        private GameObject _torch;
        
        
        // BUFFER FOR GET/SET DATA FROM/TO SHADER
        private ComputeBuffer _modulePositionsForGetDataBuffer;
        private ComputeBuffer _modulePositionsForSetDataBuffer;
        private int _modulePositionsDataStride;
        
        
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
        
        
        // FLUID SOLVER VARIABLES NAMING
        private static readonly int WildfireAreaTexture                  = Shader.PropertyToID("_MainTex");
        private static readonly int ShaderTextureResolution              = Shader.PropertyToID("texture_resolution");
        private static readonly int ShaderTorchIntensity                 = Shader.PropertyToID("torch_intensity");
        private static readonly int ShaderDiffusionIntensity             = Shader.PropertyToID("diffusion_intensity");
        private static readonly int ShaderViscosityIntensity             = Shader.PropertyToID("viscosity_intensity");
        private static readonly int ShaderDeltaTime                      = Shader.PropertyToID("delta_time");
        private static readonly int ShaderTorchPosition                  = Shader.PropertyToID("torch_position");
        private static readonly int ModulesNumber                        = Shader.PropertyToID("modules_number");
        private static readonly int ShaderColorTemperatureTexture        = Shader.PropertyToID("color_temperature_texture");
        private static readonly int ModulePositionsBuffer                = Shader.PropertyToID("pos_for_get_data");
        private static readonly int TextureForSampleAmbientTemperature   = Shader.PropertyToID("texture_for_sample_ambient_temperature");
        private static readonly int PosForSetDataBuffer                  = Shader.PropertyToID("pos_for_set_data");
        private static readonly int ShaderVelocityTexture                = Shader.PropertyToID("velocity_texture");
        private static readonly int ShaderDivergenceTexture              = Shader.PropertyToID("divergence_texture");
        private static readonly int ShaderPressureTexture                = Shader.PropertyToID("pressure_texture");
        
        
        // used to transfer data from grid to modules
        private NativeArray<Vector4> _transferedAmbientTemperatureDataArray;
        private Texture3D _textureForSampleAmbientTemperature;
        private NativeArray<float> _modulesAmbientTemperatureArray;
        // used to transfer data from modules to grid
        private NativeArray<Vector4> _modulesGeneratedTemperatureArray;
        
        
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
            
            
            // SET VARIABLES FOR FLUID SOLVER
            computeShader.SetVector(ShaderTextureResolution, textureResolution);
            computeShader.SetInt(ModulesNumber, _moduleRenderer.modulesCount);
            
            
            // ARRAY FOR ASYNC GET DATA FROM FLUID SOLVER
            _transferedAmbientTemperatureDataArray
                = new NativeArray<Vector4>(
                    (int)textureResolution.x * (int)textureResolution.y * (int)textureResolution.z,
                    Allocator.Persistent
                );
            
            // used to interpolate current modules temperature
            _textureForSampleAmbientTemperature = new Texture3D(
                _colorTemperatureTexture.width, _colorTemperatureTexture.height, _colorTemperatureTexture.volumeDepth,
                _colorTemperatureTexture.graphicsFormat,
                TextureCreationFlags.None
            );
            
            
            // GET/SET TEMPERATURE FROM/TO GRID
            _modulePositionsDataStride       = sizeof(float) * 4;
            _modulePositionsForGetDataBuffer = new ComputeBuffer(_moduleRenderer.modulesCount, _modulePositionsDataStride);
            _modulePositionsForSetDataBuffer = new ComputeBuffer(_moduleRenderer.modulesCount, _modulePositionsDataStride);
            
            
            _modulesAmbientTemperatureArray 
                = new NativeArray<float>(
                    _moduleRenderer.modulesCount,
                    Allocator.Persistent
                );
            
            _modulesGeneratedTemperatureArray
                = new NativeArray<Vector4>(
                    _moduleRenderer.modulesCount, 
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            
            AsyncGPUReadbackCallback += ReadDataFromGrid;
            AsyncGPUReadback.RequestIntoNativeArray(
                ref _transferedAmbientTemperatureDataArray,
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
                if (_textureForSampleAmbientTemperature && _modulePositionsForGetDataBuffer.count > 0)
                {
                    // need for more accurate reading data (use texture sampler)
                    _textureForSampleAmbientTemperature.SetPixelData(_transferedAmbientTemperatureDataArray, 0);
                    _textureForSampleAmbientTemperature.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                    computeShader.SetTexture(_kernelReadData, TextureForSampleAmbientTemperature, _textureForSampleAmbientTemperature);
                    
                    // set modules position to buffer
                    var modulePositions = _moduleRenderer.modulePositionsArray;
                    _modulePositionsForGetDataBuffer.SetData(modulePositions);
                    computeShader.SetBuffer(_kernelReadData, ModulePositionsBuffer, _modulePositionsForGetDataBuffer);
                    
                    computeShader.Dispatch(
                        _kernelReadData,
                        modulePositions.Length / 8,
                        1,
                        1
                    );
                    
                    // GET AMBIENT TEMPERATURE DATA FROM GRID
                    var receivedData = new Vector4[modulePositions.Length];
                    _modulePositionsForGetDataBuffer.GetData(receivedData);

                    // TRANSFER DATA TO AMBIENT TEMPERATURE ARRAY
                    for (var i = 0; i < receivedData.Length; i++)
                    {
                        _modulesAmbientTemperatureArray[i] = receivedData[i].w;
                    }
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
                        ref _transferedAmbientTemperatureDataArray,
                        _colorTemperatureTexture,
                        0,
                        AsyncGPUReadbackCallback
                    );
                }
            }
        }
        
        
        // transfer data from modules to grid
        private void TransferDataFromModulesToGrid()
        {
            if (_moduleRenderer)
            {
                if (_moduleRenderer.modulesCount > 0)
                {
                    var modules = _moduleRenderer.orderedModuleList.ToArray();
                    
                    // SIMULATE DIFFUSION IN TREE HIERARCHY
                    for (var i = modules.Length - 1; i > 0; i--)
                    {
                        var module = modules[i];
                        var connectedModule = module.neighbourModule;

                        if (connectedModule)
                        {
                            const float moduleDiffusionFactor = 0.001f;
                            
                            var middleTemperature = (module.temperature + connectedModule.temperature) / 2.0f;
                            var transferredTemperature = moduleDiffusionFactor * middleTemperature;
                            if (module.temperature > connectedModule.temperature)
                            {
                                module.temperature -= transferredTemperature;
                                connectedModule.temperature += transferredTemperature;
                            }
                            if (connectedModule.temperature > module.temperature)
                            {
                                module.temperature += transferredTemperature;
                                connectedModule.temperature -= transferredTemperature;
                            }
                        }
                    }
                    
                    var updatedAmbientTemperatureArray 
                        = new NativeArray<float>(
                            _moduleRenderer.modulesCount,
                            Allocator.TempJob
                        );
                    
                    for (var i = 0; i < _moduleRenderer.modulesCount; i++)
                    {
                        var transferAmbientTemperature = 0.0f;

                        // SIMULATE COMBUSTION
                        if (modules[i].rigidBody.mass > modules[i].stopCombustionMass)
                        {
                            var lostMass = modules[i].CalculateLostMass();
                            if (!Mathf.Approximately(lostMass, 0))
                            {
                                var releaseTemperature = (lostMass * 75000.0f) / 1000.0f;

                                transferAmbientTemperature = releaseTemperature;
                            
                                modules[i].RecalculateCharacteristics(lostMass);
                            }
                        }
                        
                        // SIMULATE TEMPERATURE BALANCE BETWEEN MODULE AND AIR
                        var ambientTemperature = _modulesAmbientTemperatureArray[i];
                        //Debug.Log("mod: " + modules[i].temperature * 1000.0f + " amb: " + ambientTemperature * 1000.0f);
                        if (ambientTemperature > modules[i].temperature)
                        {
                            const float airToModuleTransferFactor = 0.01f;
                            
                            var temperatureDifference  = ambientTemperature - modules[i].temperature;
                            var transferredTemperature = airToModuleTransferFactor * temperatureDifference;

                            modules[i].temperature     += transferredTemperature;
                            transferAmbientTemperature -= transferredTemperature;
                        }
                        if (modules[i].temperature > ambientTemperature)
                        {
                            const float moduleToAirTransferFactor = 0.001f;
                            
                            var temperatureDifference  = modules[i].temperature - ambientTemperature;
                            var transferredTemperature = moduleToAirTransferFactor * temperatureDifference;

                            transferAmbientTemperature += transferredTemperature;
                            modules[i].temperature     -= transferredTemperature;
                        }
                        
                        updatedAmbientTemperatureArray[i] = transferAmbientTemperature;
                    }
                    
                    
                    var job = new SetModulesTemperatureDataJob()
                    {
                        modulePositionsArray                        = _moduleRenderer.modulePositionsArray,
                        releaseTemperatureArray                     = updatedAmbientTemperatureArray,
                        modulesWildfireAreaPositionTemperatureArray = _modulesGeneratedTemperatureArray
                    };
                    var handle = job.Schedule(_moduleRenderer.transformAccessArray);
                    handle.Complete();
                    
                    _modulePositionsForSetDataBuffer.SetData(_modulesGeneratedTemperatureArray);
                    computeShader.SetBuffer(_kernelModulesInfluence, PosForSetDataBuffer, _modulePositionsForSetDataBuffer);
                    
                    computeShader.Dispatch(
                        _kernelModulesInfluence,
                        _moduleRenderer.modulesCount / 8,
                        1,
                        1
                    );
                    
                    updatedAmbientTemperatureArray.Dispose();
                }
            }
        }

        private void SimulateTreesDynamics()
        {
            if (winds.Length > 0 && _moduleRenderer)
            {
                var modules = _moduleRenderer.orderedModuleList.ToArray();
                
                // wind
                var windForce = winds[0].GetWindForceAtPosition();
                
                ///*
                for (var i = 0; i < _moduleRenderer.modulesCount; i++)
                {
                    modules[i].rigidBody.AddForce(windForce, ForceMode.Force);

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
            
            
            // WILDFIRE SIMULATION
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
            if (_modulePositionsForGetDataBuffer != null)
            {
                ShaderDispatch(_kernelReadData);
            }
        }
        
        private void CleanupPosForGetData()
        {
            if (_modulePositionsForGetDataBuffer != null)
            {
                _modulePositionsForGetDataBuffer.Release();
            }
            _modulePositionsForGetDataBuffer = null;
        }
        
        private void CleanupPosForSetData()
        {
            if (_modulePositionsForSetDataBuffer != null)
            {
                _modulePositionsForSetDataBuffer.Release();
            }
            _modulePositionsForSetDataBuffer = null;
        }
        
        private void OnDestroy()
        {
            AsyncGPUReadbackCallback -= ReadDataFromGrid;

            //_getModulesDataArray.Dispose(); // can't be deallocated
            
            _modulesAmbientTemperatureArray.Dispose();
            _modulesGeneratedTemperatureArray.Dispose();
            
            CleanupPosForGetData();
            CleanupPosForSetData();
        }
    }
}