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

            StructuredBuffer<float4x4> transforms;

            frag_input vert (const vert_input v, const uint instance_id : SV_InstanceID)
            {
                frag_input o;
                
                const float4 pos = mul(transforms[instance_id], v.position);
                const float3 norm = normalize(mul(transforms[instance_id], v.normal));
                o.position = UnityObjectToClipPos(pos);
                o.color = float4(norm, 1);

                //o.position = UnityObjectToClipPos(v.position);
                //o.color = float4(v.normal, 1);
                
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