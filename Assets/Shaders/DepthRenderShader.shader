Shader "Unlit/DepthRenderShader"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float _DepthRangeBottom;
            float _DepthRangeTop;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 clip_pos : SV_POSITION;
                float3 vertex : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.clip_pos = UnityObjectToClipPos(v.vertex);
                o.vertex = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float range = _DepthRangeTop - _DepthRangeBottom;
                float delta = i.vertex.y - _DepthRangeBottom;
                delta *= step(0, delta);
                float val = delta / range;
                return val;
            }
            ENDCG
        }
    }
}
