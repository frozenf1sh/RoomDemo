// 文件：AtelierPaintLook.hlsl。职责：为屋顶干颜料与落地湿色提供同一套确定性油画斑、调色和不规则圆边。
// 边界：只做程序化颜色与法线；不读取时间、屏幕或玩法事实，干壳和湿色各自决定透明度与光泽。
#ifndef ATELIER_PAINT_LOOK_HLSL
#define ATELIER_PAINT_LOOK_HLSL

// 固定世界尺度的值噪声，避免镜头推近时像屏幕贴纸一样跟着像素走。
float PaintHash(float2 p)
{
    p = frac(p * float2(127.1, 311.7));
    p += dot(p, p + 34.23);
    return frac(p.x * p.y * 43758.5453);
}

float PaintNoise(float2 p)
{
    float2 cell = floor(p), f = frac(p);
    f = f * f * (3 - 2 * f);
    float a = PaintHash(cell);
    float b = PaintHash(cell + float2(1, 0));
    float c = PaintHash(cell + float2(0, 1));
    float d = PaintHash(cell + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// 三与四层的低频叠加；层数固定，保证不同质量的构建都能编进同一支变体。
float PaintFbm3(float2 p)
{
    float sum = PaintNoise(p) * .5;
    sum += PaintNoise(p * 2.03 + float2(13.7, -7.9)) * .25;
    sum += PaintNoise(p * 4.11 + float2(-5.3, 17.1)) * .125;
    return sum / .875;
}

float PaintFbm4(float2 p)
{
    float sum = PaintNoise(p) * .5;
    sum += PaintNoise(p * 2.03 + float2(13.7, -7.9)) * .25;
    sum += PaintNoise(p * 4.11 + float2(-5.3, 17.1)) * .125;
    sum += PaintNoise(p * 8.07 + float2(23.9, 3.7)) * .0625;
    return sum / .9375;
}

// 把噪声折成细窄的脊，只用于笔触高光，不铺满整块表面。
float PaintRidge(float2 p)
{
    float n = PaintNoise(p);
    return 1 - abs(n * 2 - 1);
}

// 单个向外鼓出的颜料尖角；角度、宽度和高度都由调用方给定，不依赖纹理坐标。
float PaintSpur(float angle, float centre, float width, float height)
{
    float delta = abs(frac((angle - centre) / 6.2831853 + .5) - .5) * 6.2831853;
    return height * exp(-(delta * delta) / max(.0001, width * width));
}

// 不规则圆边：少量大起伏 + 三处颜料尖角；毛边由二维噪声负责，不在极角上留接缝。
float PaintBlobEdge(float angle, float seed, float radius, float roughness)
{
    float edge = radius;
    edge += .026 * sin(angle * 3 + 1.7 + seed);
    edge += .017 * sin(angle * 4 - .6 + seed * 1.7);
    edge += .009 * sin(angle * 7 + 2.3 - seed * .6);
    edge += .004 * sin(angle * 11 + .8 + seed * 2.1);
    edge += PaintSpur(angle, .62 + seed * .3, .19, .046);
    edge += PaintSpur(angle, 2.47 + seed * .2, .14, .036);
    edge += PaintSpur(angle, 4.38 - seed * .4, .22, .040);
    return edge;
}

// 返回从斑块边沿向内的有符号距离；单位与传入的坐标一致（米）。
float PaintBlob(float2 metres, float radius, float seed, float roughness)
{
    float r = length(metres);
    float angle = atan2(metres.y, metres.x);
    float edge = PaintBlobEdge(angle, seed, radius, roughness);
    edge += (PaintFbm3(metres * 10.5 + seed * 1.7) - .5) * (roughness + .006);
    // 厘米级毛边让近景不像一张光滑塑料片，远看仍保持圆斑轮廓。
    edge += (PaintNoise(metres * 47 + seed * 5.1) - .5) * .012;
    return edge - r;
}

// 干颜料与湿颜料共用同一组蓝；湿色更亮、更饱和，干色略压灰并保留粉质。
float3 PaintBluePalette(float2 w, float2 local, float rim, float wetness)
{
    float2 warp = float2(PaintFbm3(w * 1.9 + float2(3.1, -7.4)), PaintFbm3(w * 1.9 + float2(-9.8, 2.6))) - .5;
    float swirl = PaintFbm4(w * 3.4 + warp * 1.8);
    float broad = PaintFbm3(w * 1.05 + warp * 1.4 + float2(11.2, 4.7));
    float mottle = PaintFbm3(w * 7.0 + warp * .8 + float2(2.4, 9.1));

    // 沿一条斜向流势拉长的宽笔与细刷毛；阈值化后形成长短不一的可见笔触。
    float2 direction = normalize(float2(.83, -.56));
    float2 side = float2(-direction.y, direction.x);
    float2 along = float2(dot(local, direction), dot(local, side));
    float band = PaintNoise(float2(along.x * 5.0, along.y * 30) + warp * 3.1 + float2(swirl * 3.4, mottle * 2.2) + float2(4.1, -2.7));
    float band2 = PaintNoise(float2(along.x * 17, along.y * 82) + warp * 5.0 + float2(-8.3, 6.2));
    float stroke = smoothstep(.36 + .12 * mottle, .68, band) * (.50 + .50 * band2);
    float tip = smoothstep(.70, .92, band2 * .60 + band * .40);
    float ridge = smoothstep(.54, .70, band) * (1 - smoothstep(.78, .90, band));

    float t = saturate(.14 + broad * .38 + stroke * .58 + (swirl - .5) * .22);
    // 大块稀薄的天蓝，和细笔触分开，避免整块只有一个频率。
    float wash = smoothstep(.52, .86, PaintFbm3(w * 2.2 + warp * 2.6 + float2(6.7, -1.9)) + stroke * .18);
    t = saturate(t + wash * .20);

    // 深靛蓝做底，钴蓝铺开，笔触高处泛出天蓝；湿色把最亮的一档也带进来。
    float3 deep = float3(.020, .055, .170);
    float3 ultra = float3(.035, .110, .300);
    float3 cobalt = float3(.060, .190, .450);
    float3 sky = float3(.110, .300, .520);
    float3 ice = float3(.200, .400, .560);
    float3 colour = lerp(deep, ultra, smoothstep(0.0, .42, t));
    colour = lerp(colour, cobalt, smoothstep(.30, .78, t));
    colour = lerp(colour, sky, smoothstep(.70, .96, t) * (.55 + wetness * .30));
    colour = lerp(colour, ice, tip * (.10 + wetness * .22));
    colour += ridge * (.035 + wetness * .035) * ice;

    // 方向性的明暗渐变：受光的一侧偏亮，另一侧压在靛蓝里。
    float shade = saturate(.46 + dot(local, float2(-.48, .58)) * 1.55);
    colour *= lerp(.60, 1.38, shade);
    // 低频紫灰只压在最暗的厚颜料里，湿色不掺入。
    colour = lerp(colour, float3(.055, .050, .140), smoothstep(.30, .95, broad * .7 + (1 - shade) * .3) * (1 - wetness) * .10);

    // 细粉质与油气；不让整块颜色平掉。
    colour *= .90 + .17 * mottle;
    colour += tip * float3(.020, .045, .060);

    // 干颜料收成一线暗边，湿颜料则有一圈表面张力亮边。
    colour = lerp(colour, colour * .55, rim * (1 - wetness) * .55);
    colour = lerp(colour, ice, rim * wetness * .06);
    colour *= lerp(.94, 1.02, saturate(rim * 2));

    // 干颜料带一点粉质的灰，湿颜料保持饱和；最后只提一点蓝的纯度。
    float grey = dot(colour, float3(.24, .58, .18));
    colour = lerp(colour, grey.xxx, (1 - wetness) * .04);
    float luma = dot(colour, float3(.2126, .7152, .0722));
    colour = luma.xxx + (colour - luma.xxx) * (1.06 + wetness * .04);
    return max(colour, float3(0, 0, 0));
}

#endif
