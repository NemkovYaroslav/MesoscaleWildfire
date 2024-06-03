using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using WildfireModel.Renderer;

namespace WildfireModel.Wildfire
{
    public class Wildfire : MonoBehaviour
    {
        // constants
        private const float AirDensity = 1.2f;
        
        [Header("Fluid Solver Settings")]
        [SerializeField] private ComputeShader gridComputeShader;
        public ComputeShader GridComputeShader => gridComputeShader;
        [SerializeField] private Vector3  gridResolution;
        [SerializeField] private Material gridRenderMaterial;
        
        [Header("Fluid Solver Factor Settings")]
        [SerializeField] private float diffusionFactor  = 0.001f;
        [SerializeField] private float viscosityFactor  = 0.001f;
        [SerializeField] private int   solverIterations = 15;
        
        [Header("Wildfire Factor Settings")]
        [SerializeField] private float moduleDiffusionFactor    = 0.0001f;
        [SerializeField] private float releaseTemperatureFactor = 25000.0f;
        [SerializeField] private float airTransferFactor        = 0.001f;
        [SerializeField] private float moduleTransferFactor     = 0.01f;
        
        [Header("Wind Settings")]
        [SerializeField] private Vector3 windDirection = Vector3.right;
        public Vector3 WindDirection => windDirection;
        [SerializeField] [Range(0.0f, 15.0f)] private float windIntensity = 5.0f;
        public float WindIntensity => windIntensity / 1000.0f;


