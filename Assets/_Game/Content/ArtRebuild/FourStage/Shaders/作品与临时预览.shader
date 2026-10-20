// 文件：作品与临时预览.shader。职责：在实体画架上显示唯一作品RT与不入账预览。
// 边界：不改变原始图样，不参与颜料或阶段判定。
Shader "RoomDemo/Atelier/CanvasWorkspace"
{
    Properties {
        _AtelierEraseGroup("擦除组",Float)=0
 _BaseMap("真实作品",2D)="black"{} _Brush("预览图样",2D)="black"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../../Shaders/AtelierErasureField.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Brush);SAMPLER(sampler_Brush);
            float _Preview,_PreviewValid;float4 _Placement,_StampScale,_AlphaRect;
            struct A {float3 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
            V Vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS);o.uv=i.uv;o.world=TransformObjectToWorld(i.positionOS);return o;}
            half4 Frag(V i):SV_Target
            {
                AtelierClip(i.world);
                half3 colour=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                float2 p=i.uv-_Placement.xy;
                float c=cos(_Placement.w),s=sin(_Placement.w);
                float2 uv=float2(c*p.x+s*p.y,-s*p.x+c*p.y)/max(float2(.001,.001),_StampScale.xy)+.5;
                if(_Preview>.5&&all(uv>=0)&&all(uv<=1))
                {
                    half4 brush=SAMPLE_TEXTURE2D(_Brush,sampler_Brush,_AlphaRect.xy+uv*_AlphaRect.zw);
                    colour=lerp(colour,lerp(float3(.55,.10,.08),brush.rgb,_PreviewValid),brush.a*.42);
                }
                return half4(colour,1);
            }
            ENDHLSL
        }
    }
}
