// 画内的普通风景：四层静止颜料，所有变化都被留在矩形画布里。
Shader "Game/CanvasLandscape"
{
    Properties { _CanvasLayers("枝叶、距离、海、夜空", Vector) = (0,0,0,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _CanvasLayers;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(A a) { V v; v.positionCS=TransformObjectToHClip(a.positionOS.xyz); v.uv=a.uv; return v; }
            float Grain(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            half4 Frag(V v):SV_Target
            {
                // 朝向房间的立方体负 Z 面反转纵向 UV，天空必须在画的上方。
                float2 p=float2(v.uv.x,1-v.uv.y);
                float grain=Grain(floor(p*float2(290,370)));
                float3 paper=float3(.78,.70,.55)+(.5-grain)*.025;
                float3 sky=lerp(float3(.77,.72,.57),float3(.31,.53,.62),saturate(p.y));
                float3 c=lerp(paper,sky,_CanvasLayers.y);
                float night=smoothstep(.42,.72,p.y)*_CanvasLayers.w;
                c=lerp(c,lerp(float3(.16,.25,.39),float3(.08,.14,.29),p.y),night);
                float mountains=.40+.055*sin(p.x*11)+.028*sin(p.x*27);
                c=lerp(c,float3(.40,.46,.46),smoothstep(mountains+.014,mountains-.009,p.y)*_CanvasLayers.y);
                float bank=smoothstep(.30,.295,p.y);
                c=lerp(c,float3(.48,.51,.33),bank*max(_CanvasLayers.y,_CanvasLayers.x));
                float sea=smoothstep(.34,.33,p.y)*_CanvasLayers.z;
                float brush=.5+.5*sin(p.y*155+sin(p.x*20)*.8);
                c=lerp(c,lerp(float3(.16,.36,.46),float3(.39,.59,.61),brush*.36+p.y),sea);
                // 轮廓、树干和短笔触全部静止；服从路线不把画变成另一个窗口。
                float2 tree=(p-float2(.20,.41))/float2(.16,.22);
                float canopy=smoothstep(1.02,.92,length(tree))*(.82+.18*sin(p.y*115+p.x*61));
                float trunk=smoothstep(.012,.008,abs(p.x-.205))*smoothstep(.16,.17,p.y)*smoothstep(.53,.52,p.y);
                c=lerp(c,float3(.32,.27,.18),trunk*_CanvasLayers.x);
                c=lerp(c,lerp(float3(.22,.33,.24),float3(.39,.47,.31),grain),canopy*_CanvasLayers.x);
                float2 cell=floor(p*float2(44,57)); float star=step(.987,Grain(cell));
                star*=1-smoothstep(.02,.09,length(frac(p*float2(44,57))-.5));
                c=lerp(c,float3(.94,.86,.65),star*night);
                float moon=1-smoothstep(.019,.022,length(p-float2(.78,.82)));
                c=lerp(c,float3(.88,.82,.64),moon*_CanvasLayers.w);
                float weave=(sin(p.x*870)*sin(p.y*1110))*.011;
                c+=weave;
                Light light=GetMainLight();
                c*=.72+light.color*.26;
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}
