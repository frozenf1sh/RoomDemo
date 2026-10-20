// 文件：刮刀干湿颜料.shader。职责：第三阶段试刀板和第四阶段屋顶干颜料共用同一支可擦除表面。
// 边界：有限路径最多64段；屋顶只表现干颜料自身，不授予事实、不影响真实天空和湿色落点。
Shader "RoomDemo/Atelier/KnifePalette"
{
    Properties
    {
        _BaseMap("原表面",2D)="white"{}
        _BaseColor("原表面颜色",Color)=(.4,.35,.28,1)
        _ProbeRoof("屋面模式",Float)=0
        _WetExposed("湿色已露出",Float)=1
        _CoatReveal("结壳显现",Float)=1
        _AtelierEraseGroup("擦除组",Float)=0
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "../../Shaders/AtelierErasureField.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "AtelierPaintLook.hlsl"
    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST,_BaseColor;
        float4 _ProbeOrigin,_ProbeScale;
        float4 _ProbeErase[64],_ProbeSpread[64];
        float _ProbeRoof,_ProbeEraseCount,_ProbeSpreadCount,_ProbeRadius,_DryBrushTried,_WetExposed,_CoatReveal;
    CBUFFER_END
    struct A { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
    struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; };
    V Vert(A i) { V o; o.world=TransformObjectToWorld(i.positionOS); o.positionCS=TransformWorldToHClip(o.world); o.normal=TransformObjectToWorldNormal(i.normalOS); o.uv=TRANSFORM_TEX(i.uv,_BaseMap); return o; }
    float Segment(float2 p,float4 ab)
    {
        float2 d=ab.zw-ab.xy;
        return length(p-ab.xy-d*saturate(dot(p-ab.xy,d)/max(.000001,dot(d,d))));
    }
    float2 Hash2(float2 p) { return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453); }
    float Cracks(float2 p)
    {
        float2 cell=floor(p),f=frac(p);
        float first=10,second=10;
        for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++)
        {
            float2 offset=float2(x,y);
            float d=length(offset+Hash2(cell+offset)-f);
            if(d<first) { second=first; first=d; } else second=min(second,d);
        }
        return 1-smoothstep(.025,.065,second-first);
    }
    float2 SurfaceUV(float3 world) { return (world.xz-_ProbeOrigin.xz)/max(float2(.001,.001),_ProbeScale.xy)+.5; }
    float Erase(float2 uv)
    {
        float distance=10;
        for(int n=0;n<64;n++) { if(n>=(int)_ProbeEraseCount)break; distance=min(distance,Segment(uv,_ProbeErase[n])); }
        return 1-smoothstep(_ProbeRadius*.75,_ProbeRadius,distance);
    }
    float Spread(float2 uv)
    {
        float distance=10;
        for(int n=0;n<64;n++) { if(n>=(int)_ProbeSpreadCount)break; distance=min(distance,Segment(uv,_ProbeSpread[n])); }
        return 1-smoothstep(.03,.09,distance);
    }
    // 屋顶干颜料：不规则圆斑，随显现从中心铺开，再让试刀和整屋擦除在斑块上开门。
    float RoofBlob(float2 uv)
    {
        float2 metres=(uv-.5)*_ProbeScale.xy;
        // 屋面比圆略宽，避免在主视角的屋檐斜切里缩成一条缝，同时仍是圆斑。
        float2 shaped=float2(metres.x/1.20,metres.y);
        float inside=PaintBlob(shaped,.70,2.7,.022);
        // 显现动画只收放同一块形状，不换另一套贴图，避免中途闪成另一块表面。
        inside-=(1-saturate(_CoatReveal))*.86;
        return inside;
    }
    void Cut(float3 world)
    {
        AtelierClip(world);
        if(_ProbeRoof>.5)
        {
            float2 uv=SurfaceUV(world);
            clip(RoofBlob(uv));
            // 刮开后的洞口沿用真实接触路径，只给边沿加厘米级毛边。
            float ragged=(PaintNoise(uv*float2(185,140))-.5)*.018;
            clip(.52-Erase(uv)+ragged);
        }
    }
    half4 Colour(V i):SV_Target
    {
        Cut(i.world);
        float2 uv=SurfaceUV(i.world);
        float3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
        if(_ProbeRoof<.5)
        {
            float grain=sin(uv.y*95+sin(uv.x*23))*sin(uv.x*57);
            float cracks=Cracks(uv*float2(19,12)+sin(uv.yx*22)*.23);
            float3 wood=float3(.33,.19,.10)*(1+grain*.1);
            float dry=step(uv.x,.54);
            float revealed=Erase(uv)*dry;
            float3 crust=lerp(float3(.065,.045,.055),float3(.10,.12,.19),sin(uv.y*28)*.5+.5)*(1-cracks*.55);
            crust+=_DryBrushTried*(1-smoothstep(.001,.004,abs(uv.y-.78)))*step(.18,uv.x)*step(uv.x,.37)*.04;
            float wet=(1-smoothstep(.12,.19,length((uv-float2(.79,.50))*float2(1,1.5))))*_WetExposed;
            float moved=Spread(uv)*step(.57,uv.x);
            if(_ProbeSpreadCount>0) wet*=.68;
            base=lerp(wood,crust,dry*(1-revealed));
            // 未试刮时，厚颜料仍由结皮盖住；干壳经验成立后才露出底下的湿色。
            float skin=(1-smoothstep(.12,.19,length((uv-float2(.79,.50))*float2(1,1.5))))*(1-_WetExposed);
            base=lerp(base,crust,skin);
            base=lerp(base,float3(.018,.075,.22),max(wet,moved*.68));
            base+=moved*float3(.002,.005,.012);
        }
        if(_ProbeRoof>.5)
        {
            float2 metres=(uv-.5)*_ProbeScale.xy;
            float inside=RoofBlob(uv);
            float rim=1-smoothstep(0,.016,max(0,min(inside,.016)));
            // 湿色强度仍为0：屋顶是干透的表层，光泽和饱和都低于落下的湿颜料。
            float3 colour=PaintBluePalette(metres,metres,rim,0);
            // 干颜料只有细粉质和极浅的厚薄起伏，不再铺设大格裂纹。
            float grain=PaintNoise(metres*88+float2(4.7,-2.1));
            colour*=.93+.10*grain;
            float impasto=PaintRidge(metres*31+float2(-3.4,8.2));
            colour+=impasto*float3(.018,.032,.045)*(.30+.70*rim);
            // 边缘一线压暗，像颜料干后收起的薄边。
            colour*=1-.16*(1-smoothstep(0,.012,max(0,inside)));
            float3 normal=normalize(i.normal);
            Light key=GetMainLight();
            float3 light=SampleSH(normal)+key.color*saturate(dot(normal,key.direction));
            return half4(colour*max(light,float3(.66,.68,.76)),1);
        }
        float3 n=normalize(i.normal);
        Light key=GetMainLight();
        float3 light=SampleSH(n)+key.color*saturate(dot(n,key.direction));
        return half4(base*max(light,float3(.26,.26,.28)),1);
    }
    ENDHLSL
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Cull Off
        Pass { Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Colour
            ENDHLSL
        }
        Pass { Tags { "LightMode"="DepthOnly" } ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Depth
            half4 Depth(V i):SV_Target { Cut(i.world); return 0; }
            ENDHLSL
        }
        Pass { Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Normals
            half4 Normals(V i):SV_Target { Cut(i.world); return half4(normalize(i.normal),0); }
            ENDHLSL
        }
        Pass { Tags { "LightMode"="ShadowCaster" } ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Shadow
            half4 Shadow(V i):SV_Target { Cut(i.world); return 0; }
            ENDHLSL
        }
    }
}
