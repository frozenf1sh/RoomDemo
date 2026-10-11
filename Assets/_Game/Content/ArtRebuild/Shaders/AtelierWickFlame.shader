// 文件：AtelierWickFlame.shader。职责：锚定灯芯的三维火苗、局部热色与柔软笔触边缘。
// 边界：无静态火图、Billboard 或全屏光晕；时间只控制热流，不产生世界事实。
Shader "RoomDemo/Atelier/WickFlame"
{
    Properties
    {
        _Burn("燃烧表现",Range(0,1))=0
        _FlameClock("热流时间",Float)=0
        _Height("实体火苗高度",Float)=.035
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Burn,_FlameClock,_Height;
            struct Attributes { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 world:TEXCOORD1; float2 uv:TEXCOORD2; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                float y=saturate(i.uv.y);
                float3 p=i.positionOS;
                // 底端固定在灯芯，只弯曲上部热流；没有整体火苗平移或缩放。
                p.x+=y*y*(sin(_FlameClock*3.1+y*8)*.0015+sin(_FlameClock*5.4+y*15)*.0006)*_Burn;
                p.z+=y*y*sin(_FlameClock*2.9+y*11)*.0009*_Burn;
                o.world=TransformObjectToWorld(p);
                o.positionCS=TransformWorldToHClip(o.world);
                o.normalWS=TransformObjectToWorldNormal(i.normalOS);
                o.uv=i.uv;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float y=i.uv.y;
                float grain=.5+.5*sin(i.uv.x*94+y*51-_FlameClock*4);
                float face=saturate(dot(normalize(i.normalWS),normalize(GetWorldSpaceViewDir(i.world))));
                float edge=smoothstep(.02,.37,face);
                float core=pow(face,3)*(1-smoothstep(.42,.91,y));
                float tip=1-smoothstep(.78,1,y+grain*.018);
                float alpha=edge*tip*_Burn*lerp(.70,.93,grain);
                float3 colour=lerp(float3(1.75,.28,.025),float3(3.2,2.05,.65),core);
                colour=lerp(float3(.15,.24,.55),colour,smoothstep(.015,.12,y));
                return half4(colour*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
