Shader "Unlit/TreeRenderShader"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            
            struct vert_input
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float3 normal : NORMAL;
            };
            
            struct frag_input
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            struct mesh_properties
            {
                float4x4 mat;
            };

            StructuredBuffer<mesh_properties> properties;

            frag_input vert (const vert_input v, const uint instance_id : SV_InstanceID)
            {
                frag_input o;
                
                const float4 pos = mul(properties[instance_id].mat, v.vertex);
                const float3 norm = normalize(mul(properties[instance_id].mat, v.normal));
                o.vertex = UnityObjectToClipPos(pos);
                o.color = float4(norm, 1);

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