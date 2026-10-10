// 文件：OrdinarySky.hlsl。职责：共享解释前的静止暮云，保证单独点亮不会改画云层。
// 边界：只用世界方向；不读时间、玩法事实或 GPU 历史。
#ifndef ROOM_ORDINARY_SKY_INCLUDED
#define ROOM_ORDINARY_SKY_INCLUDED
float OrdinaryHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float OrdinaryNoise(float2 p)
{
    float2 cell=floor(p), f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(OrdinaryHash(cell),OrdinaryHash(cell+float2(1,0)),f.x),lerp(OrdinaryHash(cell+float2(0,1)),OrdinaryHash(cell+1),f.x),f.y);
}
float3 OrdinarySkyColour(float3 d)
{
    float height=saturate(d.y*1.3+.08);
    float3 colour=lerp(float3(.53,.45,.38),float3(.055,.105,.20),pow(height,.45));
    float2 uv=float2(atan2(d.x,d.z),asin(clamp(d.y,-1,1)));
    float2 p=uv*float2(1.9,5.7)+float2(3,2);
    float clouds=OrdinaryNoise(p)*.55+OrdinaryNoise(p*2.04+7)*.27+OrdinaryNoise(p*4.1+13)*.12+OrdinaryNoise(p*8.3)*.06;
    float bands=smoothstep(.42,.66,clouds)*smoothstep(-.03,.16,d.y)*(1-smoothstep(.65,.95,d.y));
    colour=lerp(colour,float3(.31,.32,.36),bands*.64);
    // 暗星只供初次观察，不预先出现明亮光晕。
    for(int n=0;n<7;n++)
    {
        float2 site=float2(-.5+n*.21,.2+frac(n*.37)*.4);
        float r=length((uv-site)*float2(1,1.3));
        colour+=float3(.12,.13,.16)*(1-smoothstep(.0008,.0026,r));
    }
    return colour;
}
#endif
