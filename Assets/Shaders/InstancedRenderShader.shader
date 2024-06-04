Shader "Unlit/InstancedRenderShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            
            struct vert_input
            {
                float4 position : POSITION;
                float2 uv       : TEXCOORD0;
            };
            
            struct frag_input
            {
                float4 position  : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float2 status    : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            StructuredBuffer<float4x4> matrices;
            StructuredBuffer<float> isolated_trees;

            frag_input vert(const vert_input v, const uint instance_id : SV_InstanceID)
            {
                frag_input o;

                const float status = isolated_trees[instance_id];
                if (status > 0 && status < 1)
                {
                    o.position = 0;
                }
                else
                {
                    o.status.x = status;
                    const float4 position = mul(matrices[instance_id], v.position);
                    o.position = UnityObjectToClipPos(position);
                    o.uv       = TRANSFORM_TEX(v.uv, _MainTex);
                }
                
                return o;
            }

            fixed4 frag(frag_input i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                if (i.status.x)
                {
                    col *= half4(0.05f, 0.05f, 0.05f, 0.05f);
                }
                return col;
            }
            
            ENDCG
        }
    }
}