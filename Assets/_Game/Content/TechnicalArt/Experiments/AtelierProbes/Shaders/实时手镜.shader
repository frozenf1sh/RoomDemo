// 文件：实时手镜.shader。职责：采样真实镜像摄像机，验证转镜的透视和倒像关系。
// 边界：无静态反射贴图，不修改原镜框或游戏事实。
Shader "RoomDemo/TAProbe/RealMirror"
{
    Properties { _ProbeReflection("实时镜像",2D)="black"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_ProbeReflection); SAMPLER(sampler_ProbeReflection);
            struct A { float3 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; float4 mirror:TEXCOORD0; };
            V Vert(A i) { V o; float3 p=TransformObjectToWorld(i.positionOS); o.positionCS=TransformWorldToHClip(p); // 镜像平面上的点不随反射矩阵移动；两镜头复制相同投影，直接用主画幅投影采样。
                o.mirror=ComputeScreenPos(o.positionCS); return o; }
            half4 Frag(V i):SV_Target
            {
                float2 uv=i.mirror.xy/max(.0001,i.mirror.w);
                return half4(SAMPLE_TEXTURE2D(_ProbeReflection,sampler_ProbeReflection,uv).rgb*.94+float3(.008,.012,.018),1);
            }
            ENDHLSL
        }
    }
}
