// 文件：落下的湿颜料.shader。职责：表现有限湿色落点、下落滴液的蓝色渐变与湿亮高光。
// 边界：不模拟流体，不拥有操作或解释状态；斑块外沿由同一支着色器裁出，不依赖网格边。
Shader "RoomDemo/Atelier/WetDeposit"
{
    Properties
    {
        _BaseColor("湿颜料",Color)=(.085,.12,.23,1)
        _Puddle("成滩模式",Float)=1
        _PuddleHalfSize("滩面半尺寸",Vector)=(.5,.5,0,0)
        _AtelierEraseGroup("擦除组",Float)=0
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "../../Shaders/AtelierErasureField.hlsl"
    #include "AtelierPaintLook.hlsl"
    float4 _BaseColor,_PuddleHalfSize;
    float _Puddle;
    struct A {float3 positionOS:POSITION;float3 normalOS:NORMAL;};
    struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float3 local:TEXCOORD2;};
    V Vert(A i){V o;o.local=i.positionOS;o.world=TransformObjectToWorld(i.positionOS);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);return o;}
    // 湿色滩面：归一化到自身半尺寸的椭圆，再压出不规则边和一圈表面张力亮边。
    float PuddleBlob(float3 local, float3 world, out float rim, out float inside)
    {
        float2 halfSize=max(_PuddleHalfSize.xy,float2(.001,.001));
        float2 unit=local.xz/halfSize;
        // 每个落点按世界位置错开形状；同一个落点刷新后仍保持同一块边缘。
        float seed=dot(world.xz,float2(3.7,1.3))+length(halfSize)*7.3;
        inside=PaintBlob(unit,.86,seed,.055);
        // 湿色不描亮边；只让最外圈自然压暗，湿亮交给面上的反光。
        rim=0;
        return inside;
    }
    half4 Frag(V i):SV_Target
    {
        AtelierClip(i.world);
        Light light=GetMainLight();
        float3 n=normalize(i.normal);
        float3 v=normalize(GetCameraPositionWS()-i.world);
        float gloss=pow(saturate(dot(n,normalize(v+light.direction))),42);
        if(_Puddle>.5)
        {
            float rim,inside;
            clip(PuddleBlob(i.local,i.world,rim,inside));
            float2 metres=i.local.xz;
            float3 colour=PaintBluePalette(i.world.xz,metres,rim,1);
            float2 unit=i.local.xz/max(_PuddleHalfSize.xy,float2(.001,.001));
            float radial=saturate(length(unit));
            // 滩心更深、薄边更通透，避免整块蓝纸一样平。
            colour*=lerp(.68,1.08,saturate(radial));
            // 窗光在湿面上摊成一大块冷反光，油灯在近边留下一点暖反光。
            float windowReflect=1-smoothstep(.0,.70,length((unit-float2(-.12,.26))*float2(.72,1.16)));
            float lampReflect=1-smoothstep(.0,.24,length((unit-float2(.28,-.32))*float2(1.25,1.7)));
            colour+=windowReflect*float3(.085,.175,.235);
            colour+=lampReflect*float3(.230,.135,.045)*.55;
            // 湿面细波只改变一点点法线，让反光有厚薄而不是一块色纸。
            float2 ripple=float2(PaintNoise(metres*17+float2(1.7,5.2)),PaintNoise(metres*16+float2(-4.3,2.8)))-.5;
            float3 wetNormal=normalize(n+float3(ripple.x*.18,0,ripple.y*.18));
            float sheen=pow(saturate(dot(wetNormal,normalize(float3(-.22,.78,.38)))),9);
            colour+=sheen*float3(.095,.180,.250)*.70;
            // 表面张力只留断续的一线湿光，不做通圈描边。
            float lipNoise=PaintNoise(metres*55+float2(2.3,7.1));
            float lip=(1-smoothstep(0,.010,abs(inside-.006)))*(.18+.82*lipNoise);
            colour+=lip*float3(.022,.062,.105)*(.30+.70*saturate(gloss*1.4));
            // 最外一圈压暗，湿色才贴在木面上而不是浮着；只留一线被光擦亮的湿边。
            colour*=1-.55*(1-smoothstep(0,.020,max(0,inside)));
            float fresnel=pow(1-saturate(dot(n,v)),4);
            colour+=float3(.035,.090,.155)*(fresnel*.55+gloss*.20);
            // 油灯和墙面把夜间湿色托起来，避免落点变成几乎看不见的黑斑。
            float3 fill=SampleSH(n)+light.color*max(saturate(dot(n,light.direction)),.25)+float3(.24,.22,.19);
            return half4(colour*max(fill,float3(.30,.31,.35)),1);
        }
        // 下落过程只是一小滴液珠：按球面高度做明暗，不套用滩面裁边。
        float3 drop=PaintBluePalette(i.world.xz*.5,i.local.xz,0,1);
        float height=saturate(i.normal.y*.5+.5);
        drop*=lerp(.55,1.12,height);
        drop+=light.color*gloss*.30;
        return half4(drop*max(SampleSH(n)+float3(.22,.18,.14),float3(.25,.25,.28)),1);
    }
    half4 Depth(V i):SV_Target
    {
        AtelierClip(i.world);
        // 深度也要按同一块滩面裁边，不能让不可见的网格边替湿色占位。
        if(_Puddle>.5){float rim,inside;clip(PuddleBlob(i.local,i.world,rim,inside));}
        return 0;
    }
    ENDHLSL
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass {Cull Off HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL }
        Pass {Tags {"LightMode"="DepthOnly"} ColorMask 0 Cull Off HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            ENDHLSL }
    }
}
