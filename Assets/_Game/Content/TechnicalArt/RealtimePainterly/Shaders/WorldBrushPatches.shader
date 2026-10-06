// 文件：WorldBrushPatches.shader。职责：移植世界空间 Voronoi 取色和光照切向笔触算法。
// 边界：算法参考 tantaneity/painterly-urp，未复制该仓库源码；只重绘当前真实 3D 帧。
// 世界种子与尺寸固定，不引入时间、相机 UV 随机数、画作贴图或材质替换。
Shader "Hidden/RoomDemo/RealtimePainterly/WorldBrushPatches"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            Name "WorldAnchoredBrushes"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            TEXTURE2D_X(_OriginalScene);
            float _PainterlyIntensity, _PatchSize, _PatchBlend, _PigmentVariation;
            float _HighlightStrength, _DebugWorldCells;
            float4 _BrushLightDirection;

            // 整数散列给同一个世界格子永久相同的种子；不随帧与视角重抽。
            uint MixBits(uint x)
            {
                x ^= x >> 16; x *= 0x7feb352du;
                x ^= x >> 15; x *= 0x846ca68bu;
                return x ^ (x >> 16);
            }
            float3 CellRandom(int3 cell)
            {
                uint seed = MixBits(asuint(cell.x) ^ MixBits(asuint(cell.y)) ^ MixBits(asuint(cell.z)));
                return float3(MixBits(seed), MixBits(seed + 17u), MixBits(seed + 71u)) / 4294967295.0;
            }
            bool HasSurface(float d)
            {
                #if UNITY_REVERSED_Z
                return d > 0.000001;
                #else
                return d < 0.999999;
                #endif
            }
            float2 ProjectWorld(float3 world)
            {
                float4 clip = TransformWorldToHClip(world);
                float2 uv = clip.xy / clip.w * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y;
                #endif
                return uv;
            }
            void NearestPatches(float3 world, float size, out float3 a, out float3 b,
                out float3 colourSeed, out float boundary)
            {
                float3 grid = world / size;
                int3 baseCell = (int3)floor(grid);
                float nearest = 1e8, second = 1e8;
                a = b = world;
                colourSeed = 0;
                // 27 邻域是 3D Voronoi 的固定候选集，不在屏幕上铺网格。
                [unroll] for (int z = -1; z <= 1; z++)
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    int3 cell = baseCell + int3(x,y,z);
                    float3 h = CellRandom(cell);
                    float3 centre = (cell + 0.18 + h * 0.64) * size;
                    float3 delta = (world - centre) / size;
                    // 各向异性距离产生长短色块，主方向固定于世界，不依赖摄像机。
                    float3 axis = normalize(float3(0.85,0.25,0.45));
                    float along = dot(delta, axis);
                    float dist = dot(delta, delta) - along * along * 0.58;
                    if (dist < nearest)
                    {
                        second = nearest; b = a;
                        nearest = dist; a = centre; colourSeed = h;
                    }
                    else if (dist < second) { second = dist; b = centre; }
                }
                boundary = smoothstep(0, 0.35, (second-nearest)/max(second+nearest,0.001));
            }
            half3 PatchColour(float3 anchor, float3 world, float3 normal, float2 originalUV)
            {
                // 将世界格子中心投到当前表面的切平面，再向当前帧取色。
                anchor -= normal * dot(anchor - world, normal);
                float2 uv = ProjectWorld(anchor);
                if (any(uv <= 0) || any(uv >= 1)) uv = originalUV;
                float d = SampleSceneDepth(uv);
                float3 found = ComputeWorldSpacePosition(uv, d, UNITY_MATRIX_I_VP);
                float3 foundNormal = SampleSceneNormals(uv);
                // 深度和法线保护防止盆栽取到背景、木桌取到水面。
                bool valid = HasSurface(d) && distance(found,anchor) < _PatchSize*0.75
                    && dot(normal,foundNormal) > 0.75;
                uv = valid ? uv : originalUV;
                return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv).rgb;
            }
            float DirectionalHighlights(float3 world,float3 normal,float3 light)
            {
                // 对应参考实现的邻域笔触候选：长笔触可以越过取色色块，不能被单格截断。
                const float spacing = 0.22;
                int3 grid = (int3)floor(world/spacing);
                float3 tangent = cross(normal,light);
                float magnitude = length(tangent);
                if (magnitude < 0.015) return 0;
                tangent /= magnitude;
                float3 across = normalize(cross(normal,tangent));
                float lightMask = smoothstep(0.35,0.78,dot(normal,light));
                float coverage = 0;
                [unroll] for (int z=-1;z<=1;z++)
                [unroll] for (int y=-1;y<=1;y++)
                [unroll] for (int x=-1;x<=1;x++)
                {
                    int3 id = grid+int3(x,y,z);
                    float3 random = CellRandom(id+int3(57,19,131));
                    if (random.x > 0.24) continue;
                    float3 anchor = (id+0.18+random*0.64)*spacing;
                    float3 offset = world-anchor;
                    if (abs(dot(offset,normal)) > spacing*0.7) continue;
                    float angle = (random.y-0.5)*0.55;
                    float3 direction = tangent*cos(angle)+across*sin(angle);
                    float3 perpendicular = across*cos(angle)-tangent*sin(angle);
                    float length = lerp(0.06,0.145,random.y);
                    float width = lerp(0.009,0.019,random.z);
                    float along = dot(offset,direction)/length;
                    float crossStroke = abs(dot(offset,perpendicular))/width;
                    float profile = cos(clamp(along,-1,1)*1.5707963);
                    profile *= profile;
                    float antialias = max(fwidth(crossStroke),0.08);
                    float mask = 1-smoothstep(profile-antialias,profile+antialias,crossStroke);
                    mask *= 1-smoothstep(0.85,1,abs(along));
                    coverage = max(coverage,mask);
                }
                return coverage*lightMask;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                half3 original = SAMPLE_TEXTURE2D_X(_OriginalScene,sampler_LinearClamp,uv).rgb;
                float depth = SampleSceneDepth(uv);
                if (!HasSurface(depth)) return half4(original,1);
                float3 world = ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                float3 normal = normalize(SampleSceneNormals(uv) + 1e-6);
                float3 a,b,seed;
                float boundary;
                NearestPatches(world,_PatchSize,a,b,seed,boundary);
                half3 patchA = PatchColour(a,world,normal,uv);
                half3 patchB = PatchColour(b,world,normal,uv);
                half3 colour = lerp((patchA+patchB)*0.5,patchA,boundary);
                half3 filtered = SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv).rgb;
                colour = lerp(filtered,colour,_PatchBlend);
                // 颜料明度只作小幅变化，物体原来的色相和明暗仍决定画面。
                colour *= 1 + (seed.z-0.5)*_PigmentVariation;

                float3 light = normalize(_BrushLightDirection.xyz);
                float stroke = DirectionalHighlights(world,normal,light);
                // 高光是一层稀疏的有色颜料，而非白色发光粒子；宽度随世界透视缩放。
                half3 highlight = colour*1.20 + half3(0.012,0.010,0.007);
                colour = lerp(colour,highlight,stroke*_HighlightStrength);
                if (_DebugWorldCells > 0.5) colour = seed;
                return half4(max(colour,0),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "PainterlyIntensityComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X(_OriginalScene);
            float _PainterlyIntensity;
            half4 Composite(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 original = SAMPLE_TEXTURE2D_X(_OriginalScene,sampler_LinearClamp,i.texcoord).rgb;
                half3 painted = SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,i.texcoord).rgb;
                return half4(lerp(original,painted,_PainterlyIntensity),1);
            }
            ENDHLSL
        }
    }
}
