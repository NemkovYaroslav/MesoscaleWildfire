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
        private Texture3D _energyTexture3D;
        
        //private ComputeBuffer _modulePositionEnergyBuffer;

        private Module[] _modules;
        
        private int _kernelInit;
        private int _kernelUserInput;
        private int _kernelDiffusion;

        //private int _kernelModuleInfluence;
        
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        
        private static readonly int ShaderTextureResolution  = Shader.PropertyToID("texture_resolution");
        private static readonly int ShaderSourceIntensity    = Shader.PropertyToID("source_intensity");
        private static readonly int ShaderDiffusionIntensity = Shader.PropertyToID("diffusion_intensity");
        private static readonly int ShaderDeltaTime          = Shader.PropertyToID("delta_time");
        private static readonly int ShaderTorchPosition    = Shader.PropertyToID("torch_position");
        
        private static readonly int ShaderColorEnergyTexture = Shader.PropertyToID("color_energy_texture");
        private static readonly int ShaderTexelEnergyTexture = Shader.PropertyToID("texel_energy_texture");

        // transfer data from gpu to cpu
        private NativeArray<Vector4> _transferNativeArray;
        private AsyncGPUReadbackRequest _request;

        private NativeArray<Vector4> _texturePositions;
        private Matrix4x4 _wildfireZoneTransform;

        private ModuleRenderer _moduleRenderer;
        
        //private static readonly int ModulesBuffer = Shader.PropertyToID("modules_buffer");

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
            //_kernelModuleInfluence = computeShader.FindKernel("kernel_module_influence");
            
            ShaderSetTexturesData(_kernelInit);
            ShaderSetTexturesData(_kernelUserInput);
            ShaderSetTexturesData(_kernelDiffusion);
            
            //ShaderSetTexturesData(_kernelModuleInfluence);
            
            renderMaterial.SetTexture(MainTex, _colorEnergyTexture);
            
            ShaderDispatch(_kernelInit);

            _moduleRenderer = GameObject.Find("ModuleRenderer").gameObject.GetComponent<ModuleRenderer>();
            
            _modules = new Module[_moduleRenderer.modulesCount];
            for (var i = 0; i < _moduleRenderer.modulesCount; i++)
            {
                _modules[i] = _moduleRenderer.transformAccessArray[i].gameObject.GetComponent<Module>();
            }
            
            // transfer data from gpu to cpu
            _energyTexture3D = 
                new Texture3D(
                    (int)textureResolution.x,
                    (int)textureResolution.y,
                    (int)textureResolution.z,
                    GraphicsFormat.R32G32B32A32_SFloat,
                    TextureCreationFlags.None
                );
            
            _transferNativeArray 
                = new NativeArray<Vector4>(
                    (int)textureResolution.x * (int)textureResolution.y * (int)textureResolution.z,
                    Allocator.Persistent
                );

            _wildfireZoneTransform = transform.worldToLocalMatrix;
            
            AsyncGPUReadbackCallback += TransferDataFromRenderTargetToTexture3D;

            _request 
                = AsyncGPUReadback.RequestIntoNativeArray(
                    ref _transferNativeArray,
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
                ParallelReading();

                if (_texelEnergyTexture != null)
                {
                    _request
                        = AsyncGPUReadback.RequestIntoNativeArray(
                            ref _transferNativeArray,
                            _texelEnergyTexture,
                            0,
                            AsyncGPUReadbackCallback
                        );
                }
            }
        }

        private void ParallelReading()
        {
            if (_moduleRenderer != null)
            {
                if (_moduleRenderer.transformAccessArray.length > 0)
                {
                    if (_energyTexture3D != null)
                    {
                        _energyTexture3D.SetPixelData(_transferNativeArray, 0);
                        _energyTexture3D.Apply(false, false);
                        
                        _texturePositions 
                            = new NativeArray<Vector4>(
                                _moduleRenderer.modulesCount,
                                Allocator.TempJob
                            );
                    
                        var job = new ModuleWildfireJob()
                        {
                            textureResolution = textureResolution,
                            
                            wildfireZoneTransform = _wildfireZoneTransform,
                            
                            texturePositions = _texturePositions
                        };
                        var handle = job.Schedule(_moduleRenderer.transformAccessArray);
                        handle.Complete();

                        for (var i = 0; i < _moduleRenderer.modulesCount; i++)
                        {
                            var temperature 
                                = _energyTexture3D.GetPixel(
                                    (int)_texturePositions[i].x,
                                    (int)_texturePositions[i].y,
                                    (int)_texturePositions[i].z
                                ).a;
                            _modules[i].temperature = temperature;
                        }

                        _texturePositions.Dispose();
                    }
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

        /*
        private void CleanupComputeBuffer()
        {
            if (_modulePositionEnergyBuffer != null) 
            {
                _modulePositionEnergyBuffer.Release();
            }
            _modulePositionEnergyBuffer = null;
        }
        
        private void UpdateModulesData()
        {
            if (_modules.Length > 0)
            {
                var modulePositions = new Vector4[_modules.Length];
                for (var i = 0; i < _modules.Length; i++)
                {
                    if (_modules[i].TryGetComponent(out ModuleStats ms))
                    {
                        if (ms.isBurning)
                        {
                            var localAreaModulePosition = transform.InverseTransformPoint(_modules[i].transform.position);
                            modulePositions[i] = new Vector4(localAreaModulePosition.x, localAreaModulePosition.y, localAreaModulePosition.z, 0.01f);
                        }
                    }
                }

                if (modulePositions.Length > 0)
                {
                    CleanupComputeBuffer();
            
                    const int stride = sizeof(float) * 4;
                    _modulePositionEnergyBuffer = new ComputeBuffer(modulePositions.Length, stride);
                    _modulePositionEnergyBuffer.SetData(modulePositions);
                
                    computeShader.SetBuffer(_kernelModuleInfluence, ModulesBuffer, _modulePositionEnergyBuffer);
                    computeShader.Dispatch(_kernelModuleInfluence, modulePositions.Length / 10, 1, 1);
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
        }
    }
}