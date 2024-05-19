// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Unlit/TreeRenderShader"
{
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
                float3 normal : NORMAL;
            };
            
            struct frag_input
            {
                float4 position : SV_POSITION;
                float4 color : COLOR;
            };

            StructuredBuffer<float4x4> matrices;

            frag_input vert (const vert_input v, const uint instance_id : SV_InstanceID)
            {
                frag_input o;
                
                const float4 position = mul(matrices[instance_id], v.position);
                const float3 normal = normalize(mul(matrices[instance_id], v.normal));
                
                o.position = UnityObjectToClipPos(position);
                o.color = float4(normal, 1);
                
                return o;
            }

            fixed4 frag (frag_input i) : SV_Target
            {
                return i.color;
            }
            
            ENDCG
        }
    }
}