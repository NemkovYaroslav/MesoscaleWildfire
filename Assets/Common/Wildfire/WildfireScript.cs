using System;
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
        public ComputeShader computeShader;
        [SerializeField] private Vector3 textureResolution;
        [SerializeField] private Material renderMaterial;
        
        [Header("Torch Settings")]
        [SerializeField] private GameObject torch;
        
        [Header("Solver Settings")]
        [SerializeField] private float sourceIntensity;
        [SerializeField] private float diffusionIntensity;
        [SerializeField] private int solverIterations;
        
        // render textures
        private RenderTexture _colorTemperatureTexture;
        
        private ComputeBuffer _posForSetData;
        private int _posForSetDataStride;

        private ComputeBuffer _posForGetData;
        private int _posForGetDataStride;

        private Texture3D _textureForSampleTemperature;

        private Module[] _modules;
        
        private int _kernelInit;
        private int _kernelUserInput;
        private int _kernelDiffusionTemperature;
        private int _kernelModulesInfluence;

        private int _kernelReadData;
        
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        
        private static readonly int ShaderTextureResolution  = Shader.PropertyToID("texture_resolution");
        private static readonly int ShaderAreaScale  = Shader.PropertyToID("area_scale");
        
        private static readonly int ShaderSourceIntensity    = Shader.PropertyToID("source_intensity");
        private static readonly int ShaderDiffusionIntensity = Shader.PropertyToID("diffusion_intensity");
        private static readonly int ShaderDeltaTime          = Shader.PropertyToID("delta_time");
        private static readonly int ShaderTorchPosition      = Shader.PropertyToID("torch_position");
        private static readonly int ModulesNumber            = Shader.PropertyToID("modules_number");
        
        private static readonly int ShaderColorTemperatureTexture = Shader.PropertyToID("color_temperature_texture");
        
        private static readonly int PosForGetDataBuffer = Shader.PropertyToID("pos_for_get_data");
        private static readonly int TextureForSampleTemperature = Shader.PropertyToID("texture_for_sample_temperature");
        
        private static readonly int PosForSetDataBuffer = Shader.PropertyToID("pos_for_set_data");
        
        // transfer data from gpu to cpu
        private NativeArray<Vector4> _transferArray;

        private NativeArray<float> _modulesAmbientTemperatureArray;
        
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
        
        
        /*
        // WIND SHIT

        private RenderTexture _velocityTexture;
        private int _kernelAdvection;
        private static readonly int ShaderVelocityTexture = Shader.PropertyToID("velocity_texture");

        private RenderTexture _divergenceTexture;
        private int _kernelDivergence;
        private static readonly int ShaderDivergenceTexture = Shader.PropertyToID("divergence_texture");
        
        private RenderTexture _pressureTexture;
        private int _kernelPressure;
        private static readonly int ShaderPressureTexture = Shader.PropertyToID("pressure_texture");

        private int _kernelSubtractGradient;
        */
        

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
            computeShader.SetTexture(_kernelAdvectionVelocity, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            
            
            /*
            // WIND SHIT
            _velocityTexture = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            _kernelAdvection = computeShader.FindKernel("kernel_advection");
            computeShader.SetTexture(_kernelInit, ShaderVelocityTexture, _velocityTexture);
            computeShader.SetTexture(_kernelAdvection, ShaderColorTemperatureTexture, _colorTemperatureTexture);
            computeShader.SetTexture(_kernelAdvection, ShaderVelocityTexture, _velocityTexture);

            _divergenceTexture = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            _kernelDivergence = computeShader.FindKernel("kernel_divergence");
            computeShader.SetTexture(_kernelInit, ShaderDivergenceTexture, _divergenceTexture);
            computeShader.SetTexture(_kernelDivergence, ShaderDivergenceTexture, _divergenceTexture);
            computeShader.SetTexture(_kernelDivergence, ShaderVelocityTexture, _velocityTexture);
            
            _pressureTexture = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            _kernelPressure = computeShader.FindKernel("kernel_pressure");
            computeShader.SetTexture(_kernelInit, ShaderPressureTexture, _pressureTexture);
            computeShader.SetTexture(_kernelPressure, ShaderPressureTexture, _pressureTexture);
            computeShader.SetTexture(_kernelPressure, ShaderDivergenceTexture, _divergenceTexture);
            
            _kernelSubtractGradient = computeShader.FindKernel("kernel_subtract_gradient");
            computeShader.SetTexture(_kernelSubtractGradient, ShaderPressureTexture, _pressureTexture);
            computeShader.SetTexture(_kernelSubtractGradient, ShaderVelocityTexture, _velocityTexture);
            */
            
            
            
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
            // set empty texture
            computeShader.SetTexture(_kernelReadData, TextureForSampleTemperature, _textureForSampleTemperature);
            
            // modules ambient temperature
            _modulesAmbientTemperatureArray 
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

                    CleanupPosTempBuffer();
                    _posForGetData = new ComputeBuffer(_moduleRenderer.modulesCount, _posForGetDataStride);
                    _posForGetData.SetData(positionTemperatureArray);
                    
                    computeShader.SetBuffer(_kernelReadData, PosForGetDataBuffer, _posForGetData);
                    
                    _textureForSampleTemperature.SetPixelData(_transferArray, 0);
                    _textureForSampleTemperature.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                    
                    computeShader.SetTexture(_kernelReadData, TextureForSampleTemperature, _textureForSampleTemperature);
                    
                    computeShader.Dispatch(
                        _kernelReadData,
                        _moduleRenderer.modulesCount / 8,
                        1,
                        1
                    );
                    
                    var temp = new Vector4[_moduleRenderer.modulesCount];
                    _posForGetData.GetData(temp);

                    for (var i = 0; i < temp.Length; i++)
                    {
                        _modulesAmbientTemperatureArray[i] = temp[i].w;
                        
                        Debug.Log("info: " + temp[i]);
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
        
        private void CleanupPosTempBuffer()
        {
            if (_posForGetData != null)
            {
                _posForGetData.Release();
            }
            _posForGetData = null;
        }
        
        private void CleanupPositionTemperatureBuffer()
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
            if (_moduleRenderer != null)
            {
                if (_moduleRenderer.transformsArray.length > 0)
                {
                    // MAIN SIMULATION LOGIC

                    // input
                    var releaseTemperatureArray = new NativeArray<float>(
                        _moduleRenderer.modulesCount,
                        Allocator.TempJob
                    );
                    
                    const float airThermalCapacity = 1000.0f; // J / (kg * С)
                    const float woodThermalCapacity = 2000.0f; // J / (kg * С)
                    
                    for (var i = 0; i < _moduleRenderer.modulesCount; i++)
                    {
                        var ambientTemperature = _modulesAmbientTemperatureArray[i];
                    
                        // temperature to transfer to grid
                        var transferTemperature = 0.0f;
                        
                        if (_modules[i].rigidBody.mass > _modules[i].stopCombustionMass)
                        {
                            if (_modules[i].temperature > 0.25f && !_modules[i].isSelfSupported)
                            {
                                _modules[i].isSelfSupported = true;
                            }
                            
                            if (!_modules[i].isSelfSupported)
                            {
                                _modules[i].temperature = ambientTemperature;
                            }
                            
                            var lostMass = _modules[i].CalculateLostMass();

                            if (!Mathf.Approximately(lostMass, 0.0f))
                            {
                                _modules[i].RecalculateCharacteristics(lostMass);

                                var heat = 10000.0f * lostMass;

                                var areaScale = transform.lossyScale;
                                var airVolume 
                                    = (areaScale.x / textureResolution.x) 
                                      * (areaScale.y / textureResolution.y) 
                                        * (areaScale.z / textureResolution.z);
                                
                                var air = heat / (airThermalCapacity * Time.fixedDeltaTime * airVolume * 1.2f) / 1000.0f;

                                if (_modules[i].isSelfSupported)
                                {
                                    var wood = heat / (woodThermalCapacity * Time.fixedDeltaTime * _modules[i].rigidBody.mass) / 1000.0f;
                                    _modules[i].temperature += wood;
                                }
                                
                                transferTemperature += air;
                            }
                            
                            //Debug.Log("mod: " + _modules[i].temperature * 1000.0f + " amb: " + ambientTemperature * 1000.0f);
                        }

                        releaseTemperatureArray[i] = transferTemperature;
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
                        releaseTemperatureArray = releaseTemperatureArray,
                        
                        // result
                        positionTemperatureArray = positionTemperatureArray
                    };
                    var handle = job.Schedule(_moduleRenderer.transformsArray);
                    handle.Complete();

                    CleanupPositionTemperatureBuffer();
                    _posForSetData = new ComputeBuffer(_moduleRenderer.modulesCount, _posForSetDataStride);
                    _posForSetData.SetData(positionTemperatureArray);
                    
                    computeShader.SetBuffer(_kernelModulesInfluence, PosForSetDataBuffer, _posForSetData);
                    
                    computeShader.Dispatch(
                        _kernelModulesInfluence,
                        _moduleRenderer.modulesCount / 8,
                        1,
                        1
                    );
                    
                    positionTemperatureArray.Dispose();
                    releaseTemperatureArray.Dispose();
                }
            }
        }

        private void FixedUpdate()
        {
            computeShader.SetFloat(ShaderDeltaTime, Time.fixedDeltaTime);
            computeShader.SetFloat(ShaderSourceIntensity, sourceIntensity);
            computeShader.SetFloat(ShaderDiffusionIntensity, diffusionIntensity);

            var torchPosition = transform.InverseTransformPoint(torch.transform.position);
            computeShader.SetVector(ShaderTorchPosition, torchPosition);

            /*
            // WIND SHIT
            ShaderDispatch(_kernelAdvection);
            */
            
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
            
            // ANOTHER SHIT
            
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
            
            // DIFFUSE TEMPERATURE
            for (var i = 0; i < solverIterations; i++)
            {
                ShaderDispatch(_kernelDiffusionTemperature);
            }
            
            ShaderDispatch(_kernelReadData);
        }
        
        private void OnDestroy()
        {
            AsyncGPUReadbackCallback -= TransferTextureData;

            _modulesAmbientTemperatureArray.Dispose();
            
            CleanupPosTempBuffer();
            CleanupPositionTemperatureBuffer();
        }
    }
}