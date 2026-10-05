// 共享的轻笔触调制：世界空间方向、暖冷色差和细小纸面起伏。
// 强度由表现消费者提供，Shader 无法读取或修改世界事实。
#ifndef ROOM_PAINTERLY_PIGMENT
#define ROOM_PAINTERLY_PIGMENT
float _PainterlyIntensity;
float PigmentHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float PigmentNoise(float2 p)
{
    float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(PigmentHash(i),PigmentHash(i+float2(1,0)),f.x),lerp(PigmentHash(i+float2(0,1)),PigmentHash(i+1),f.x),f.y);
}
float3 PainterlyPigment(float3 color,float3 world,float3 normal)
{
    float2 plane=lerp(world.xz,world.xy,abs(normal.z));
    plane.x+=plane.y*.28;
    float dab=PigmentNoise(plane*float2(5.5,26));
    float flow=sin(plane.y*44+PigmentNoise(plane*3.1)*7);
    float ridge=smoothstep(.36,.82,dab)*(.72+.28*flow);
    float3 warmth=lerp(float3(.88,.95,1.09),float3(1.13,1.02,.84),ridge);
    float3 painted=color*warmth*(.90+dab*.21);
    // 小量饱和度与短笔触，避免家具成为浮在天空前的另一套材质。
    float grey=dot(painted,float3(.21,.72,.07));
    painted=lerp(grey.xxx,painted,1.13);
    return lerp(color,painted,saturate(_PainterlyIntensity)*.88);
}
#endif
