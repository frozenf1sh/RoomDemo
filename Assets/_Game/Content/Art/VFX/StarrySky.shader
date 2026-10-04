// 文件：StarrySky.shader。职责：用层叠星点、月轮、银河和缓慢笔触组成完整夜空。
// 边界：原创数学图案，不复制具体画作；只服务绘画世界的天空表现。
Shader "Game/StarrySky"
{
    Properties
    {
        _Zenith ("天顶夜色", Color) = (0.015,0.025,0.068,1)
        _Horizon ("远处夜色", Color) = (0.09,0.15,0.21,1)
        _Stars ("星点亮度", Range(0,3)) = 1.45
        _Nebula ("银河笔触", Range(0,2)) = 0.9
        _Moon ("月光亮度", Range(0,3)) = 1.1
        _CloudColor ("银河色彩", Color) = (0.20,0.31,0.45,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            Cull Front ZWrite Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Zenith, _Horizon, _CloudColor;
            float _Stars, _Nebula, _Moon;
            CBUFFER_END
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.direction=v.positionOS.xyz; return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            float Fbm(float2 p)
            {
                float value=0, weight=.5;
                for(int i=0;i<4;i++) { value+=Noise(p)*weight; p=p*2.13+17.5; weight*=.5; }
                return value;
            }
            float3 StarLayer(float2 uv, float2 density, float cutoff, float radius, float brightness)
            {
                float2 grid=uv*density, cell=floor(grid), local=frac(grid);
                float seed=Hash(cell);
                float2 center=float2(Hash(cell+31),Hash(cell+83))*.7+.15;
                float distance=length(local-center), antialias=max(fwidth(distance),.008);
                float core=(1-smoothstep(radius,radius+antialias,distance));
                float halo=exp(-distance*distance/(radius*radius*18))*.14;
                float twinkle=.78+.22*sin(_Time.y*.39+seed*48);
                float3 tint=lerp(float3(.64,.80,1),float3(1,.86,.64),Hash(cell+127));
                return (core+halo)*step(cutoff,seed)*brightness*twinkle*tint;
            }
            half4 Frag(Varyings v) : SV_Target
            {
                float3 d=normalize(v.direction);
                float2 uv=float2(atan2(d.z,d.x)/6.2831853+.5,acos(clamp(d.y,-1,1))/3.1415927);
                float horizon=exp(-max(d.y,0)*3.3);
                float3 color=lerp(_Zenith.rgb,_Horizon.rgb,horizon);
                float skyFade=smoothstep(-.02,.20,d.y);
                // 银河不是均匀星点图：倾斜弧带具有暗尘、长笔触与局部彩色云气。
                float drift=_Time.y*.00028;
                float2 brushUv=float2(uv.x*7+drift,uv.y*24+sin(uv.x*11)*.36);
                float clouds=Fbm(brushUv), detail=Fbm(brushUv*2.7+9);
                float bandHeight=.385+.065*sin((uv.x-.12)*6.28318);
                float band=exp(-pow((uv.y-bandHeight)*10,2));
                float dust=Fbm(float2(uv.x*34,uv.y*67));
                float nebula=band*saturate((clouds-.31)*2.2)*(1-saturate((dust-.61)*2.7));
                float strokes=saturate(sin(uv.x*126+clouds*7+uv.y*20)*.18+.72);
                float3 nebulaTint=lerp(_CloudColor.rgb,float3(.30,.22,.43),detail);
                color+=nebulaTint*nebula*strokes*_Nebula*skyFade;
                color+=band*pow(saturate(detail-.48)*2.2,3)*float3(.16,.26,.28)*_Nebula;
                float3 stars=StarLayer(uv,float2(420,210),.946,.016,.58);
                stars+=StarLayer(uv+.173,float2(810,405),.975,.012,.31);
                stars+=StarLayer(uv+.531,float2(140,70),.976,.034,1.7);
                stars+=StarLayer(uv+.271,float2(960,480),.962,.012,.35)*band;
                color+=stars*_Stars*skyFade;
                float3 moonDirection=normalize(float3(-.31,.36,.88));
                float moonDot=dot(d,moonDirection);
                float disc=smoothstep(.99913,.99922,moonDot);
                float halo=pow(saturate(moonDot),230)*.10+pow(saturate(moonDot),1800)*.15;
                float crater=Noise(d.xz*440)*.10+Noise(d.xz*140)*.09;
                float crescent=smoothstep(-.029,.012,d.x-moonDirection.x);
                color+=disc*(float3(.88,.92,.83)-crater)*lerp(.16,1,crescent)*_Moon;
                color+=halo*float3(.28,.38,.48)*_Moon;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
