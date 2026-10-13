// 文件：ApprovedSkyPaint.hlsl。职责：复用原 Hero 的颜料、暖星与动态笔势。
// 边界：只把两份有限历史在取色前混合；不移动星位，不添加替代颜料流动的微光。
TEXTURE2D(_PigmentNext); SAMPLER(sampler_PigmentNext);
float _SkyHistoryBlend;
float4 SampleSkyPigment(float2 uv)
{
    float4 colour=SAMPLE_TEXTURE2D(_Pigment,sampler_Pigment,uv);
    // 同一笔触只绘制一次，先混合浓度与厚度，避免两套刷毛轮廓交叉重影。
    [branch] if(_SkyHistoryBlend>.00001)
        colour=lerp(colour,SAMPLE_TEXTURE2D(_PigmentNext,sampler_PigmentNext,uv),_SkyHistoryBlend);
    return colour;
}
            float3 FlowBrushes(float2 uv,float3 pigment,float2 direction)
            {
                float2 q=uv*float2(1.7777778,1),grid=floor(q/.036);
                float3 painted=0;float weight=0;
                [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
                {
                    float2 id=grid+float2(x,y),seed=float2(Hash21(id+6),Hash21(id+92));
                    float2 anchor=(id+.12+seed*.76)*.036,delta=q-anchor;
                    float angle=(seed.x-.5)*.30;
                    float2 tangent=float2(direction.x*cos(angle)-direction.y*sin(angle),direction.x*sin(angle)+direction.y*cos(angle));
                    float2 across=float2(-tangent.y,tangent.x);
                    float along=dot(delta,tangent),side=dot(delta,across);
                    float radius=length(q-float2(.39*1.7777778,.47));
                    side-=along*along*.5/(radius+.09);
                    float length=.021+seed.x*.030,width=.0035+seed.y*.0040;
                    float taper=sqrt(saturate(1-pow(along/length,2)));
                    float mask=(1-smoothstep(width*taper*.72,width*taper+.0006,abs(side)))*(1-smoothstep(length*.87,length,abs(along)));
                    float3 colour=SampleSkyPigment(anchor/float2(1.7777778,1)).rgb;
                    colour*=.80+seed.y*.43;
                    painted+=colour*mask;weight+=mask;
                }
                float visible=smoothstep(.052,.13,max(pigment.r,max(pigment.g,pigment.b)));
                return lerp(pigment,painted/max(weight,.0001),saturate(weight)*visible*.66);
            }
            half4 LivingSkyPaint(float2 inputUV)
            {
                float2 uv=inputUV;
                float4 paint=SampleSkyPigment(uv);
                float2 ts=_Pigment_TexelSize.xy;
                float h1=SampleSkyPigment(uv+float2(ts.x,0)).a;
                float h2=SampleSkyPigment(uv+float2(0,ts.y)).a;
                float relief=clamp((h1-paint.a)*1.3+(h2-paint.a)*.8,-.10,.10);
                float2 direction=normalize(Velocity(uv)*float2(1.7777778,1)+1e-6);
                float3 colour=FlowBrushes(uv,paint.rgb,direction)*(1+relief);
                // 未点亮时只保留冷色云的笔触，点亮以后才出现暖星与光晕。
                colour=lerp(dot(colour,float3(.22,.6,.18))*float3(.18,.33,.62),colour,_StarGain);
                colour=SkyStarLayer(colour,uv);
                float vignette=smoothstep(.82,.20,length((uv-.5)*float2(.85,1)));
                return half4(colour*lerp(.79,1,vignette),1);
            }
            float3 SkyStarLayer(float3 colour,float2 uv)
            {
                [unroll] for(int n=0;n<8;n++)
                {
                    float2 d=(uv-Star(n))*float2(1.7777778,1);
                    float angle=atan2(d.y,d.x),r=length(d);
                    float size=StarRadius(n);
                    float contour=size*(1+.026*sin(angle*7+n)+.018*sin(angle*13-n));
                    float core=1-smoothstep(contour*.70,contour*1.15,r);
                    float halo=exp(-r*r/(size*size*15));
                    float blueBoundary=exp(-pow((r-size*2.7)/(size*1.4),2));
                    colour*=1-blueBoundary*.27*_StarGain;
                    colour+=float3(.24,.16,.035)*halo*_StarGain;
                    colour=lerp(colour,float3(1.35,.79,.23)*(1+.07*sin(angle*9+r*800)),core*_StarGain);
                }
                // 小星固定且稀疏；没有随机闪烁或满屏发光点。
                [unroll] for(int s=0;s<19;s++)
                {
                    float2 site=float2(frac(.197+s*.6180339),.08+frac(.71+s*.381966)*.85);
                    float r=length((uv-site)*float2(1.7777778,1));
                    colour+=float3(.49,.36,.16)*(1-smoothstep(.0006,.0018,r))*_StarGain;
                }
                return colour;
            }
