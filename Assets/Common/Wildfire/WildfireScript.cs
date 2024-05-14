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

        private ComputeBuffer _posTempBuffer;
        private int _posTempBufferStride;

        private Texture3D _shitTexture3D;

        private Module[] _modules;
        
        private int _kernelInit;
        private int _kernelUserInput;
        private int _kernelDiffusion;
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
        
        private static readonly int ShaderColorTemperatureTexture = Shader.PropertyToID("ColorTemperatureTexture");
        private static readonly int ShaderTexelTemperatureTexture = Shader.PropertyToID("TexelTemperatureTexture");

        private static readonly int PositionTemperatureBuffer = Shader.PropertyToID("PositionTemperatureBuffer");
        
        private static readonly int PosTempBuffer = Shader.PropertyToID("posTempBuffer");
        private static readonly int MyTexture3D = Shader.PropertyToID("myTexture3D");
        
        // transfer data from gpu to cpu
        private NativeArray<Vector4> _transferArray;

        private NativeArray<float> _modulesAmbientTemperatureArray;
        
        private ModuleRenderer _moduleRenderer;

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
            
            computeShader.SetVector(ShaderAreaScale, transform.lossyScale);
            
            _colorTemperatureTexture = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            _texelTemperatureTexture = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            
            _kernelInit             = computeShader.FindKernel("kernel_init");
            _kernelUserInput        = computeShader.FindKernel("kernel_user_input");
            _kernelDiffusion        = computeShader.FindKernel("kernel_diffusion");
            _kernelModulesInfluence = computeShader.FindKernel("kernel_modules_influence");
            
            _kernelReadData = computeShader.FindKernel("kernel_read_data");
            
            ShaderSetTexturesData(_kernelInit);
            ShaderSetTexturesData(_kernelUserInput);
            ShaderSetTexturesData(_kernelDiffusion);
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
            
            _posTempBufferStride = sizeof(float) * 4;
            
            _positionTemperatureBufferStride = sizeof(float) * 4;
            
            _shitTexture3D = new Texture3D(
                _texelTemperatureTexture.width,
                _texelTemperatureTexture.height,
                _texelTemperatureTexture.volumeDepth,
                _texelTemperatureTexture.graphicsFormat,
                TextureCreationFlags.None
            );
            // set empty texture
            computeShader.SetTexture(_kernelReadData, MyTexture3D, _shitTexture3D);
            
            // modules ambient temperature
            _modulesAmbientTemperatureArray 
                = new NativeArray<float>(
                    _moduleRenderer.modulesCount,
                    Allocator.Persistent
                );
            
            AsyncGPUReadbackCallback += TransferTextureData;
            AsyncGPUReadback.RequestIntoNativeArray(
                ref _transferArray,
                _texelTemperatureTexture,
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
                    var job = new TransferDataFromShaderJob()
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
                    _posTempBuffer = new ComputeBuffer(_moduleRenderer.modulesCount, _posTempBufferStride);
                    _posTempBuffer.SetData(positionTemperatureArray);
                    
                    computeShader.SetBuffer(_kernelReadData, PosTempBuffer, _posTempBuffer);
                    
                    _shitTexture3D.SetPixelData(_transferArray, 0);
                    _shitTexture3D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                    
                    computeShader.SetTexture(_kernelReadData, MyTexture3D, _shitTexture3D);
                    
                    computeShader.Dispatch(
                        _kernelReadData,
                        _moduleRenderer.modulesCount / 8,
                        1,
                        1
                    );
                    
                    var temp = new Vector4[_moduleRenderer.modulesCount];
                    _posTempBuffer.GetData(temp);

                    for (var i = 0; i < temp.Length; i++)
                    {
                        _modulesAmbientTemperatureArray[i] = temp[i].w;
                        
                        //Debug.Log("info: " + temp[i]);
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
                if (_texelTemperatureTexture != null)
                {
                    TransferDataFromShader();
                    
                    AsyncGPUReadback.RequestIntoNativeArray(
                        ref _transferArray,
                        _texelTemperatureTexture,
                        0,
                        AsyncGPUReadbackCallback
                    );
                }
            }
        }
        
        private void CleanupPosTempBuffer()
        {
            if (_posTempBuffer != null)
            {
                _posTempBuffer.Release();
            }
            _posTempBuffer = null;
        }
        
        private void CleanupPositionTemperatureBuffer()
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

                    // input
                    var releaseTemperatureArray = new NativeArray<float>(
                        _moduleRenderer.modulesCount,
                        Allocator.TempJob
                    );

                    //const float airThermalConductivity = 0.025f; // W / (m * C)
                    const float airThermalCapacity = 1000.0f; // J / (kg * С)
                    //const float woodThermalConductivity = 0.25f; // W / (m * C)
                    const float woodThermalCapacity = 2000.0f; // J / (kg * С)
                    //const float thickness = 0.01f;
                    
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
                                //_modules[i].temperature += ambientTemperature;
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
                                
                                var air = heat / (airThermalCapacity * Time.fixedDeltaTime * 1.2f) / 1000.0f;

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
                    var job = new TransferDataFromModulesToGridJob()
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
                    _positionTemperatureBuffer = new ComputeBuffer(_moduleRenderer.modulesCount, _positionTemperatureBufferStride);
                    _positionTemperatureBuffer.SetData(positionTemperatureArray);
                    
                    computeShader.SetBuffer(_kernelModulesInfluence, PositionTemperatureBuffer, _positionTemperatureBuffer);
                    
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
            
            // transfer data from modules to grid
            TransferDataFromModulesToGrid();
            
            ShaderDispatch(_kernelReadData);
            
            if (Input.GetMouseButton(0))
            {
                computeShader.Dispatch(
                    _kernelUserInput,
                    1,
                    1,
                    1
                );
            }
            
            for (var i = 0; i < solverIterations; i++)
            {
                ShaderDispatch(_kernelDiffusion);
            }
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