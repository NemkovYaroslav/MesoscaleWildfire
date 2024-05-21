using System;
using Common.Renderer;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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
        
        [Header("Wind Simulation Settings")]
        [SerializeField] private Wind.Wind wind;
        
        [Header("Fire Simulation Settings")]
        [SerializeField] private float fireLiftingPower = 0.005f;
        
        // COMMON VARIABLES
        private ModuleRenderer _moduleRenderer;
        private GameObject _torch;
        
        
        // BUFFER FOR GET/SET DATA FROM/TO SHADER
        private ComputeBuffer _getDataBuffer;
        private ComputeBuffer _setDataBuffer;
        private int _modulePositionsDataStride;
        
        
        // FLUID SOLVER STEPS
        private int _kernelInit;
        private int _kernelUserInput;
        private int _kernelReadData;
        private int _kernelWriteData;
        private int _kernelDiffusionVelocity;
        private int _kernelDivergence;
        private int _kernelPressure;
        private int _kernelSubtractGradient;
        private int _kernelAdvectionVelocity;
        private int _kernelAdvectionTemperature;
        private int _kernelDiffusionTemperature;
        private int _kernelSimulateFire;
        private int _kernelSimulateWind;
        
        
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
        private static readonly int GetDataBuffer                        = Shader.PropertyToID("get_data");
        private static readonly int TextureForSampleAmbientTemperature   = Shader.PropertyToID("texture_for_sample_ambient_temperature");
        private static readonly int SetDataBuffer                        = Shader.PropertyToID("set_data");
        private static readonly int ShaderVelocityTexture                = Shader.PropertyToID("velocity_texture");
        private static readonly int ShaderDivergenceTexture              = Shader.PropertyToID("divergence_texture");
        private static readonly int ShaderPressureTexture                = Shader.PropertyToID("pressure_texture");
        
        
        // used to transfer data from grid to modules
        private NativeArray<Vector4> _transferredAmbientTemperatureDataArray;
        private Texture3D _textureForSampleAmbientTemperature;
        private NativeArray<float> _modulesAmbientTemperatureArray;
        // used to transfer data from modules to grid
        private NativeArray<float> _modulesUpdatedAmbientTemperatureArray;
        
        
        // WIND VARIABLES
        private static readonly int ShaderWindDirection = Shader.PropertyToID("wind_direction");
        private static readonly int ShaderWindIntensity = Shader.PropertyToID("wind_intensity");
        private static readonly int ShaderFireLiftingPower = Shader.PropertyToID("fire_lifting_power");
        
        
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
            _kernelReadData             = computeShader.FindKernel("kernel_read_data");
            _kernelWriteData            = computeShader.FindKernel("kernel_write_data");
            _kernelDiffusionVelocity    = computeShader.FindKernel("kernel_diffusion_velocity");
            _kernelDivergence           = computeShader.FindKernel("kernel_divergence");
            _kernelPressure             = computeShader.FindKernel("kernel_pressure");
            _kernelSubtractGradient     = computeShader.FindKernel("kernel_subtract_gradient");
            _kernelAdvectionVelocity    = computeShader.FindKernel("kernel_advection_velocity");
            _kernelAdvectionTemperature = computeShader.FindKernel("kernel_advection_temperature");
            _kernelDiffusionTemperature = computeShader.FindKernel("kernel_diffusion_temperature");
            _kernelSimulateFire         = computeShader.FindKernel("kernel_simulate_fire");
            _kernelSimulateWind         = computeShader.FindKernel("kernel_simulate_wind");
            
            
            // SET MAIN TEXTURES TO FLUID SOLVER
            computeShader.SetTexture(_kernelInit, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            computeShader.SetTexture(_kernelInit, ShaderVelocityTexture, _velocityTexture);
            computeShader.SetTexture(_kernelInit, ShaderDivergenceTexture, _divergenceTexture);
            computeShader.SetTexture(_kernelInit, ShaderPressureTexture, _pressureTexture);
            computeShader.SetTexture(_kernelUserInput, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            computeShader.SetTexture(_kernelWriteData, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            computeShader.SetTexture(_kernelReadData, ShaderColorTemperatureTexture, _colorTemperatureTexture);
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
            computeShader.SetTexture(_kernelSimulateFire, ShaderVelocityTexture, _velocityTexture);
            computeShader.SetTexture(_kernelSimulateWind, ShaderVelocityTexture, _velocityTexture);
            
            // INITIALIZE COMMON VARIABLES
            _torch          = GameObject.FindWithTag("Torch");
            _moduleRenderer = GameObject.FindWithTag("ModulesRenderer").GetComponent<ModuleRenderer>();
            
            
            // SET VARIABLES FOR FLUID SOLVER
            computeShader.SetVector(ShaderTextureResolution, textureResolution);
            computeShader.SetInt(ModulesNumber, _moduleRenderer.modulesCount);
            
            
            // ARRAY FOR ASYNC GET DATA FROM FLUID SOLVER
            _transferredAmbientTemperatureDataArray
                = new NativeArray<Vector4>(
                    (int)textureResolution.x * (int)textureResolution.y * (int)textureResolution.z,
                    Allocator.Persistent
                );
            
            // used to interpolate current modules temperature
            _textureForSampleAmbientTemperature 
                = new Texture3D(
                    _colorTemperatureTexture.width,
                    _colorTemperatureTexture.height,
                    _colorTemperatureTexture.volumeDepth,
                    _colorTemperatureTexture.graphicsFormat,
                    TextureCreationFlags.None
                );
            
            
            // GET/SET TEMPERATURE FROM/TO GRID
            _getDataBuffer = new ComputeBuffer(_moduleRenderer.modulesCount, sizeof(float));
            _setDataBuffer = new ComputeBuffer(_moduleRenderer.modulesCount, sizeof(float));
            
            // modules ambient temperature (get from async read)
            _modulesAmbientTemperatureArray 
                = new NativeArray<float>(
                    _moduleRenderer.modulesCount,
                    Allocator.Persistent
                );
            
            // modules updated ambient temperature (set in combustion process)
            _modulesUpdatedAmbientTemperatureArray
                = new NativeArray<float>(
                    _moduleRenderer.modulesCount, 
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            
            AsyncGPUReadbackCallback += ReadDataFromGrid;
            AsyncGPUReadback.RequestIntoNativeArray(
                ref _transferredAmbientTemperatureDataArray,
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
            if (_textureForSampleAmbientTemperature)
            {
                // need for more accurate reading data (use texture sampler)
                _textureForSampleAmbientTemperature.SetPixelData(_transferredAmbientTemperatureDataArray, 0);
                _textureForSampleAmbientTemperature.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                computeShader.SetTexture(_kernelReadData, TextureForSampleAmbientTemperature, _textureForSampleAmbientTemperature);
            
                // set read buffer
                computeShader.SetBuffer(_kernelReadData, GetDataBuffer, _getDataBuffer);
                
                // read data from grid
                computeShader.Dispatch(
                    _kernelReadData,
                    _moduleRenderer.modulesCount / 8,
                    1,
                    1
                );
                
            
                // copy received data
                var receivedData = new float[_moduleRenderer.modulesCount];
                _getDataBuffer.GetData(receivedData);

                // set received data
                for (var i = 0; i < receivedData.Length; i++)
                {
                    _modulesAmbientTemperatureArray[i] = receivedData[i];
                }
            }
        }
        
        private void ReadDataFromGrid(AsyncGPUReadbackRequest request)
        {
            if (request.done)
            {
                if (_colorTemperatureTexture)
                {
                    // transfer data from fluid solver to modules
                    TransferDataFromGridToModules();
                
                    // the event triggers itself upon completion
                    AsyncGPUReadback.RequestIntoNativeArray(
                        ref _transferredAmbientTemperatureDataArray,
                        _colorTemperatureTexture,
                        0,
                        AsyncGPUReadbackCallback
                    );
                }
            }
        }
        
        
        // simulate combustion process
        private void SimulateCombustionProcess()
        {
            // ITERATE THROUGH MODULES
            var modules = _moduleRenderer.orderedModuleList.ToArray();
            
            for (var i = modules.Length - 1; i >= 0; i--)
            {
                var module = modules[i];
                
                // SIMULATE DIFFUSION IN TREE HIERARCHY
                const float moduleDiffusionFactor = 0.001f;
                var connectedModule = module.neighbourModule;
                if (connectedModule)
                {
                    var middleTemperature = (module.temperature + connectedModule.temperature) / 2.0f;
                    var transferredTemperature = moduleDiffusionFactor * middleTemperature;
                    if (module.temperature > connectedModule.temperature)
                    {
                        module.temperature -= transferredTemperature;
                        connectedModule.temperature += transferredTemperature;
                    }
                    if (module.temperature < connectedModule.temperature)
                    {
                        connectedModule.temperature -= transferredTemperature;
                        module.temperature += transferredTemperature;
                    }
                }
                
                
                var transferAmbientTemperature = 0.0f;
                
                
                // SIMULATE COMBUSTION
                const float releaseTemperatureFactor = 75000.0f;
                if (module.rigidBody.mass > module.stopCombustionMass)
                {
                    var lostMass = module.CalculateLostMass();
                    if (!Mathf.Approximately(lostMass, 0))
                    {
                        var releaseTemperature = (lostMass * releaseTemperatureFactor) / 1000.0f;
                        transferAmbientTemperature = releaseTemperature;
                        module.RecalculateCharacteristics(lostMass);
                    }
                }
                
                
                // SIMULATE TEMPERATURE BALANCE BETWEEN MODULE AND AIR
                const float airToModuleTransferFactor = 0.01f;
                const float moduleToAirTransferFactor = 0.001f;
                var ambientTemperature = _modulesAmbientTemperatureArray[i];
                //Debug.Log("mod: " + modules[i].temperature * 1000.0f + " amb: " + ambientTemperature * 1000.0f);
                if (ambientTemperature > module.temperature)
                {
                    var temperatureDifference = ambientTemperature - module.temperature;
                    var transferredTemperature = airToModuleTransferFactor * temperatureDifference;

                    module.temperature += transferredTemperature;
                    transferAmbientTemperature -= transferredTemperature;
                }
                if (module.temperature > ambientTemperature)
                {
                    var temperatureDifference = module.temperature - ambientTemperature;
                    var transferredTemperature = moduleToAirTransferFactor * temperatureDifference;

                    transferAmbientTemperature += transferredTemperature;
                    module.temperature -= transferredTemperature;
                }
                
                
                _modulesUpdatedAmbientTemperatureArray[i] = transferAmbientTemperature;
            }
        }
        
        // transfer data from modules to grid
        private void TransferDataFromModulesToGrid()
        {
            _setDataBuffer.SetData(_modulesUpdatedAmbientTemperatureArray);
            computeShader.SetBuffer(_kernelWriteData, SetDataBuffer, _setDataBuffer);
            
            computeShader.Dispatch(
                _kernelWriteData,
                _moduleRenderer.modulesCount / 8,
                1,
                1
            );
        }

        private void SimulateTreesDynamics()
        {
            var modules = _moduleRenderer.orderedModuleList.ToArray();
            
            
            // SIMULATE WIND BEHAVIOUR
            computeShader.SetVector(ShaderWindDirection, wind.direction);
            computeShader.SetFloat(ShaderWindIntensity, wind.intensity / 1000.0f);
            
            ShaderDispatch(_kernelSimulateWind);
            
            // SIMULATE TREE DYNAMICS
            for (var i = modules.Length - 1; i >= 0; i--)
            {
                var module = modules[i];

                const float airDensity = 1.2f;
                
                var surfaceArea = 2.0f * Mathf.PI * module.capsuleCollider.radius * module.capsuleCollider.height;
                var windForce = wind.direction * (0.5f * airDensity * Mathf.Pow(wind.intensity, 2) * surfaceArea);
                module.rigidBody.AddForce(windForce, ForceMode.Force);
            }
            
            
            // SIMULATE FIRE BEHAVIOR
            computeShader.SetFloat(ShaderFireLiftingPower, fireLiftingPower);
            
            computeShader.Dispatch(
                _kernelSimulateFire,
                _moduleRenderer.modulesCount / 8,
                1,
                1
            );
        }

        private void SimulateFluid()
        {
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
            
            // MAIN SIMULATION
            SimulateTreesDynamics();
            SimulateCombustionProcess();
            TransferDataFromModulesToGrid();
            SimulateFluid();
        }
        
        private void CleanupPosForGetData()
        {
            if (_getDataBuffer != null)
            {
                _getDataBuffer.Release();
            }
            _getDataBuffer = null;
        }
        
        private void CleanupPosForSetData()
        {
            if (_setDataBuffer != null)
            {
                _setDataBuffer.Release();
            }
            _setDataBuffer = null;
        }
        
        private void OnDestroy()
        {
            AsyncGPUReadbackCallback -= ReadDataFromGrid;

            //_getModulesDataArray.Dispose(); // can't be deallocated
            
            _modulesAmbientTemperatureArray.Dispose();
            _modulesUpdatedAmbientTemperatureArray.Dispose();
            
            CleanupPosForGetData();
            CleanupPosForSetData();
        }
    }
}