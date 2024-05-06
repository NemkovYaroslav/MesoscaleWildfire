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
        
        private RenderTexture _colorEnergyTexture;
        private RenderTexture _texelEnergyTexture;
        
        private ComputeBuffer _positionEnergyBuffer;

        private Module[] _modules;
        
        private int _kernelInit;
        private int _kernelUserInput;
        private int _kernelDiffusion;

        private int _kernelModulesInfluence;
        
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        
        private static readonly int ShaderTextureResolution  = Shader.PropertyToID("texture_resolution");
        private static readonly int ShaderSourceIntensity    = Shader.PropertyToID("source_intensity");
        private static readonly int ShaderDiffusionIntensity = Shader.PropertyToID("diffusion_intensity");
        private static readonly int ShaderDeltaTime          = Shader.PropertyToID("delta_time");
        private static readonly int ShaderTorchPosition      = Shader.PropertyToID("torch_position");
        
        private static readonly int ShaderColorEnergyTexture = Shader.PropertyToID("color_energy_texture");
        private static readonly int ShaderTexelEnergyTexture = Shader.PropertyToID("texel_energy_texture");

        private static readonly int PositionEnergyBuffer = Shader.PropertyToID("position_energy_buffer");
        
        // transfer data from gpu to cpu
        private NativeArray<Vector4> _transferArray;
        private NativeArray<Vector4> _transferTempArray;
        private AsyncGPUReadbackRequest _request;
        
        private Matrix4x4 _wildfireZoneTransform;

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
            computeShader.SetTexture(kernel, ShaderColorEnergyTexture, _colorEnergyTexture);
            computeShader.SetTexture(kernel, ShaderTexelEnergyTexture, _texelEnergyTexture);
        }

        private void Start()
        {
            computeShader.SetVector(ShaderTextureResolution, textureResolution);
            
            _colorEnergyTexture = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            _texelEnergyTexture = CreateRenderTexture3D(GraphicsFormat.R32G32B32A32_SFloat);
            
            _kernelInit      = computeShader.FindKernel("kernel_init");
            _kernelUserInput = computeShader.FindKernel("kernel_user_input");
            _kernelDiffusion = computeShader.FindKernel("kernel_diffusion");
            _kernelModulesInfluence = computeShader.FindKernel("kernel_modules_influence");
            
            ShaderSetTexturesData(_kernelInit);
            ShaderSetTexturesData(_kernelUserInput);
            ShaderSetTexturesData(_kernelDiffusion);
            
            ShaderSetTexturesData(_kernelModulesInfluence);
            
            renderMaterial.SetTexture(MainTex, _colorEnergyTexture);
            
            ShaderDispatch(_kernelInit);

            _moduleRenderer = GameObject.Find("ModuleRenderer").gameObject.GetComponent<ModuleRenderer>();
            
            _modules = new Module[_moduleRenderer.modulesCount];
            for (var i = 0; i < _moduleRenderer.modulesCount; i++)
            {
                _modules[i] = _moduleRenderer.transformsArray[i].gameObject.GetComponent<Module>();
            }
            
            // transfer data from gpu to cpu
            _transferArray 
                = new NativeArray<Vector4>(
                    (int)textureResolution.x * (int)textureResolution.y * (int)textureResolution.z,
                    Allocator.Persistent
                );
            // additional array for transfer
            _transferTempArray
                = new NativeArray<Vector4>(
                    (int)textureResolution.x * (int)textureResolution.y * (int)textureResolution.z,
                    Allocator.Persistent
                );

            _wildfireZoneTransform = transform.worldToLocalMatrix;
            
            AsyncGPUReadbackCallback += TransferDataFromRenderTargetToTexture3D;

            _request 
                = AsyncGPUReadback.RequestIntoNativeArray(
                    ref _transferArray,
                    _texelEnergyTexture,
                    0,
                    AsyncGPUReadbackCallback
                );
        }
        
        
        private static event Action<AsyncGPUReadbackRequest> AsyncGPUReadbackCallback;
        
        private void TransferDataFromRenderTargetToTexture3D(AsyncGPUReadbackRequest request)
        {
            if (request.done && !request.hasError)
            {
                // transfer data from grid to modules
                TransferDataFromGridToModules();

                if (_texelEnergyTexture != null)
                {
                    _request = AsyncGPUReadback.RequestIntoNativeArray(
                        ref _transferArray,
                        _texelEnergyTexture,
                        0,
                        AsyncGPUReadbackCallback
                    );
                }
            }
        }

        // transfer data from grid to modules
        private void TransferDataFromGridToModules()
        {
            if (_moduleRenderer != null)
            {
                if (_moduleRenderer.transformsArray.length > 0)
                {
                    // input
                    _transferTempArray.CopyFrom(_transferArray);
                    
                    // output
                    var temperatureArray = new NativeArray<float>(
                        _moduleRenderer.modulesCount,
                        Allocator.TempJob
                    );
                
                    // job process from GPU to CPU
                    var job = new TransferDataFromGridToModulesJob()
                    {
                        textureResolution = textureResolution,
                        wildfireZoneTransform = _wildfireZoneTransform,
                        textureArray = _transferTempArray,
                        temperatureArray = temperatureArray
                    };
                    var handle = job.Schedule(_moduleRenderer.transformsArray);
                    handle.Complete();
                    
                    // transfer data from grid to modules
                    for (var i = 0; i < _moduleRenderer.modulesCount; i++)
                    {
                        _modules[i].temperature = temperatureArray[i];
                    }
                    
                    temperatureArray.Dispose();
                }
            }
        }
        
        private void CleanupComputeBuffer()
        {
            if (_positionEnergyBuffer != null) 
            {
                _positionEnergyBuffer.Release();
            }
            _positionEnergyBuffer = null;
        }

        // transfer data from modules to grid
        private void TransferDataFromModulesToGrid()
        {
            if (_moduleRenderer != null)
            {
                if (_moduleRenderer.transformsArray.length > 0)
                {
                    // output
                    var positionEnergyArray = new NativeArray<Vector4>(
                        _moduleRenderer.modulesCount,
                        Allocator.TempJob
                    );
                    
                    // job process from CPU to GPU
                    var job = new TransferDataFromModulesToGridJob()
                    {
                        centers = _moduleRenderer.centers,
                        wildfireZoneTransform = _wildfireZoneTransform,
                        positionEnergyArray = positionEnergyArray
                    };
                    var handle = job.Schedule(_moduleRenderer.transformsArray);
                    handle.Complete();

                    CleanupComputeBuffer();
                    
                    const int stride = sizeof(float) * 4;
                    _positionEnergyBuffer = new ComputeBuffer(_moduleRenderer.modulesCount, stride);
                    _positionEnergyBuffer.SetData(positionEnergyArray);
                
                    computeShader.SetBuffer(_kernelModulesInfluence, PositionEnergyBuffer, _positionEnergyBuffer);
                    computeShader.Dispatch(_kernelModulesInfluence, _moduleRenderer.modulesCount / 1, 1, 1);

                    positionEnergyArray.Dispose();
                }
            }
        }

        /*
        private Vector3 TransformPositionFromWorldSpaceToTextureSpace(Vector3 position)
        {
            var textureSpace = transform.InverseTransformPoint(position);
            
            textureSpace += new Vector3(0.5f, 0.5f, 0.5f);
            
            textureSpace.x *= textureResolution.x;
            textureSpace.y *= textureResolution.y;
            textureSpace.z *= textureResolution.z;
            
            textureSpace -= new Vector3(0.5f, 0.5f, 0.5f);

            return textureSpace;
        }

        private void TransferEnergyFromTexture3DToModules()
        {
            foreach (var module in _modules)
            {
                var moduleTexturePosition 
                    = TransformPositionFromWorldSpaceToTextureSpace(module.transform.position);
                
                var energyContent
                    = _energyTexture3D.GetPixel((int)moduleTexturePosition.x, (int)moduleTexturePosition.y, (int)moduleTexturePosition.z);
                
                //Debug.Log("pos: " + moduleTexturePosition + " energy: " + energyContent);
                
                
                if (module.TryGetComponent(out ModuleStats ms))
                {
                    if (!ms.isBurning)
                    {
                        ms.woodHeatCapacity -= energyContent.a;
                        if (ms.woodHeatCapacity < 0)
                        {
                            ms.isBurning = true;
                        }
                    }
                    else
                    {
                        ms.woodStrength -= energyContent.a;
                        if (ms.woodStrength < 0 && ms.fixedJoint != null)
                        {
                            ms.fixedJoint.breakForce = 1;
                        }
                    }
                }
            }
        }
        */

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
            }
            
            for (var i = 0; i < solverIterations; i++)
            {
               ShaderDispatch(_kernelDiffusion);
            }
        }
        
        private void OnDestroy()
        {
            AsyncGPUReadbackCallback -= TransferDataFromRenderTargetToTexture3D;

            if (_request.done || _request.hasError)
            {
                _transferArray.Dispose();
            }
            _transferTempArray.Dispose();
        }
    }
}