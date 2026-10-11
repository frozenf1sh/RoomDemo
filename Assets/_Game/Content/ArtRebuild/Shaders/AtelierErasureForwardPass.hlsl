#include "AtelierErasureField.hlsl"
#ifndef UNIVERSAL_FORWARD_LIT_PASS_INCLUDED
#define UNIVERSAL_FORWARD_LIT_PASS_INCLUDED

#include "AtelierLitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

#if defined(_PARALLAXMAP)
#define REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR
#endif

#if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL)
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif

// keep this file in sync with LitGBufferPass.hlsl

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 staticLightmapUV   : TEXCOORD1;
    float2 dynamicLightmapUV  : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD1;
#endif

    float3 normalWS                 : TEXCOORD2;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    half4 tangentWS                : TEXCOORD3;    // xyz: tangent, w: sign
#endif

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight   : TEXCOORD5; // x: fogFactor, yzw: vertex light
#else
    half  fogFactor                 : TEXCOORD5;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD6;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS                : TEXCOORD7;
#endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
#ifdef DYNAMICLIGHTMAP_ON
    float2  dynamicLightmapUV : TEXCOORD9; // Dynamic lightmap UVs
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD10;
#endif

    float4 positionCS               : SV_POSITION;

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
#endif

#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#endif

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(_NORMALMAP) || defined(_DETAIL)
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);

    #if defined(_NORMALMAP)
    inputData.tangentToWorld = tangentToWorld;
    #endif
    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
#endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    #if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
    #endif
    #endif
}

void InitializeBakedGIData(Varyings input, inout InputData inputData)
{
    #if defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy, inputData.normalWS);
    #elif defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
    #else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #endif
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex lighting and SH evaluation
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);

    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    #endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

    // already normalized from normal transform to WS.
    output.normalWS = normalInput.normalWS;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    real sign = input.tangentOS.w * GetOddNegativeScale();
    half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    output.tangentWS = tangentWS;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
    output.viewDirTS = viewDirTS;
#endif

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
#else
    output.fogFactor = fogFactor;
#endif

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    output.positionWS = vertexInput.positionWS;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    output.positionCS = vertexInput.positionCS;

    return output;
}

// Used in Standard (Physically Based) shader
void LitPassFragment(
    Varyings input
    , FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_PARALLAXMAP)
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
#else
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
#endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif

    AtelierClip(input.positionWS);
    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);
    // 笔触跟随世界表面，保留扫描纹理、真实光照、法线、阴影与 AO。
    float3 wp = input.positionWS;
    float3 cell = floor(wp * float3(_GrainScale * 0.42, _GrainScale * 2.8, _GrainScale));
    float grain = frac(sin(dot(cell, float3(12.9898,78.233,39.425))) * 43758.5453);
    float brush = sin(wp.x * 3.7 + sin(wp.z * 2.1)) * sin(wp.y * 5.3 + wp.z * 1.7);
    float mottled = sin(wp.x * 1.71 + wp.y * 2.17 + sin(wp.z * 1.31));
    float dirt = saturate(1 - wp.y * 0.75) * (0.65 + mottled * 0.35);
    surfaceData.albedo *= 1 + _PigmentStrength * (brush * 0.13 + (grain - 0.5) * 0.11);
    surfaceData.albedo *= lerp(1, 0.64 + 0.14 * mottled, dirt * _WearStrength);
    surfaceData.smoothness = saturate(surfaceData.smoothness + (grain - 0.5) * _PigmentStrength * 0.16);
    surfaceData.normalTS.xy += float2(brush, grain - 0.5) * _PigmentStrength * 0.045;
    // 仅在工作器皿和抹布上启用干颜料，表面坐标固定，既不发光也不随屏幕漂移。
    float2 stainUV = input.uv * float2(15, 23);
    float2 stainCell = floor(stainUV);
    float stainSeed = frac(sin(dot(stainCell, float2(127.1,311.7))) * 43758.5453);
    float2 offset = frac(stainUV) - float2(.35 + stainSeed * .3,.5);
    float dist = length(offset * float2(1.2,1.7)) + sin(stainUV.x * 37 + stainUV.y * 23) * .07;
    float dots = (1-smoothstep(.12,.24,dist)) * step(.66,stainSeed);
    float drip = (1-smoothstep(.015,.04,abs(offset.x))) * smoothstep(.35,-.45,offset.y) * step(.94,stainSeed);
    // 粗尺度擦痕与细小飞溅共同构成干颜料，避免整张布只有等距黑点。
    float2 smearUV = input.uv * float2(3.3,5.2);
    float2 smearCell = floor(smearUV);
    float smearSeed = frac(sin(dot(smearCell,float2(83.17,219.31))) * 37129.29);
    float2 smearOffset = frac(smearUV) - float2(.42,.46);
    float smearDistance = length(smearOffset * float2(1.0,2.2)) + sin(input.uv.x * 139 + input.uv.y * 73) * .035;
    float dryGrain = smoothstep(.13,.68,frac(sin(dot(floor(input.uv * 410),float2(19.3,37.1))) * 21971.3));
    float smear = (1-smoothstep(.24,.49,smearDistance)) * step(.53,smearSeed) * lerp(.55,1,dryGrain);
    float stains = saturate(dots * .35 + drip * .4 + smear) * _PaintStains;
    float3 paintColor = lerp(_StainColor.rgb,float3(.12,.23,.34),step(.76,smearSeed));
    surfaceData.albedo = lerp(surfaceData.albedo,paintColor,stains);
    surfaceData.smoothness *= 1-stains*.72;

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    surfaceData.albedo=lerp(surfaceData.albedo,float3(.12,.24,.34),AtelierMark(input.positionWS)*.68);
    float lift=AtelierLift(input.positionWS);
    surfaceData.albedo=lerp(surfaceData.albedo,float3(.42,.40,.34),lift*.67);
    surfaceData.smoothness*=1-lift*.7;
    InputData inputData;
    // 双面薄布从室内看背面时翻转法线，局部暖灯与窗边冷光才能正确落在褶皱上。
    surfaceData.normalTS.z = IS_FRONT_VFACE(facing, surfaceData.normalTS.z, -surfaceData.normalTS.z);
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    InitializeBakedGIData(input, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent());

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
