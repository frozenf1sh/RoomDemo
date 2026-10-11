// 文件：AtelierMotifID.shader。职责：Editor 按真实深度生成静物分色，不用于玩家画面。
Shader "Hidden/RoomDemo/Atelier/MotifID"
{
    Properties { _IDColour("分色",Color)=(0,0,0,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _IDColour;
            CBUFFER_END
            struct V {float4 position:SV_POSITION;};
            V Vert(float3 p:POSITION) { V o; o.position=TransformObjectToHClip(p); return o; }
            float4 Frag(V i):SV_Target {return _IDColour;}
            ENDHLSL
        }
    }
}
