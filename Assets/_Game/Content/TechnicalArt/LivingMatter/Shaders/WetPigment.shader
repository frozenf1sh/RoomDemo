// 文件：WetPigment.shader。职责：真实反馈湿膜的吸色、积聚边缘与稀薄水光。
// 仅覆盖已经湿润的区域；透明混合保留木纹身份，不用一张增长的直线 Mask。
Shader "RoomDemo/LivingMatter/WetPigment"
{
    Properties { _Pigment("运行时湿膜",2D)="black"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" }
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Pigment);SAMPLER(sampler_Pigment);float4 _Pigment_TexelSize;
            struct Input { float3 positionOS:POSITION;float2 uv:TEXCOORD0; };
            struct Output { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1; };
            Output Vertex(Input i) { Output o;o.world=TransformObjectToWorld(i.positionOS);o.positionCS=TransformWorldToHClip(o.world);o.uv=i.uv;return o; }
            half4 Fragment(Output i):SV_Target
            {
                float4 film=SAMPLE_TEXTURE2D(_Pigment,sampler_Pigment,i.uv);
                float grain=frac(sin(dot(floor(i.uv*82),float2(12.9898,78.233)))*43758.5453);
                float wet=smoothstep(.019+grain*.014,.08+grain*.012,film.r);
                clip(wet-.002);
                float2 ts=_Pigment_TexelSize.xy;
                float dx=SAMPLE_TEXTURE2D(_Pigment,sampler_Pigment,i.uv+float2(ts.x*2,0)).r-film.r;
                float dz=SAMPLE_TEXTURE2D(_Pigment,sampler_Pigment,i.uv+float2(0,ts.y*2)).r-film.r;
                float3 n=normalize(float3(-dx*2.5,1,-dz*2.5));
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float shine=pow(saturate(dot(n,normalize(view+float3(-.6,1,.25)))),48);
                float border=4*wet*(1-wet);
                float concentration=saturate((film.g+film.b)/max(film.r,.005));
                float blueRatio=film.b/max(film.g+film.b,.001);
                float3 ink=lerp(float3(.011,.17,.18),float3(.026,.055,.13),blueRatio);
                float3 colour=lerp(float3(.12,.12,.075),ink,concentration*.76);
                colour*=1-border*.26;
                colour+=float3(.57,.61,.53)*(shine*.36+border*.035);
                return half4(colour,wet*lerp(.34,.78,concentration));
            }
            ENDHLSL
        }
    }
}
