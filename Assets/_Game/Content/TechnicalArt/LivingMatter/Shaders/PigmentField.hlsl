// 文件：PigmentField.hlsl。职责：Hero 专属的无散旋涡流场与初始颜料分布。
// 边界：定义初态、流场与局部注入；后续状态通过反馈平流更新，不以滚动 UV 冒充流动。
#ifndef ROOM_PIGMENT_FIELD
#define ROOM_PIGMENT_FIELD
float _Mode, _VisualTime, _Flow, _Aspect, _Injection;
float Hash21(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float Noise(float2 p)
{
    float2 n=floor(p),f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(Hash21(n),Hash21(n+float2(1,0)),f.x),lerp(Hash21(n+float2(0,1)),Hash21(n+1),f.x),f.y);
}
float Fbm(float2 p)
{
    float v=0,w=.53;
    [unroll] for(int n=0;n<4;n++) { v+=Noise(p)*w;p=mul(float2x2(.8,-.6,.6,.8),p)*2.03+17.3;w*=.48; }
    return v;
}
float2 Star(int n)
{
    if(n==0)return float2(.765,.80);
    if(n==1)return float2(.155,.77);
    if(n==2)return float2(.565,.90);
    if(n==3)return float2(.875,.405);
    if(n==4)return float2(.345,.19);
    if(n==5)return float2(.935,.885);
    if(n==6)return float2(.095,.38);
    return float2(.665,.21);
}
float StarRadius(int n) { return n==0 ? .019 : n==1 ? .014 : .0055+frac(n*.37)*.003; }
float2 Swirl(float2 q,float2 centre,float radius,float angular)
{
    float2 d=q-centre;
    // 切向速度乘径向包络仍无散；中心不制造黑洞或持续收缩。
    return float2(-d.y,d.x)*angular*exp(-dot(d,d)/(radius*radius));
}
float Potential(float2 q)
{
    float t=_VisualTime*.017;
    return sin(q.x*5.8+sin(q.y*4.2+t))*.0022 + sin(q.y*7.1-q.x*2.5-t)*.0016;
}
float2 Velocity(float2 uv)
{
    float2 result=float2(0,0);
    if(_Mode>.5 && _Mode<1.5)
    {
        float2 d=uv-float2(.47,.51);
        float r=length(d);
        float spin=.92*(1-.60*saturate(r/.48));
        float2 v=float2(-d.y,d.x)*spin;
        v+=float2(sin(d.y*15+_VisualTime*.45),cos(d.x*13-_VisualTime*.35))*.011;
        v+=Swirl(uv,float2(.55,.47),.15,.18)+Swirl(uv,float2(.36,.58),.13,-.12);
        float wave=d.x*11+cos(d.y*8+_VisualTime*.7);
        v+=float2(-.014*sin(d.y*8+_VisualTime*.7)*cos(wave),-.020*cos(wave));
        v+=d*.080*smoothstep(4.7,6.5,_VisualTime);
        // 杯沿之前只允许切向流动；近边界把径向扰动投影掉。
        v-=normalize(d+1e-6)*dot(v,normalize(d+1e-6))*smoothstep(.40,.47,r);
        result=v*_Flow;
    }
    else if(_Mode>1.5)
    {
        float2 d=uv-float2(.32,.48);
        float r=length(d);
        float angle=atan2(d.y,d.x);
        float edge=.60+.25*sin(angle*3+1)+.16*sin(angle*7-1);
        float2 radial=d/max(r,.035)*(.018+.044*edge)*exp(-r*2.4);
        // 细小横向偏转和桌面倾向形成不规则支流，不预画一条线。
        result=radial+float2(.026,-.012)+float2(sin(uv.y*21),sin(uv.x*24))*.015;
    }
    else
    {
    float2 q=uv*float2(_Aspect,1);
    float2 v=Swirl(q,float2(.39*_Aspect,.47),.32,.18);
    v+=Swirl(q,float2(.74*_Aspect,.65),.19,-.16);
    v+=Swirl(q,float2(.13*_Aspect,.30),.12,.18);
    const float e=.003;
    // 对流函数求旋度，给主旋涡叠加平滑的不规则流动；不是给纹理加时间偏移。
    v+=float2(Potential(q+float2(0,e))-Potential(q-float2(0,e)),
        -Potential(q+float2(e,0))+Potential(q-float2(e,0)))/(2*e);
    [unroll] for(int n=0;n<8;n++)
    {
        float2 site=Star(n)*float2(_Aspect,1);
        v+=Swirl(q,site,.055,n%2==0 ? .32 : -.27);
    }
        result=v/float2(_Aspect,1)*_Flow;
    }
    return result;
}
float Spiral(float2 uv,float2 centre,float radius,float handedness)
{
    float2 d=(uv-centre)*float2(_Aspect,1);
    float r=length(d),a=atan2(d.y,d.x);
    float irregular=Fbm(d*11+centre*8);
    float phase=a*handedness+r*27+irregular*3;
    float arm=smoothstep(-.55,.80,sin(phase));
    float envelope=exp(-pow(r/radius,2.2))*(.5+.5*smoothstep(.01,.055,r));
    return arm*envelope;
}
float BrushRelief(float2 uv)
{
    float2 q=uv*float2(_Aspect,1),flow=normalize(Velocity(uv)*float2(_Aspect,1)+1e-6);
    float2 across=float2(-flow.y,flow.x);
    const float spacing=.027;
    float2 cell=floor(q/spacing);float stroke=0;
    [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
    {
        float2 id=cell+float2(x,y),h=float2(Hash21(id),Hash21(id+93.4));
        float2 centre=(id+.15+h*.70)*spacing,offset=q-centre;
        float along=dot(offset,flow),crosswise=dot(offset,across);
        float length=.017+h.x*.018,width=.0025+h.y*.0035;
        float taper=sqrt(saturate(1-pow(along/length,2)));
        float brush=(1-smoothstep(width*taper*.55,width*taper,abs(crosswise)))*(1-smoothstep(length*.8,length,abs(along)));
        float breakage=.45+.55*Noise(float2(along*150+h.x*8,crosswise*310+h.y*13));
        stroke=max(stroke,brush*breakage);
    }
    return stroke;
}
float4 InitialPigment(float2 uv)
{
    if(_Mode>.5) return 0;
    float2 q=uv*float2(_Aspect,1);
    float cloud=Fbm(q*5.5);
    float broad=exp(-pow((uv.y-(.40+.10*sin(uv.x*5-1)))/.16,2));
    float main=Spiral(uv,float2(.39,.47),.35,1);
    float other=Spiral(uv,float2(.74,.65),.20,-1);
    float low=Spiral(uv,float2(.13,.30),.14,1);
    float paint=saturate(main*.86+other*.80+low*.75+broad*.20*cloud);
    [unroll] for(int n=0;n<8;n++)
    {
        float2 delta=(uv-Star(n))*float2(_Aspect,1);float radius=length(delta);
        float arc=.42+.58*smoothstep(-.3,.8,sin(atan2(delta.y,delta.x)*2+Fbm(q*15)*3));
        paint=max(paint,exp(-pow((radius-.057)/.024,2))*arc*.40);
    }
    float3 night=lerp(float3(.011,.019,.062),float3(.035,.050,.130),cloud);
    float3 blue=lerp(float3(.025,.11,.32),float3(.17,.30,.44),Fbm(q*8+5));
    float violet=Fbm(q*4-7);
    blue=lerp(blue,float3(.24,.10,.34),smoothstep(.48,.8,violet)*.68);
    float3 colour=lerp(night,blue,paint);
    // 不规则长短笔触写进历史纹理；刷毛不会重新锁回同心圆或按帧重抽。
    float ridges=BrushRelief(uv);
    colour*=1+(ridges-.4)*.21*paint;
    return float4(colour,.35+.44*ridges*paint);
}
float4 PigmentSource(float2 uv,float4 current,float dt)
{
    if(_Mode>.5 && _Mode<1.5)
    {
        float2 p=uv-float2(.70+sin(_VisualTime*.7)*.012,.54+sin(_VisualTime*1.2)*.014);
        float ink=exp(-dot(p,p)/.00042)*(.65+.35*Noise(uv*70))*_Injection*dt*3.4;
        float2 p2=uv-float2(.40,.69);
        float indigo=exp(-dot(p2,p2)/.00032)*_Injection*dt*2.6;
        // 小股连续注入被径向剪切卷成细长色丝，淡尾保留透明水域。
        current=saturate(current*exp(-dt*.10)+float4(ink,indigo,ink*.12,0));
    }
    else if(_Mode>1.5)
    {
        float2 p=uv-float2(.32,.48);
        float amount=exp(-dot(p,p)/.0011)*_Injection*dt*1.6;
        #if defined(PIGMENT_SIMULATION)
        float2 sampleUV=float2(.70+sin(_VisualTime*.7)*.012,.54+sin(_VisualTime*1.2)*.014);
        float4 supply=tex2D(_FeedPigment,sampleUV);
        supply.g=tex2D(_FeedPigment,float2(.40,.69)).g*.75;
        current=saturate(current+float4(amount,amount*saturate(supply.r*5+.05),amount*saturate(supply.g*4+.025),0));
        #endif
    }
    else
    {
    [unroll] for(int n=0;n<8;n++)
    {
        float2 d=(uv-Star(n))*float2(_Aspect,1);
        float influence=exp(-dot(d,d)/.0007)*dt*.025;
        // 暖星的颜色进入实际被平流的周边颜料，产生细小的赭金卷边。
        current.rgb=lerp(current.rgb,float3(.40,.22,.055),influence);
    }
    }
    return current;
}
#endif
