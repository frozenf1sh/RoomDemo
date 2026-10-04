// 文件：PaintingSea.shader。职责：让静止颜料连续变成有地平线、深度和浪纹的海。
// 边界：所有参数属于视觉过渡；Shader 不决定领域事实，也不模拟真实流体。
Shader "RoomDemo/PaintingSea"
{
    Properties
    {
        _Transition("海的过渡", Range(0,1)) = 0
        _Distortion("颜料扰动", Range(0,.1)) = .025
        _WaveStrength("浪纹强度", Range(0,1)) = .25
        _WaveSpeed("浪纹速度", Float) = .6
        _Wetness("边缘湿润", Range(0,1)) = 0
        _Emission("海光强度", Range(0,1)) = .10
        _EdgeBlend("画布边界", Range(0,.2)) = .07
        _CanvasMode("画布或窗外", Range(0,1)) = 1
        _DeepColor("深海颜色", Color) = (.025,.16,.20,1)
        _LightColor("浅海颜色", Color) = (.10,.49,.49,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Transition, _Distortion, _WaveStrength, _WaveSpeed;
                float _Wetness, _Emission, _EdgeBlend, _CanvasMode;
                half4 _DeepColor, _LightColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Unity 立方体朝向房间的负 Z 面 UV 上下相反，统一为天空在上。
                float2 uv = float2(input.uv.x, 1-input.uv.y);
                // 过渡为零时完全静止；第一阶段只扰动颜料，随后海逐渐覆盖原画。
                float t = _Time.y * _WaveSpeed;
                float motion = smoothstep(0,.45,_Transition);
                uv += float2(sin(uv.y*22+t),sin(uv.x*17-t*.7)) * _Distortion * motion;
                float grain = Noise(uv*180) * .035;
                half3 paintSky = lerp(half3(.64,.59,.45),half3(.84,.76,.58),uv.y);
                half3 farSky = lerp(half3(.58,.70,.66),half3(.29,.46,.51),uv.y);
                half3 dry = lerp(farSky,paintSky,_CanvasMode);
                float hill = .40 + sin(uv.x*5.3)*.05 + Noise(uv*3)*.045;
                half3 land = lerp(half3(.22,.36,.26),half3(.37,.29,.22),_CanvasMode);
                dry = lerp(dry,land,1-smoothstep(hill-.01,hill+.01,uv.y));
                float sun = 1-smoothstep(.061,.074,length(uv-float2(.32,.71)));
                dry = lerp(dry,half3(.89,.75,.48),sun);
                dry += grain;
                // 透视压缩浪纹：靠近地平线更密，前景更宽，建立画中空间深度。
                float depth = saturate((.52-uv.y)/.52);
                float frequency = lerp(95,19,pow(depth,.55));
                float wave = sin(uv.y*frequency + sin(uv.x*11+t*.3)*1.5 - t*(.7+depth));
                float swell = sin(uv.x*7+uv.y*16-t*.4);
                half3 water = lerp(_DeepColor.rgb,_LightColor.rgb,saturate(.35+wave*.17+swell*.13+depth*.15));
                float patches = smoothstep(.48,.75,Noise(float2(uv.x*28+t*.2,uv.y*39-t*.15)));
                float foam = smoothstep(.91,.998,wave) * patches * _WaveStrength;
                water = lerp(water,half3(.70,.85,.79),foam);
                // 窄地平线、天空层次与微弱反光避免整幅画只是一张蓝色纹理。
                half3 sky = lerp(half3(.69,.79,.72),half3(.29,.47,.53),uv.y);
                sky += Noise(uv*float2(6,18)+float2(t*.01,0))*.03;
                half3 sea = lerp(water,sky,smoothstep(.515,.525,uv.y));
                sea += half3(.10,.20,.19) * exp(-abs(uv.y-.52)*160);
                float reflection = exp(-abs(uv.x-.32)*24)*depth*(.02+foam*.18);
                sea += reflection + _Emission*.15;
                // 低频颜料遮罩使连续侵入有不规则边界，而非一次颜色切换。
                float coverage = saturate(_Transition*1.35 - Noise(input.uv*4)*.35);
                half3 color = lerp(dry,sea,coverage);
                float edge = min(min(input.uv.x,1-input.uv.x),min(input.uv.y,1-input.uv.y));
                float boundary = 1-smoothstep(0,max(.001,_EdgeBlend),edge);
                color *= 1-boundary*_Wetness*.28;
                color += grain*(1-_Transition*.6);
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
    }
}
