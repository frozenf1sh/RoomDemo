// 文件：刮刀干湿颜料.shader。职责：沿用已验收TA的干壳裂纹、露木与软色薄带。
// 边界：有限路径最多64段；只消费接触表现参数，不授予事实、不影响屋顶。
Shader "RoomDemo/Atelier/KnifePalette"
{
    Properties
    {
        _BaseMap("原表面",2D)="white"{}
        _BaseColor("原表面颜色",Color)=(.4,.35,.28,1)
        _ProbeRoof("屋面模式",Float)=0
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST,_BaseColor;
        float4 _ProbeOrigin,_ProbeScale;
        float4 _ProbeErase[64],_ProbeSpread[64];
        float _ProbeRoof,_ProbeEraseCount,_ProbeSpreadCount,_ProbeRadius,_DryBrushTried;
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
    void Cut(float3 world)
    {
        if(_ProbeRoof>.5)
        {
            float2 uv=SurfaceUV(world);
            float irregular=sin(world.x*310+sin(world.z*47))*sin(world.z*247)*.035;
            clip(.52-Erase(uv)+irregular);
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
            float wet=1-smoothstep(.12,.19,length((uv-float2(.79,.50))*float2(1,1.5)));
            float moved=Spread(uv)*step(.57,uv.x);
            if(_ProbeSpreadCount>0) wet*=.68;
            base=lerp(wood,crust,dry*(1-revealed));
            base=lerp(base,float3(.018,.075,.22),max(wet,moved*.68));
            base+=moved*float3(.002,.005,.012);
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
