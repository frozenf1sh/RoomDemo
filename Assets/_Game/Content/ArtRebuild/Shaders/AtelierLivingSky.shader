// 文件：AtelierLivingSky.shader。职责：以保角世界投影保留原 TA 旋涡、笔触和暖星光晕。
// 边界：复用原 ApprovedSkyPaint 与运行时 GPU 颜料，不添加静态星图，不修改原生产件。
Shader "RoomDemo/ArtRebuild/AtelierLivingSky"
{
    Properties
    {
        _ProjectionForward("笔触构图世界方向",Vector)=(0,0.39,0.92,0)
        _ProjectionScale("笔触投影范围",Vector)=(1.46,2.6,0,0)
        _CloudTint("夜间颜料明度",Color)=(0.28,0.37,0.65,1)
        _ProjectionOffset("涡旋构图中心",Vector)=(0.39,0.47,0,0)
        _ProjectionRotation("涡旋构图转角",Float)=20
        _Pigment("运行时颜料",2D)="black"{}
        _Aspect("颜料场比例",Float)=1.7777778
        _Mode("颜料模式",Float)=0
        _StarGain("星光强度",Range(0,1))=1
        _CloudPaint("云层绘画强度",Range(0,1))=1
        _Coverage("天空覆盖",Range(0,1))=0
        _Opacity("天空显现",Range(0,1))=1
        _Feather("边缘柔和度",Range(.01,.3))=.07
        _RevealDirection("世界揭示方向",Vector)=(0,.4,1,0)
        _SkyYaw("天空朝向弧度",Float)=0
        _SkyElevation("天空纬度中心",Float)=0
        _SkySpan("单幅颜料场角宽",Float)=1.5707963
    }
    SubShader
    {
        // 普通 Skybox 之后绘制，并使用最远深度；始终被房间几何遮挡。
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Front
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../../TechnicalArt/LivingMatter/Shaders/PigmentField.hlsl"
            TEXTURE2D(_Pigment);SAMPLER(sampler_Pigment);float4 _Pigment_TexelSize;
            float _StarGain;
            float3 SkyStarLayer(float3 colour,float2 uv);
            #include "../../TechnicalArt/Production/Shaders/ApprovedSkyPaint.hlsl"
            #include "../../TechnicalArt/Production/Shaders/OrdinarySky.hlsl"
            float _Coverage,_Opacity,_Feather,_SkyYaw,_SkyElevation,_SkySpan,_CloudPaint;
            float4 _RevealDirection;
            float4 _ProjectionForward,_ProjectionScale,_CloudTint;
            float4 _ProjectionOffset; float _ProjectionRotation;
            struct Input { float3 positionOS:POSITION; };
            struct Output { float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0; };
            Output Vertex(Input i)
            {
                Output o;
                o.positionCS=TransformObjectToHClip(i.positionOS);
                o.direction=TransformObjectToWorldDir(i.positionOS);
                #if UNITY_REVERSED_Z
                    o.positionCS.z=0;
                #else
                    o.positionCS.z=o.positionCS.w;
                #endif
                return o;
            }
            half4 SeamlessRow(float2 uv,float blendX)
            {
                half4 paint=LivingSkyPaint(uv);
                // 只在球面瓦片边缘混合同一实时颜料场；中央构图与已通过函数保持不变。
                [branch] if(blendX>.0001)
                    paint=lerp(paint,LivingSkyPaint(float2(frac(uv.x+.5),uv.y)),blendX);
                return paint;
            }
            half4 SeamlessPaint(float2 uv)
            {
                float2 edge=smoothstep(.34,.47,abs(uv-.5));
                half4 paint=SeamlessRow(uv,edge.x);
                [branch] if(edge.y>.0001)
                    paint=lerp(paint,SeamlessRow(float2(uv.x,frac(uv.y+.5)),edge.x),edge.y);
                return paint;
            }
            half4 Fragment(Output i):SV_Target
            {
                clip(_Coverage-.0001);
                float3 direction=normalize(i.direction);
                // 以固定世界方向做保角投影，屋顶不经过球极，经纬拉伸不会拉成长条。
                float3 forward=normalize(_ProjectionForward.xyz);
                float3 right=float3(1,0,0),up=normalize(cross(forward,right));
                float denominator=max(.25,1+dot(direction,forward));
                float2 chart=float2(dot(direction,right),dot(direction,up))/denominator;
                float2 q=chart*_ProjectionScale.xy*float2(1.7777778,1);
                float rotation=radians(_ProjectionRotation);
                q=float2(q.x*cos(rotation)-q.y*sin(rotation),q.x*sin(rotation)+q.y*cos(rotation));
                float2 uv=_ProjectionOffset.xy+q/float2(1.7777778,1);
                float2 safeUV=clamp(uv,.015,.985);
                float angle=acos(clamp(dot(direction,normalize(_RevealDirection.xyz)),-1,1));
                // 边缘只依赖世界方向与固定颜料细节，不随屏幕位置重抽噪声。
                float rough=(Noise(direction.xz*45+direction.yy*7)-.5)*.032;
                float radius=_Coverage*3.14159265;
                float mask=1-smoothstep(max(0,radius-_Feather),radius+_Feather,angle+rough);
                if(_Coverage>.999) mask=1;
                half4 painted=LivingSkyPaint(safeUV);
                // 压低冷色底层在 Linear 管线中的显示明度，保留原暖色大星的 HDR 峰值。
                float starPeak=smoothstep(.35,1.05,max(painted.r,max(painted.g,painted.b)));
                starPeak=max(starPeak,saturate((painted.r-painted.b)*5));
                painted.rgb*=lerp(_CloudTint.rgb,float3(1,1,1),starPeak);
                float chartEdge=1-smoothstep(.43,.52,max(abs(uv.x-.5),abs(uv.y-.5)));
                painted.rgb=lerp(float3(.009,.015,.044),painted.rgb,chartEdge);
                // 单独点亮保留同一片暮云；流动才把云带改写成 TA 的颜料笔势。
                if(_CloudPaint<.999) painted.rgb=lerp(SkyStarLayer(OrdinarySkyColour(direction),uv),painted.rgb,_CloudPaint);
                return half4(painted.rgb,mask*_Opacity);
            }
            ENDHLSL
        }
    }
}