        private Queue<Module> _moduleToDestroyQueue;
        private Stack<Module> _moduleToMarkStack;
        
        
        // COMMON VARIABLES
        private ModuleRenderer _moduleRenderer;
        private GameObject     _torch;
        private VisualEffect   _torchVisualEffect;
        
        
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
        private int _kernelSimulateWind;
        
        
        // FLUID SOLVER VARIABLES
        private RenderTexture _colorTemperatureTexture;
        private RenderTexture _velocityTexture;
        private RenderTexture _divergenceTexture;
        private RenderTexture _pressureTexture;
        
        
        // FLUID SOLVER VARIABLES NAMING
        private static readonly int WildfireAreaTexture                  = Shader.PropertyToID("_MainTex");
        private static readonly int ShaderTextureResolution              = Shader.PropertyToID("texture_resolution");
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
        
        
        // create render texture in 3D format
        private RenderTexture CreateRenderTexture3D(GraphicsFormat format)
        {
            var dataTexture 
                = new RenderTexture((int)gridResolution.x, (int)gridResolution.y, format, 0)
                {
                    volumeDepth       = (int)gridResolution.z,
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
            gridComputeShader.Dispatch(
                kernel,
                (int)gridResolution.x / 8,
                (int)gridResolution.y / 8,
                (int)gridResolution.z / 8
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
            gridRenderMaterial.SetTexture(WildfireAreaTexture, _colorTemperatureTexture);
            
            
            // FILL MAIN STEPS OF FLUID SOLVER
            _kernelInit                 = gridComputeShader.FindKernel("kernel_init");
            _kernelUserInput            = gridComputeShader.FindKernel("kernel_user_input");
            _kernelReadData             = gridComputeShader.FindKernel("kernel_read_data");
            _kernelWriteData            = gridComputeShader.FindKernel("kernel_write_data");
            _kernelDiffusionVelocity    = gridComputeShader.FindKernel("kernel_diffusion_velocity");
            _kernelDivergence           = gridComputeShader.FindKernel("kernel_divergence");
            _kernelPressure             = gridComputeShader.FindKernel("kernel_pressure");
            _kernelSubtractGradient     = gridComputeShader.FindKernel("kernel_subtract_gradient");
            _kernelAdvectionVelocity    = gridComputeShader.FindKernel("kernel_advection_velocity");
            _kernelAdvectionTemperature = gridComputeShader.FindKernel("kernel_advection_temperature");
            _kernelDiffusionTemperature = gridComputeShader.FindKernel("kernel_diffusion_temperature");
            _kernelSimulateWind         = gridComputeShader.FindKernel("kernel_simulate_wind");
            
            
            // SET MAIN TEXTURES TO FLUID SOLVER
            gridComputeShader.SetTexture(_kernelInit, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            gridComputeShader.SetTexture(_kernelInit, ShaderVelocityTexture, _velocityTexture);
            gridComputeShader.SetTexture(_kernelInit, ShaderDivergenceTexture, _divergenceTexture);
            gridComputeShader.SetTexture(_kernelInit, ShaderPressureTexture, _pressureTexture);
            gridComputeShader.SetTexture(_kernelUserInput, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            gridComputeShader.SetTexture(_kernelWriteData, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            gridComputeShader.SetTexture(_kernelReadData, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            gridComputeShader.SetTexture(_kernelDiffusionVelocity, ShaderVelocityTexture, _velocityTexture);
            gridComputeShader.SetTexture(_kernelDivergence, ShaderDivergenceTexture, _divergenceTexture);
            gridComputeShader.SetTexture(_kernelDivergence, ShaderPressureTexture, _pressureTexture);
            gridComputeShader.SetTexture(_kernelDivergence, ShaderVelocityTexture, _velocityTexture);
            gridComputeShader.SetTexture(_kernelPressure, ShaderDivergenceTexture, _divergenceTexture);
            gridComputeShader.SetTexture(_kernelPressure, ShaderPressureTexture, _pressureTexture);
            gridComputeShader.SetTexture(_kernelPressure, ShaderVelocityTexture, _velocityTexture);
            gridComputeShader.SetTexture(_kernelSubtractGradient, ShaderPressureTexture, _pressureTexture);
            gridComputeShader.SetTexture(_kernelSubtractGradient, ShaderVelocityTexture, _velocityTexture);
            gridComputeShader.SetTexture(_kernelAdvectionVelocity, ShaderVelocityTexture, _velocityTexture);
            gridComputeShader.SetTexture(_kernelAdvectionTemperature, ShaderVelocityTexture, _velocityTexture);
            gridComputeShader.SetTexture(_kernelAdvectionTemperature, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            gridComputeShader.SetTexture(_kernelDiffusionTemperature, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            gridComputeShader.SetTexture(_kernelSimulateWind, ShaderVelocityTexture, _velocityTexture);
            
            
            // INITIALIZE COMMON VARIABLES
            _torch             = GameObject.FindWithTag("Torch");
            _torchVisualEffect = _torch.GetComponentInChildren<VisualEffect>();
            _moduleRenderer    = GameObject.FindWithTag("ModulesRenderer").GetComponent<ModuleRenderer>();
            
            
            // SET VARIABLES FOR FLUID SOLVER
            gridComputeShader.SetVector(ShaderTextureResolution, gridResolution);
            gridComputeShader.SetInt(ModulesNumber, _moduleRenderer.modulesCount);
            
            
            // ARRAY FOR ASYNC GET DATA FROM FLUID SOLVER
            _transferredAmbientTemperatureDataArray
                = new NativeArray<Vector4>(
                    (int)gridResolution.x * (int)gridResolution.y * (int)gridResolution.z,
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
                    Allocator.Persistent
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

            
            
            _moduleToDestroyQueue = new Queue<Module>(1000);
            _moduleToMarkStack    = new Stack<Module>(10);
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
                gridComputeShader.SetTexture(_kernelReadData, TextureForSampleAmbientTemperature, _textureForSampleAmbientTemperature);
            
                // set read buffer
                gridComputeShader.SetBuffer(_kernelReadData, GetDataBuffer, _getDataBuffer);
                
                // read data from grid
                gridComputeShader.Dispatch(
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
                Profiler.BeginSample("AsyncReadData");
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
                Profiler.EndSample();
            }
        }
        
        
        // simulate combustion process
        private void ProcessMainSimulationLoop()
        {
            // SIMULATE WIND BEHAVIOUR
            gridComputeShader.SetVector(ShaderWindDirection, windDirection);
            gridComputeShader.SetFloat(ShaderWindIntensity, windIntensity / 1000.0f);
            ShaderDispatch(_kernelSimulateWind);
            
            
             // ITERATE THROUGH MODULES
            for (var i = _moduleRenderer.orderedModuleList.Count - 1; i >= 0; i--)
            {
                var module = _moduleRenderer.orderedModuleList[i];
                
                
                // SIMULATE TREE DYNAMICS
                var surfaceArea = 2.0f * Mathf.PI * module.cachedCapsuleCollider.radius * module.cachedCapsuleCollider.height;
                var windForce = windDirection * (0.5f * AirDensity * Mathf.Pow(windIntensity, 2) * surfaceArea);
                module.cachedRigidbody.AddForce(windForce, ForceMode.Force);
               
                
                // SIMULATE DIFFUSION IN TREE HIERARCHY
                var connectedModule = module.cachedPreviousModule;
                if (connectedModule)
                {
                    var middleTemperature = (module.temperature + connectedModule.temperature) / 2.0f;
                    module.temperature          += moduleDiffusionFactor * (middleTemperature - module.temperature)          * Time.fixedDeltaTime;
                    connectedModule.temperature += moduleDiffusionFactor * (middleTemperature - connectedModule.temperature) * Time.fixedDeltaTime;
                }
                
                
                var transferAmbientTemperature = 0.0f;
                
                
                // SIMULATE COMBUSTION
                if (module.temperature > 0.15f)
                {
                    if (!module.isIsolatedByCoal)
                    {
                        module.isIsolatedByCoal = true;
                    }
                    
                    var massDifference = module.cachedRigidbody.mass - module.stopCombustionMass;
                    if (massDifference > 0.0f)
                    {
                        var lostMass = module.CalculateLostMass();
                        if (!Mathf.Approximately(lostMass, 0))
                        {
                            var releaseTemperature = (lostMass * releaseTemperatureFactor) / 1000.0f;
                            transferAmbientTemperature = releaseTemperature;
                            module.RecalculateCharacteristics(lostMass);
                        }
                    }
                    /*
                    else
                    {
                        module.isBurned = true;
                    }
                    */
                    
                    /*
                    if (module.temperature > 0.25f)
                    {
                        if (!module.cachedVisualEffect.enabled)
                        {
                            module.cachedVisualEffect.enabled = true;
                        }
                    }
                    else
                    {
                        if (module.cachedVisualEffect.enabled)
                        {
                            module.cachedVisualEffect.enabled = false;
                        }
                    }
                    */
                }
                
                
                // SIMULATE TEMPERATURE BALANCE BETWEEN MODULE AND AIR
                var ambientTemperature = _modulesAmbientTemperatureArray[i];
                if (ambientTemperature > module.temperature)
                {
                    var temperatureDifference = ambientTemperature - module.temperature;
                    var transferTemperature   = moduleTransferFactor * temperatureDifference * Time.fixedDeltaTime;
                    module.temperature           += transferTemperature;
                    transferAmbientTemperature   -= transferTemperature;
                }
                else
                {
                    if (module.temperature > ambientTemperature)
                    {
                        var temperatureDifference = module.temperature - ambientTemperature;
                        var transferTemperature   = airTransferFactor * temperatureDifference * Time.fixedDeltaTime;
                        transferAmbientTemperature   += transferTemperature;
                        module.temperature           -= transferTemperature;
                    }
                }
                
                _modulesUpdatedAmbientTemperatureArray[i] = transferAmbientTemperature;
                
                // TEST
                if (module.isBurned && !module.isTrunk)
                {
                    _moduleToDestroyQueue.Enqueue(module);
                }
            }
        }
        
        // transfer data from modules to grid
        private void TransferDataFromModulesToGrid()
        {
            _setDataBuffer.SetData(_modulesUpdatedAmbientTemperatureArray);
            gridComputeShader.SetBuffer(_kernelWriteData, SetDataBuffer, _setDataBuffer);
            
            gridComputeShader.Dispatch(
                _kernelWriteData,
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
        
        
        private void DestroyModule(Module moduleToDestroy)
        {
            
        }

        private void RecursiveMarkChildren(Module moduleToMark)
        {
            _moduleToMarkStack.Push(moduleToMark);

            if (moduleToMark.cachedNextModule)
            {
                var nextModule = moduleToMark.cachedNextModule;

                while (_moduleToMarkStack.Count > 0)
                {
                    var previousModule = _moduleToMarkStack.Peek();
                    if (nextModule.cachedPreviousModule.Equals(previousModule))
                    {
                        if (!nextModule.isBurned)
                        {
                            nextModule.isBurned = true;
                        }
                        RecursiveMarkChildren(nextModule);
                    }
                    else
                    {
                        _moduleToMarkStack.Pop();
                    }
                }
            }
            else
            {
                _moduleToMarkStack.Clear();
            }
        }

        private void MarkChildrenToDestroy()
        {
            while (_moduleToDestroyQueue.Count != 0)
            {
                var module = _moduleToDestroyQueue.Dequeue();
                
                RecursiveMarkChildren(module);
            }
        }
        
        
        private void FixedUpdate()
        {
            gridComputeShader.SetFloat(ShaderDeltaTime, Time.fixedDeltaTime);
            gridComputeShader.SetFloat(ShaderDiffusionIntensity, diffusionFactor);
            gridComputeShader.SetFloat(ShaderViscosityIntensity, viscosityFactor);
            
            if (Input.GetMouseButton(0))
            {
                if (!_torchVisualEffect.enabled)
                {
                    _torchVisualEffect.enabled = true;
                }
                
                var torchPosition = transform.InverseTransformPoint(_torch.transform.position);
                gridComputeShader.SetVector(ShaderTorchPosition, torchPosition);
                
                gridComputeShader.Dispatch(
                    _kernelUserInput,
                    1,
                    1,
                    1
                );
            }
            else
            {
                if (_torchVisualEffect.enabled)
                {
                    _torchVisualEffect.enabled = false;
                }
            }
            
            // MAIN SIMULATION LOOP
            Profiler.BeginSample("Wildfire");
            ProcessMainSimulationLoop();
            TransferDataFromModulesToGrid();
            SimulateFluid();
            Profiler.EndSample();
            
            ///*
            // MARK AND DESTROY BRANCHES
            Profiler.BeginSample("Branch Destruction");
            MarkChildrenToDestroy();
            Profiler.EndSample();
            //*/
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