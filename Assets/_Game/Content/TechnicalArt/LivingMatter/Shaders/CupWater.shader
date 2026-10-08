// 文件：CupWater.shader。职责：杯内自由液面、颜料折射与出杯液体的共同材质。
// 边界：顶点水波是流体近似；颜料来自实时平流纹理，水不是伸长线条。
Shader "RoomDemo/LivingMatter/CupWater"
{
    Properties
    {
        _Pigment("运行时颜料",2D)="black"{}
        _Flow("流动",Range(0,1))=0
        _Surge("杯沿接触",Range(0,1))=0
        _CupPhase("液面时间",Float)=0
        _Sheet("出杯流束",Float)=0
        _Opacity("液体透明度",Range(0,1))=.88
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            TEXTURE2D(_Pigment);SAMPLER(sampler_Pigment);
            float _Flow,_Surge,_CupPhase,_Sheet,_Opacity;
            struct Input { float3 positionOS:POSITION;float2 uv:TEXCOORD0;float3 normalOS:NORMAL; };
            struct Output { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2; };
            float Height(float2 uv)
            {
                float2 d=(uv-.5)*2;
                float r=length(d),a=atan2(d.y,d.x);
                float waves=sin(a*3-_CupPhase*1.3+r*4)*.032*r*r;
                waves+=sin(d.x*8+d.y*4-_CupPhase*1.8)*.012;
                float meniscus=smoothstep(.84,1,r)*.013;
                return meniscus+_Surge*.085+_Flow*(waves-.078*exp(-r*r*4));
            }
            Output Vertex(Input i)
            {
                Output o;float3 p=i.positionOS;
                if(_Sheet<.5) p.y+=Height(i.uv);
                o.world=TransformObjectToWorld(p);o.positionCS=TransformWorldToHClip(o.world);
                o.uv=i.uv;o.normal=TransformObjectToWorldNormal(i.normalOS);return o;
            }
            half4 Fragment(Output i):SV_Target
            {
                float2 uv=i.uv;
                float3 normal;
                if(_Sheet<.5)
                {
                    float h=Height(uv),e=.003;
                    normal=normalize(float3((h-Height(uv+float2(e,0)))/(e*1.28),1,(h-Height(uv+float2(0,e)))/(e*1.28)));
                }
                else normal=normalize(i.normal+float3(sin(uv.y*30-_CupPhase*3)*.04,0,cos(uv.y*23-_CupPhase*2)*.025));
                float2 screen=GetNormalizedScreenSpaceUV(i.positionCS);
                float2 paintUV=_Sheet<.5 ? uv : float2(.70+sin(_CupPhase*.7)*.012,.54+sin(_CupPhase*1.2)*.014+sin(uv.y*17-_CupPhase*5)*.009);
                float4 paint=SAMPLE_TEXTURE2D(_Pigment,sampler_Pigment,paintUV);
                float concentration=saturate((paint.r+paint.g)*6.5);
                float3 transmitted=SampleSceneColor(screen+normal.xz*.008*_Flow);
                float3 tint=lerp(float3(.008,.27,.31),float3(.024,.047,.15),paint.g/max(paint.r+paint.g,.0001));
                float3 water=transmitted*.69+float3(.018,.059,.067);
                float3 colour=lerp(water,tint,concentration*.89);
                if(_Sheet>.5) colour=lerp(colour,float3(.018,.20,.23),.09);
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float3 reflected=reflect(-view,normal);
                float3 halfDirection=normalize(view+normalize(float3(-.5,.9,.4)));
                float spec=pow(saturate(dot(normal,halfDirection)),84);
                float reflection=pow(saturate(dot(reflected,normalize(float3(-.44,.64,-.62)))),28);
                float fresnel=.04+.32*pow(1-saturate(abs(dot(normal,view))),4);
                // 程序性窗户反射通过真实法线投影；静水保留窗格，波动时反射自然扭曲。
                float2 window=reflected.xz/max(reflected.y,.08)-float2(-1.13,-1.40);
                float2 pane=(1-smoothstep(float2(.24,.35),float2(.29,.40),abs(window)));
                float mullion=1-.78*max(1-smoothstep(.009,.022,abs(window.x)),1-smoothstep(.009,.022,abs(window.y)));
                float reflectedWindow=pane.x*pane.y*mullion*smoothstep(.05,.2,reflected.y);
                colour+=float3(.80,.88,.93)*(spec*.85+reflection*.29+fresnel*.17+reflectedWindow*.56*(1-concentration*.52));
                if(_Sheet>.5)
                {
                    // 流束侧面以体积色、透光边缘和连续白色反光表现圆截面，避免扁平蓝带。
                    float facing=abs(dot(normal,view));
                    float brightSide=pow(saturate(dot(normal,normalize(float3(-.2,.2,.9)))),16);
                    colour*=.50+.55*facing;
                    colour+=float3(.29,.50,.51)*pow(1-facing,3)*.65;
                    colour+=float3(.71,.87,.88)*brightSide*.80;
                }
                float edge=1;
                return half4(colour,_Opacity*edge);
            }
            ENDHLSL
        }
    }
}
