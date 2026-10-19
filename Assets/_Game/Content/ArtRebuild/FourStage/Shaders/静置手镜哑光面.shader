// 文件：静置手镜哑光面.shader
// 职责：给静置手镜提供双面灰色金属质感，不创建反射摄像机或采样房间。
// 边界：仅有限亮度的宽高光和细磨砂；拿起后的真实反射由专用通道覆盖。
Shader "RoomDemo/Atelier/RestingMirror"
{
    Properties
    {
        [HideInInspector] _BaseMap("深度通道白底",2D)="white"{}
        [HideInInspector] _BaseColor("深度通道颜色",Color)=(1,1,1,1)
        [HideInInspector] _Cutoff("裁切阈值",Float)=.5
        [HideInInspector] _Cull("双面剔除",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float3 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 local:TEXCOORD2; };
            V Vert(A i)
            {
                V o;
                o.world=TransformObjectToWorld(i.positionOS);
                o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(i.normalOS);
                o.local=i.positionOS.xy;
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                float facing=abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)));
                float sheen=pow(1-facing,3)*.045;
                float broad=.022*cos(i.local.x*3+i.local.y*2);
                float grain=sin(i.local.x*1300+i.local.y*13)*.0015;
                return half4(float3(.105,.118,.132)+sheen+broad+grain,1);
            }
            ENDHLSL
        }
        // 深度和法线也包含静置镜片，底稿遮罩才能以镜片自身位置恢复实色。
        UsePass "Universal Render Pipeline/Unlit/DepthOnly"
        UsePass "Universal Render Pipeline/Unlit/DepthNormalsOnly"
    }
}
