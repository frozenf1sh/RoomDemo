// 文件：AtelierWetTrial.shader。职责：有厚薄差的湿颜料在擦笔布下被带走，留下木纹和残色。
Shader "RoomDemo/Atelier/WetTrial"
{
    Properties { _Wipe("接触擦除",Range(0,1))=0 _AtelierEraseGroup("擦除表面组",Float)=0 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest+10"}
        Cull Off
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "AtelierErasureField.hlsl"
            float _Wipe;
            struct A {float3 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;};
            V Vert(A a) {V o;o.world=TransformObjectToWorld(a.positionOS);o.positionCS=TransformWorldToHClip(o.world);o.uv=a.uv;return o;}
            half4 Frag(V i):SV_Target
            {
                AtelierClip(i.world);
                float2 p=(i.uv-.5)*float2(1,2.7);
                float grain=sin(i.uv.x*377+sin(i.uv.y*41))*sin(i.uv.y*511)*.035;
                float shape=1-length(p)*1.9+grain+sin(i.uv.y*157)*.04;
                float cleared=saturate(_Wipe*1.23-i.uv.x+grain);
                clip(shape-.08-cleared*2.4);
                half3 col=lerp(half3(.035,.095,.16),half3(.09,.18,.25),saturate(shape));
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                col*=SampleSH(float3(0,1,0))+light.color*saturate(light.direction.y)*light.shadowAttenuation;
                float3 halfDir=normalize(GetWorldSpaceNormalizeViewDir(i.world)+light.direction);
                col+=pow(saturate(halfDir.y),100)*light.color*.22;
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
