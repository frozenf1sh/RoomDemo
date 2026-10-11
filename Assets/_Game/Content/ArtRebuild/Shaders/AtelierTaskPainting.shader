// 文件：AtelierTaskPainting.shader。职责：空白亚麻、逐件静物笔触和画中水纹。
// 边界：底图由本项目实际场景重绘；没有 AI 概念图、天空图片或整张视频替换。
Shader "RoomDemo/Atelier/TaskPainting"
{
    Properties
    {
        _BaseMap("场景取材笔触画",2D)="white"{}
        _MotifMask("真实几何分色遮罩",2D)="black"{}
        _Sketch("四层落笔进度",Vector)=(0,0,0,0)
        _WaterRegion("水色位置和范围",Vector)=(.5,.2,.08,.03)
        _WaterPaint("水色落笔进度",Range(0,1))=0
        _LampRegion("画中灯芯位置和局部色域",Vector)=(.78,.4,.055,.09)
        _LampPaint("灯芯亮色进度",Range(0,1))=0
        _PaintTime("水色时间",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MotifMask); SAMPLER(sampler_MotifMask);
            float4 _WaterRegion,_LampRegion,_Sketch,_Dabs[32]; float _WaterPaint,_LampPaint,_PaintTime,_SelectedMotif,_DabCount;
            struct Attributes { float3 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; float3 normal:TEXCOORD2; float3 tangent:TEXCOORD3; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.world=TransformObjectToWorld(i.positionOS); o.positionCS=TransformWorldToHClip(o.world);
                o.uv=i.uv; o.normal=TransformObjectToWorldNormal(i.normalOS); o.tangent=TransformObjectToWorldDir(i.tangentOS.xyz); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv=i.uv;
                float3 colour=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
                float2 water=(uv-_WaterRegion.xy)/_WaterRegion.zw;
                float radius=length(water);
                float mask=1-smoothstep(.79,1.0,radius);
                float angle=atan2(water.y,water.x);
                float bristle=.5+.5*sin(uv.y*2380+sin(uv.x*86)*1.9);
                float waterArrival=saturate((water.x+1)*.48+sin(water.y*9)*.025+bristle*.035);
                float laid=smoothstep(waterArrival,waterArrival+.12,_WaterPaint*1.12);
                // 空出的水色保留暖底、铅笔椭圆和断开的干刷，不是纯白洞。
                float graphite=exp(-pow((radius-.91)*45,2))*.22;
                float3 underpaint=float3(.39,.32,.22)*(1-graphite)+colour*.15;
                float ripple=sin(radius*19+angle*1.3-_PaintTime*.5);
                float stroke=smoothstep(.45,.95,ripple)*(.55+.45*bristle);
                float3 wet=lerp(colour*.86+float3(.025,.045,.055),float3(.20,.29,.27),stroke*.48);
                colour=lerp(colour,wet,laid*mask);
                float2 lamp=(uv-_LampRegion.xy)/_LampRegion.zw;
                float lampMask=exp(-dot(lamp,lamp)*2.4);
                float lampBrush=.6+.4*sin(uv.x*2100+sin(uv.y*94)*1.4);
                float lampLaid=smoothstep(saturate(.45+lamp.y*.28+lampBrush*.08),saturate(.57+lamp.y*.28+lampBrush*.08),_LampPaint);
                // 先画静物时灯芯只留暗底；后续用短笔触添暖色，既有瓷座纹理仍保留。
                colour=lerp(colour,colour*.33+float3(.035,.023,.012),lampMask*(1-lampLaid));
                colour+=float3(.20,.085,.015)*lampMask*lampLaid;
                float4 motif=SAMPLE_TEXTURE2D(_MotifMask,sampler_MotifMask,uv);
                float2 cell=floor(uv*float2(38,54));
                float random=frac(sin(dot(cell,float2(12.9898,78.233)))*43758.5453);
                // 短笔触按交错行逐条落下，细笔毛留出颜料沟；不是整张图片透明度淡入。
                float arrival=saturate((1-uv.y)*.79+frac(cell.y*.381)*.16+random*.05);
                float4 revealed=smoothstep(arrival,arrival+.035,_Sketch*1.04);
                float coverage=saturate(dot(motif,revealed));
                float chosen=0;
                if(_SelectedMotif>=0) chosen=motif[(int)_SelectedMotif];
                float gesture=0;
                [loop] for(int d=0;d<(int)_DabCount;d++)
                {
                    float2 q=uv-_Dabs[d].xy;
                    q=float2(q.x*.91+q.y*.42,-q.x*.42+q.y*.91);
                    float width=_Dabs[d].z*(.88+.12*sin(q.x*1100+d*7));
                    float stamp=1-smoothstep(.60,1,length(q/float2(width*1.55,width*.38)));
                    stamp*=.78+.22*sin(q.y*5300+d*11);
                    gesture=max(gesture,stamp);
                }
                coverage=max(coverage,gesture*chosen);
                float3 linen=float3(.76,.70,.57);
                float fibre=sin(uv.x*1800)*sin(uv.y*1450);
                linen*=.98+fibre*.025;
                // 未画的纸保持空白；选中静物时仅有很淡的轮廓帮助定位。
                float2 pixel=float2(1.0/1024,1.0/800);
                float4 neighbour=SAMPLE_TEXTURE2D(_MotifMask,sampler_MotifMask,uv+pixel*2);
                float edge=_SelectedMotif>=0 ? abs(chosen-neighbour[(int)_SelectedMotif]) : 0;
                colour=lerp(linen*(1-edge*.15),colour,coverage);
                float weave=sin(uv.x*1800)*sin(uv.y*1450);
                float3 n=normalize(i.normal+normalize(i.tangent)*weave*.035);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=colour*(.98+weave*.014); surface.alpha=1;
                surface.smoothness=.11+laid*mask*.16; surface.normalTS=float3(0,0,1); surface.occlusion=1;
                float paintedWick=exp(-pow(lamp.x*7,2)-pow(lamp.y*3.5,2));
                surface.emission=float3(.52,.22,.042)*paintedWick*lampLaid*coverage*(.97+.03*sin(_PaintTime*3.7));
                InputData input=(InputData)0;
                input.positionWS=i.world; input.normalWS=n; input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);
                input.shadowCoord=TransformWorldToShadowCoord(i.world); input.bakedGI=SampleSH(n);
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS); input.shadowMask=half4(1,1,1,1);
                return UniversalFragmentPBR(input,surface);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
