// 文件：AtelierErasureField.hlsl。职责：所有实体共用的世界笔刷场；无逐物件缩放或屏幕噪声。
#ifndef ATELIER_ERASURE_FIELD
#define ATELIER_ERASURE_FIELD
float _AtelierRoof, _AtelierRoom, _AtelierFrame, _AtelierMark;
#ifndef ATELIER_GROUP_DEFINED
float _AtelierEraseGroup;
#endif
float AtelierGrain(float3 p)
{
    // 固定世界纹理，长纤维和细刷毛共同决定边沿，不随时间抖动。
    return sin(p.x*11.3+p.z*3.7+sin(p.y*9.2))*.10+sin(p.x*83+p.y*47+p.z*37)*.007;
}
float AtelierRoomOrder(float3 p)
{
    // 笔刷先掠过高处，随后越过窗和桌沿，最后收去脚下；跨材质保持连续。
    return saturate((4.7-p.y)/5.9+(p.x+4.5)*.018+sin(p.z*.62+p.x*.5)*.045+sin(p.x*1.85+p.z*.34)*.075+sin(p.x*3.1-p.z*.24)*.028+AtelierGrain(p)*.035);
}
float AtelierRoofRadius(float3 p)
{
    float2 q=(p.xz-float2(0,-.3))/float2(2.94,3.35);
    return length(q)+AtelierGrain(p)*.13+sin(atan2(q.y,q.x)*7)*.027;
}
float AtelierLift(float3 p)
{
    float distance=2;
    if(_AtelierRoom>.001)distance=AtelierRoomOrder(p)-_AtelierRoom*1.2+.025;
    if(_AtelierEraseGroup>.5&&_AtelierEraseGroup<1.5&&_AtelierRoof>.001)
        distance=min(distance,(AtelierRoofRadius(p)-_AtelierRoof)*.32);
    // 薄层先失去厚重颜料，再露出细纤维缺口；不是给边沿加 Bloom 或发光。
    return 1-smoothstep(.006,.054,distance);
}
void AtelierClip(float3 p)
{
    if(_AtelierRoom>.0001)
    {
        float remaining=AtelierRoomOrder(p)-_AtelierRoom*1.2+.025;
        // 细刷毛只留下毫米级缺口，不沿高度反复折返形成一整排锯齿。
        float bristle=sin(p.x*47+sin(p.z*17)*1.8)*.00065+sin(p.z*79+p.x*31)*.00035;
        clip(remaining+bristle*(1-smoothstep(.0,.04,remaining)));
    }
    if(_AtelierEraseGroup>.5 && _AtelierEraseGroup<1.5 && _AtelierRoof>.0001)
        clip(AtelierRoofRadius(p)-_AtelierRoof);
    if(_AtelierEraseGroup>1.5 && _AtelierFrame>.0001)
    {
        // 只擦掉有漆的窗框，灰泥和帘布不跟着消失。
        float order=saturate((p.y-.8)/3.2+sin(p.x*17)*.04+AtelierGrain(p)*.10);
        clip(order-_AtelierFrame*1.15);
    }
}
float AtelierMark(float3 p)
{
    // 一道连续的干刷笔势跨过后墙灰泥、木框与两侧亚麻；它是可见调查依据。
    float centre=3.19+.115*p.x+.05*sin(p.x*1.8);
    float stripe=1-smoothstep(.018,.058,abs(p.y-centre)+AtelierGrain(p)*.035);
    float dry=.63+.37*smoothstep(-.5,.6,sin(p.x*183+p.z*51)+sin(p.y*397));
    return stripe*dry*_AtelierMark*smoothstep(1.6,2.0,p.z);
}
#endif
