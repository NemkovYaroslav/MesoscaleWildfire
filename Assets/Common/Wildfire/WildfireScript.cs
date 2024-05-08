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
        private RenderTexture _texelTemperatureTexture;
        
        private ComputeBuffer _positionTemperatureBuffer;
        private int _positionTemperatureBufferStride;

        private Module[] _modules;
        
        private int _kernelInit;
        private int _kernelUserInput;
        private int _kernelDiffusion;
        private int _kernelModulesInfluence;

        private float _generatedTemperature;
        
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        
        private static readonly int ShaderTextureResolution  = Shader.PropertyToID("texture_resolution");
        private static readonly int ShaderSourceIntensity    = Shader.PropertyToID("source_intensity");
        private static readonly int ShaderDiffusionIntensity = Shader.PropertyToID("diffusion_intensity");
        private static readonly int ShaderDeltaTime          = Shader.PropertyToID("delta_time");
        private static readonly int ShaderTorchPosition      = Shader.PropertyToID("torch_position");
        private static readonly int ModulesNumber            = Shader.PropertyToID("modules_number");
        
        private static readonly int ShaderColorTemperatureTexture = Shader.PropertyToID("color_temperature_texture");
        private static readonly int ShaderTexelTemperatureTexture = Shader.PropertyToID("texel_temperature_texture");

        private static readonly int PositionTemperatureBuffer = Shader.PropertyToID("position_temperature_buffer");
        
        // transfer data from gpu to cpu
        private NativeArray<Vector4> _transferArray;
        private NativeArray<Vector4> _transferTempArray;
        private AsyncGPUReadbackRequest _request;

        private NativeArray<float> _modulesAmbientTemperatureArray;
        
        private ModuleRenderer _moduleRenderer;
        
        private Matrix4x4 _wildfireAreaTransform;

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
            computeShader.SetTexture(kernel, ShaderTexelTemperatureTexture, _texelTemperatureTexture);
        }

        private void Start()
        {
            computeShader.SetVector(ShaderTextureResolution, textureResolution);
            
            _colorTemperatureTexture = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            _texelTemperatureTexture = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            
            _kernelInit             = computeShader.FindKernel("kernel_init");
            _kernelUserInput        = computeShader.FindKernel("kernel_user_input");
            _kernelDiffusion        = computeShader.FindKernel("kernel_diffusion");
            _kernelModulesInfluence = computeShader.FindKernel("kernel_modules_influence");
            
            ShaderSetTexturesData(_kernelInit);
            ShaderSetTexturesData(_kernelUserInput);
            ShaderSetTexturesData(_kernelDiffusion);
            ShaderSetTexturesData(_kernelModulesInfluence);
            
            renderMaterial.SetTexture(MainTex, _colorTemperatureTexture);
            
            ShaderDispatch(_kernelInit);

            _wildfireAreaTransform = transform.worldToLocalMatrix;

            _positionTemperatureBufferStride = sizeof(float) * 4;
            
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
                    (int)textureResolution.x * (int)textureResolution.y * (int)textureResolution.z,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            _transferTempArray
                = new NativeArray<Vector4>(
                    (int)textureResolution.x * (int)textureResolution.y * (int)textureResolution.z,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            
            // modules ambient temperature
            _modulesAmbientTemperatureArray
                = new NativeArray<float>(
                    _moduleRenderer.modulesCount,
                    Allocator.Persistent
                );
            
            AsyncGPUReadbackCallback += TransferTextureData;

            _request 
                = AsyncGPUReadback.RequestIntoNativeArray(
                    ref _transferArray,
                    _texelTemperatureTexture,
                    0,
                    AsyncGPUReadbackCallback
                );
        }
        
        private static event Action<AsyncGPUReadbackRequest> AsyncGPUReadbackCallback;
        
        // transfer data from grid to modules
        private void TransferDataFromGrid()
        {
            if (_moduleRenderer != null)
            {
                if (_moduleRenderer.transformsArray.length > 0)
                {
                    // input
                    _transferTempArray.CopyFrom(_transferArray);
                
                    // fill the output temperature array with data from shader
                    var job = new TransferDataFromGridToModulesJob()
                    {
                        // unchangeable
                        textureResolution = textureResolution,
                        wildfireAreaTransform = _wildfireAreaTransform,
                        
                        // changeable (shader data)
                        textureArray = _transferTempArray,
                        
                        // result
                        modulesAmbientTemperatureArray = _modulesAmbientTemperatureArray
                    };
                    var handle = job.Schedule(_moduleRenderer.transformsArray);
                    handle.Complete();
                }
            }
        }
        
        private void TransferTextureData(AsyncGPUReadbackRequest request)
        {
            if (request.done && !request.hasError)
            {
                // transfer data from grid to modules
                TransferDataFromGrid();

                if (_texelTemperatureTexture != null)
                {
                    _request = AsyncGPUReadback.RequestIntoNativeArray(
                        ref _transferArray,
                        _texelTemperatureTexture,
                        0,
                        AsyncGPUReadbackCallback
                    );
                }
            }
        }
        
        private void CleanupComputeBuffer()
        {
            if (_positionTemperatureBuffer != null) 
            {
                _positionTemperatureBuffer.Release();
            }
            _positionTemperatureBuffer = null;
        }

        // transfer data from modules to grid
        private void TransferDataFromModulesToGrid()
        {
            if (_moduleRenderer != null)
            {
                if (_moduleRenderer.transformsArray.length > 0)
                {
                    // MAIN SIMULATION LOGIC
                    var ambientTemperature = _modulesAmbientTemperatureArray[0];
                    Debug.Log("ambient Temperature: " + ambientTemperature * 1000.0f);

                    // temperature to transfer to grid
                    var transferTemperature = 0.0f;
                    
                    var temperatureDelta = ambientTemperature * Time.fixedDeltaTime;
                    if (_modules[0].temperature < ambientTemperature)
                    {
                        Debug.Log("less");
                        _modules[0].temperature += temperatureDelta;
                        transferTemperature -= temperatureDelta;
                    }
                    else
                    {
                        if (_modules[0].temperature > ambientTemperature)
                        {
                            Debug.Log("more");
                            _modules[0].temperature -= temperatureDelta;
                            transferTemperature += temperatureDelta;
                        }
                    }
                    
                    Debug.Log("module temperature: " + _modules[0].temperature * 1000.0f);
                    Debug.Log("transfer temperature: " + transferTemperature * 1000.0f);
                    
                    // input
                    var releaseTemperatureArray = new NativeArray<float>(
                        _moduleRenderer.modulesCount,
                        Allocator.TempJob
                    );

                    releaseTemperatureArray[0] = transferTemperature;
                    
                    // output
                    var positionTemperatureArray = new NativeArray<Vector4>(
                        _moduleRenderer.modulesCount,
                        Allocator.TempJob,
                        NativeArrayOptions.UninitializedMemory
                    );
                    
                    // job process
                    var job = new TransferDataFromModulesToGridJob()
                    {
                        centers = _moduleRenderer.centers,
                        wildfireZoneTransform = _wildfireAreaTransform,
                        
                        // changeable
                        releaseTemperatureArray = releaseTemperatureArray,
                        
                        // result
                        positionTemperatureArray = positionTemperatureArray
                    };
                    var handle = job.Schedule(_moduleRenderer.transformsArray);
                    handle.Complete();

                    CleanupComputeBuffer();
                    _positionTemperatureBuffer = new ComputeBuffer(_moduleRenderer.modulesCount, _positionTemperatureBufferStride);
                    _positionTemperatureBuffer.SetData(positionTemperatureArray);
                    
                    computeShader.SetBuffer(_kernelModulesInfluence, PositionTemperatureBuffer, _positionTemperatureBuffer);
                    ShaderDispatch(_kernelModulesInfluence);
                    
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
            
            // transfer data from modules to grid
            TransferDataFromModulesToGrid();
            
            if (Input.GetMouseButton(0))
            {
                ShaderDispatch(_kernelUserInput);
                _generatedTemperature += sourceIntensity;
                Debug.Log("generated: " + _generatedTemperature * 1000.0f);
            }
            
            for (var i = 0; i < solverIterations; i++)
            {
               ShaderDispatch(_kernelDiffusion);
            }
        }
        
        private void OnDestroy()
        {
            AsyncGPUReadbackCallback -= TransferTextureData;
            
            //_transferArray.Dispose();
            
            _transferTempArray.Dispose();

            _modulesAmbientTemperatureArray.Dispose();
            
            CleanupComputeBuffer();
        }
    }
}