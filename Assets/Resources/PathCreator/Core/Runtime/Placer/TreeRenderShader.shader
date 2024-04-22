Shader "Unlit/TreeRenderShader"
{
    Properties
    {
        // входные данные, определенные пользователем
        //_MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            // CGPROGRAM - ENDCG - код шейдера
            CGPROGRAM

            // сообщаем компилятору какая функция является вершинный и фрагментным шейдерами
            #pragma vertex vert
            #pragma fragment frag

            // множество полезных встроенных функций
            #include "UnityCG.cginc"

            //sampler2D _MainTex;
            //float4 _MainTex_ST;

            float4 _Color;

            // данные меша для каждой вершины
            // автоматически заполняется движком
            struct vert_input
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                //float2 uv : TEXCOORD0;
            };

            // данные из вершинного шейдера, которые мы хотим передать в фрагментный шейдер
            // по-другому называют интерполяторами
            struct frag_input
            {
                float4 vertex : SV_POSITION; // clip space position of each vertex
                float3 normal : EXCOORD0;
                //float2 uv : TEXCOORD0;
            };

            frag_input vert (const vert_input v)
            {
                frag_input o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = v.normal;
                //o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (frag_input i) : SV_Target
            {
                // sample the texture
                //fixed4 col = tex2D(_MainTex, i.uv);
                //return col;

                return float4(i.normal, 1);
            }
            
            ENDCG
        }
    }
}
