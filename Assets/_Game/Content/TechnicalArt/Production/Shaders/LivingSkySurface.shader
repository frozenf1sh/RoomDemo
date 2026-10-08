// 文件：LivingSkySurface.shader。职责：把已通过的星夜画面接到世界方向与局部揭示遮罩。
// 边界：笔触与星光函数逐字复用；仅增加空间映射、覆盖率和普通天空混合。
Shader "RoomDemo/Production/LivingSkySurface"
{
    Properties
    {
        _Pigment("运行时颜料",2D)="black"{}
        _Aspect("颜料场比例",Float)=1.7777778
        _Mode("颜料模式",Float)=0
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
            #include "../../LivingMatter/Shaders/PigmentField.hlsl"
            TEXTURE2D(_Pigment);SAMPLER(sampler_Pigment);float4 _Pigment_TexelSize;
            #include "ApprovedSkyPaint.hlsl"
            float _Coverage,_Opacity,_Feather,_SkyYaw,_SkyElevation,_SkySpan;
            float4 _RevealDirection;
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
                float longitude=atan2(direction.x,direction.z)-_SkyYaw;
                float2 uv=float2(frac(longitude/_SkySpan+.5),frac((asin(clamp(direction.y,-1,1))-_SkyElevation)/(_SkySpan/1.7777778)+.5));
                float angle=acos(clamp(dot(direction,normalize(_RevealDirection.xyz)),-1,1));
                // 边缘只依赖世界方向与固定颜料细节，不随屏幕位置重抽噪声。
                float rough=(Noise(direction.xz*45+direction.yy*7)-.5)*.032;
                float radius=_Coverage*3.14159265;
                float mask=1-smoothstep(max(0,radius-_Feather),radius+_Feather,angle+rough);
                if(_Coverage>.999) mask=1;
                half4 painted=SeamlessPaint(uv);
                // 球极以同一暗蓝安静区域收束，消除经度在极点汇合造成的星点拉丝。
                painted.rgb=lerp(float3(.014,.022,.065),painted.rgb,1-smoothstep(.985,1,abs(direction.y)));
                return half4(painted.rgb,mask*_Opacity);
            }
            ENDHLSL
        }
    }
}
