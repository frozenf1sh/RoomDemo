// 文件：局部炭稿.shader
// 职责：保留房间原图，只在世界空间左侧物件组和窗洞之后合成稳定炭线。
// 边界：测试管线专用，不作为全屏模糊，不读写玩法状态。
Shader "Hidden/RoomDemo/TAProbe/LocalDraft"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X_FLOAT(_ProbeDepth);
            TEXTURE2D_X(_ProbeNormals);
            float4 _DraftAmounts;
            float _DraftDensity;

            float Hash(float3 p) { return frac(sin(dot(p,float3(12.9898,78.233,37.719)))*43758.5453); }
            float Box(float3 p,float3 c,float3 e,float feather)
            {
                float3 q=abs(p-c)-e;
                return 1-smoothstep(-feather,feather,max(q.x,max(q.y,q.z)));
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv=i.texcoord;
                float3 original=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv).rgb;
                float raw=SAMPLE_TEXTURE2D_X(_ProbeDepth,sampler_PointClamp,uv).r;
                float depth=raw;
                #if !UNITY_REVERSED_Z
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                #endif
                float3 world=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                float3 direction=world-_WorldSpaceCameraPos;
                float windowT=(2.18-_WorldSpaceCameraPos.z)/max(.0001,direction.z);
                float3 atWindow=_WorldSpaceCameraPos+direction*windowT;
                float2 windowEdge=abs(atWindow.xy-float2(1.35,2.08))-float2(1.46,1.04);
                float windowMask=(1-smoothstep(-.06,.06,max(windowEdge.x,windowEdge.y)))*step(2.65,world.z)*step(0,direction.z);
                // 左侧包围体止于画架前方，杯、便签和完整画布不进入此区域。
                float left=Box(world,float3(-2.45,1.10,-.40),float3(1.70,.75,1.10),.055);
                float amount=saturate(max(left*_DraftAmounts.x,windowMask*_DraftAmounts.y)*_DraftDensity);
                float luma=dot(original,float3(.2126,.7152,.0722));
                float3 normal=SampleSceneNormals(uv);
                float normalEdge=length(ddx(normal))+length(ddy(normal));
                float depthEdge=abs(ddx(LinearEyeDepth(raw,_ZBufferParams)))+abs(ddy(LinearEyeDepth(raw,_ZBufferParams)));
                float contour=saturate(normalEdge*2.6+depthEdge*2.0);
                // 炭线使用世界锚点，停镜时不逐帧随机。纸纹只在被遮区域内部。
                float3 anchor=lerp(world,atWindow,windowMask*.92);
                float grain=Hash(floor(anchor*280));
                float wobble=sin(anchor.y*11+anchor.z*13)*1.8+sin(anchor.x*17)*.6;
                float lineA=sin((anchor.x+anchor.y*.73+anchor.z*.19)*88+wobble);
                float lineB=sin((anchor.x-anchor.y*.57+anchor.z*.23)*61+wobble*.4);
                float shade=1-smoothstep(.02,.30,luma);
                float breaks=smoothstep(.2,.8,Hash(floor(anchor*27)));
                float hatch=smoothstep(.68,.98,lineA)*(.2+shade*.4)*breaks
                    +smoothstep(.86,.99,lineB)*shade*.22;
                // 夜间底稿保持灰暗纸面，不能在曝光后变成两块发光白板。
                float paper=.042+smoothstep(.0,.28,luma)*.065;
                float ink=saturate(1-hatch-contour*.70-(grain-.5)*.19);
                float3 draft=paper*ink*float3(.90,.92,.99);
                float brokenEdge=saturate(amount+(Hash(floor(anchor*60))-.5)*.10*amount*(1-amount));
                float3 colour=lerp(original,draft,brokenEdge);
                if (_DraftAmounts.z>.001)
                {
                    float3 shifted=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(1.7,-.6)/_ScreenParams.xy).rgb;
                    colour=lerp(colour,(colour+shifted)*.5,_DraftAmounts.z);
                }
                return half4(colour,1);
            }
            ENDHLSL
        }
    }
}
