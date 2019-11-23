Shader "Sprites Compute"
{
    Properties
    {
        _BaseColor("Main Color", Color) = (1,1,1,1)
        _MainTex("Texture", 2D) = "white" {}
        _TransitionTex("Transition Texture", 2D) = "transparent" {}
        _Cutoff("Cutoff", Range(0, 1)) = 1
        _TileOffset("Tile Offset", Vector) = (1,1,0,0)
        _Scale("Mesh Scale", Vector) = (1,1,1)
        _Pivot("Pivot", Vector) = (0,0,0)
    }
        SubShader
        {
            Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
            LOD 100
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Pass
            {             
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma multi_compile_local __ USE_COMPUTE
                #pragma multi_compile_instancing
                #pragma instancing_options procedural:setup
                #include "UnityCG.cginc"

                void setup() {}

                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct v2f
                {
                    float2 uv1 : TEXCOORD0;
                    fixed2 uv2 : TEXCOORD1;
                    float4 vertex : SV_POSITION;
                    fixed4 color : COLOR0;
                    float cutoff : CUTOFF;
                };

                float f16tof32(uint x)
                {
                    const uint shifted_exp = (0x7c00 << 13);
                    uint uf = (x & 0x7fff) << 13;
                    uint e = uf & shifted_exp;
                    uf += (127 - 15) << 23;
                    uf += lerp(0, (128u - 16u) << 23, e == shifted_exp);
                    uf = lerp(uf, asuint(asfloat(uf + (1 << 23)) - 6.10351563e-05f), e == 0);
                    uf |= (x & 0x8000) << 16;
                    return asfloat(uf);
                }

                float2 uintToFloat2(uint input) {
                    return float2(f16tof32(input & 0x0000FFFF), f16tof32((input & 0xFFFF0000) >> 16));
                }

                float4 uint2ToFloat4(uint2 input) {
                    float2 xy = uintToFloat2(input.x);
                    float2 zw = uintToFloat2(input.y);
                    return float4(xy.x, xy.y, zw.x, zw.y);
                }

                float4x4 uint4x2ToFloat4x4(uint4x2 input) {
                    float4 c0 = uint2ToFloat4(float2(input[0].x, input[1].x));
                    float4 c2 = uint2ToFloat4(float2(input[0].y, input[1].y));
                    float4 c1 = uint2ToFloat4(float2(input[2].x, input[3].x));
                    float4 c3 = uint2ToFloat4(float2(input[2].y, input[3].y));
                    return float4x4(
                        c0.x, c1.x, c2.x, c3.x,
                        c0.y, c1.y, c2.y, c3.y,
                        c0.z, c1.z, c2.z, c3.z,
                        c0.w, c1.w, c2.w, c3.w);
                }

                sampler2D _MainTex;
                float4 _MainTex_ST;
                float4 _TransitionTex_ST;
                sampler2D _TransitionTex;

                float4 _TileOffset;
                float3 _Scale;
                float2 _Pivot;
                fixed4 _BaseColor;
                float _Cutoff;

                // uint is 32 bit and is filled with half2 (2 * 16 bit)
                StructuredBuffer<uint4x2> localToWorldBuffer; // half4x4
                StructuredBuffer<uint2> tileOffsetBuffer; // half4
                StructuredBuffer<uint2> scaleBuffer; // half4
                StructuredBuffer<uint> pivotBuffer; // half2
                StructuredBuffer<uint2> colorBuffer; // half4
                StructuredBuffer<uint> cutoffBuffer; // half

                v2f vert(appdata v, uint instanceID : SV_InstanceID)
                {
                    v2f o;
#ifdef USE_COMPUTE
                    float3 scale = uint2ToFloat4(scaleBuffer[instanceID]);
                    float2 pivot = uintToFloat2(pivotBuffer[instanceID]);
                    float4 tileOffset = uint2ToFloat4(tileOffsetBuffer[instanceID]);
                    float4x4 localToWorld = uint4x2ToFloat4x4(localToWorldBuffer[instanceID]);
                    float4 color = uint2ToFloat4(colorBuffer[instanceID]);
                    float cutoff = cutoffBuffer[instanceID];
#else
                    float3 scale = _Scale;
                    float2 pivot = _Pivot;
                    float4 tileOffset = _TileOffset;
                    float4x4 localToWorld = float4x4(
                        1, 0, 0, 0,
                        0, 1, 0, 0,
                        0, 0, 1, 0,
                        0, 0, 0, 1);
                    float4 color = _BaseColor;
                    float cutoff = _Cutoff;
#endif
                    float4x4 scaleMatrix = float4x4(
                        scale.x, 0, 0, 0,
                        0, scale.y, 0, 0,
                        0, 0, scale.z, 0,
                        0, 0, 0, 1);
                    float4 localVertexPos = mul(scaleMatrix, v.vertex) + mul(scaleMatrix, float4(pivot.x, pivot.y, 0, 0));
                    float4 localTranslated = mul(localToWorld, localVertexPos);
                    o.vertex = UnityObjectToClipPos(localTranslated);
                    o.uv1 = TRANSFORM_TEX(v.uv * tileOffset.xy + tileOffset.zw, _MainTex);
                    o.uv2 = TRANSFORM_TEX(v.uv, _TransitionTex);
                    o.color = color;
                    o.cutoff = cutoff;
                    return o;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    float4 transit = tex2D(_TransitionTex, i.uv2);
                    fixed4 col = tex2D(_MainTex, i.uv1) * i.color;
                    fixed alpha = max(transit.a < 1, i.cutoff);              
                    col.a = step(transit.b, alpha) * col.a;
                    return col;                    
                }
                ENDCG
            }
        }
}