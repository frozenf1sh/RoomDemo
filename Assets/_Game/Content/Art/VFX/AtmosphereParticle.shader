// 文件：AtmosphereParticle.shader。职责：柔软笔触火焰、微光与烟尘的低成本粒子表面。
// 边界：原创程序化形状；颜色、生命期和数量由具体 ParticleSystem 决定。
Shader "Game/AtmosphereParticle"
{
    Properties
    {
        _Tint ("粒子色调", Color) = (1,1,1,1)
        _Mode ("形状：0光点 1火焰 2烟", Float) = 0
        _Softness ("柔边", Range(0.1,3)) = 1.1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint; float _Mode, _Softness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; o.color=v.color*_Tint; return o; }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            half4 Frag(Varyings v) : SV_Target
            {
                float2 p=v.uv*2-1;
                float radial=saturate(1-dot(p,p));
                float alpha=pow(radial,_Softness);
                if(_Mode>.5 && _Mode<1.5)
                {
                    // 手绘火舌的横向扭曲；拉伸 billboard 随生命期上升，轮廓持续变化。
                    p.x+=sin(v.uv.y*8-_Time.y*7)*.11+sin(v.uv.y*15+_Time.y*5)*.045;
                    float width=lerp(.8,.10,v.uv.y);
                    alpha=pow(saturate(1-abs(p.x)/width),1.6)*sin(v.uv.y*3.14159);
                    alpha*=smoothstep(0,.16,v.uv.y)*(1-smoothstep(.82,1,v.uv.y));
                }
                if(_Mode>1.5) alpha*=.45+.35*sin(v.uv.x*14+v.uv.y*9+_Time.y*.5);
                return half4(v.color.rgb,v.color.a*alpha);
            }
            ENDHLSL
        }
    }
}
