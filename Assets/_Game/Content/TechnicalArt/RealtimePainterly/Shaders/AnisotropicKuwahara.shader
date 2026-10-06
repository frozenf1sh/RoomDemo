// 文件：AnisotropicKuwahara.shader。职责：复用 MIT 的张量、特征向量与八扇区 Kuwahara。
// 边界：只处理摄像机颜色；URP RenderGraph 接入、浮点张量、HDR 和深度保护由本项目适配。
// 原始算法代码 Copyright (c) 2022 Garrett Gunnell，另见 Assets/ThirdParty/PainterlyRendering。
// 参数命名与数值保护参考 Josephy5，Copyright (c) 2024 Josephy5。
Shader "Hidden/RoomDemo/RealtimePainterly/Kuwahara"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        TEXTURE2D_X(_TFM);
        int _KernelSize, _N;
        float _Hardness, _Q, _Alpha, _ZeroCrossing, _Zeta;
        float gaussian(float sigma, float pos)
        {
            return exp(-(pos*pos)/(2.0*sigma*sigma));
        }
        ENDHLSL
        Pass
        {
            Name "StructureTensor"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 d = _BlitTexture_TexelSize.xy;

                float3 Sx = (
                    1.0f * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + float2(-d.x, -d.y)).rgb +
                    2.0f * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + float2(-d.x,  0.0)).rgb +
                    1.0f * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + float2(-d.x,  d.y)).rgb +
                    -1.0f * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + float2(d.x, -d.y)).rgb +
                    -2.0f * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + float2(d.x,  0.0)).rgb +
                    -1.0f * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + float2(d.x,  d.y)).rgb
                ) / 4.0f;

                float3 Sy = (
                    1.0f * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + float2(-d.x, -d.y)).rgb +
                    2.0f * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + float2( 0.0, -d.y)).rgb +
                    1.0f * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + float2( d.x, -d.y)).rgb +
                    -1.0f * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + float2(-d.x, d.y)).rgb +
                    -2.0f * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + float2( 0.0, d.y)).rgb +
                    -1.0f * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + float2( d.x, d.y)).rgb
                ) / 4.0f;

                
                return float4(dot(Sx, Sx), dot(Sy, Sy), dot(Sx, Sy), 1.0f);
            }
            ENDHLSL
        }
        Pass
        {
            Name "TensorHorizontal"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                int kernelRadius = 5;

                float4 col = 0;
                float kernelSum = 0.0f;

                for (int x = -kernelRadius; x <= kernelRadius; ++x) {
                    float4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + float2(x, 0) * _BlitTexture_TexelSize.xy);
                    float gauss = gaussian(2.0f, x);

                    col += c * gauss;
                    kernelSum += gauss;
                }

                return col / kernelSum;
            }
            ENDHLSL
        }
        Pass
        {
            Name "TensorEigenvectors"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                int kernelRadius = 5;

                float4 col = 0;
                float kernelSum = 0.0f;

                for (int y = -kernelRadius; y <= kernelRadius; ++y) {
                    float4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + float2(0, y) * _BlitTexture_TexelSize.xy);
                    float gauss = gaussian(2.0f, y);

                    col += c * gauss;
                    kernelSum += gauss;
                }

                float3 g = col.rgb / kernelSum;

                float lambda1 = 0.5f * (g.y + g.x + sqrt(g.y * g.y - 2.0f * g.x * g.y + g.x * g.x + 4.0f * g.z * g.z));
                float lambda2 = 0.5f * (g.y + g.x - sqrt(g.y * g.y - 2.0f * g.x * g.y + g.x * g.x + 4.0f * g.z * g.z));

                float2 v = float2(lambda1 - g.x, -g.z);
                float2 t = length(v) > 1e-5 ? normalize(v) : float2(0.0f, 1.0f);
                float phi = -atan2(t.y, t.x);

                float A = (lambda1 + lambda2 > 1e-6f) ? (lambda1 - lambda2) / (lambda1 + lambda2) : 0.0f;
                
                return float4(t, phi, A);
            }
            ENDHLSL
        }
        Pass
        {
            Name "AnisotropicKernel"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float centreZ = LinearEyeDepth(SampleSceneDepth(i.texcoord), _ZBufferParams);
                float alpha = _Alpha;
                float4 t = SAMPLE_TEXTURE2D_X(_TFM, sampler_LinearClamp, i.texcoord);

                int kernelRadius = max(1, _KernelSize >> 1);
                float a = float((kernelRadius)) * clamp((alpha + t.w) / alpha, 0.1f, 2.0f);
                float b = float((kernelRadius)) * clamp(alpha / (alpha + t.w), 0.1f, 2.0f);
                
                float cos_phi = cos(t.z);
                float sin_phi = sin(t.z);

                float2x2 R = {cos_phi, -sin_phi,
                              sin_phi, cos_phi};

                float2x2 S = {0.5f / a, 0.0f,
                              0.0f, 0.5f / b};

                float2x2 SR = mul(S, R);

                int max_x = int(sqrt(a * a * cos_phi * cos_phi + b * b * sin_phi * sin_phi));
                int max_y = int(sqrt(a * a * sin_phi * sin_phi + b * b * cos_phi * cos_phi));

                //float zeta = 2.0f / (kernelRadius);
                float zeta = _Zeta;

                float zeroCross = _ZeroCrossing;
                float sinZeroCross = sin(zeroCross);
                float eta = (zeta + cos(zeroCross)) / (sinZeroCross * sinZeroCross);
                int k;
                float4 m[8];
                float3 s[8];

                for (k = 0; k < _N; ++k) {
                    m[k] = 0.0f;
                    s[k] = 0.0f;
                }

                [loop]
                for (int y = -max_y; y <= max_y; ++y) {
                    [loop]
                    for (int x = -max_x; x <= max_x; ++x) {
                        float2 v = mul(SR, float2(x, y));
                        if (dot(v, v) <= 0.25f) {
                            // 不让椭圆核把墙面颜色拖到叶片和物体轮廓内。
                            float2 sampleUV = i.texcoord + float2(x, y) * _BlitTexture_TexelSize.xy;
                            float sampleZ = LinearEyeDepth(SampleSceneDepth(sampleUV), _ZBufferParams);
                            if (abs(sampleZ - centreZ) > max(0.08, centreZ * 0.012)) continue;
                            float3 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, i.texcoord + float2(x, y) * _BlitTexture_TexelSize.xy, 0).rgb;
                            // 保留 HDR 颜色；最终由 URP 色调映射。
                            float sum = 0;
                            float w[8];
                            float z, vxx, vyy;
                            
                            /* Calculate Polynomial Weights */
                            vxx = zeta - eta * v.x * v.x;
                            vyy = zeta - eta * v.y * v.y;
                            z = max(0, v.y + vxx); 
                            w[0] = z * z;
                            sum += w[0];
                            z = max(0, -v.x + vyy); 
                            w[2] = z * z;
                            sum += w[2];
                            z = max(0, -v.y + vxx); 
                            w[4] = z * z;
                            sum += w[4];
                            z = max(0, v.x + vyy); 
                            w[6] = z * z;
                            sum += w[6];
                            v = sqrt(2.0f) / 2.0f * float2(v.x - v.y, v.x + v.y);
                            vxx = zeta - eta * v.x * v.x;
                            vyy = zeta - eta * v.y * v.y;
                            z = max(0, v.y + vxx); 
                            w[1] = z * z;
                            sum += w[1];
                            z = max(0, -v.x + vyy); 
                            w[3] = z * z;
                            sum += w[3];
                            z = max(0, -v.y + vxx); 
                            w[5] = z * z;
                            sum += w[5];
                            z = max(0, v.x + vyy); 
                            w[7] = z * z;
                            sum += w[7];
                            
                            float g = exp(-3.125f * dot(v,v)) / sum;
                            
                            for (int k = 0; k < 8; ++k) {
                                float wk = w[k] * g;
                                m[k] += float4(c * wk, wk);
                                s[k] += c * c * wk;
                            }
                        }
                    }
                }

                float4 output = 0;
                for (k = 0; k < _N; ++k) {
                    m[k].w = max(m[k].w, 1e-6);
                    m[k].rgb /= m[k].w;
                    s[k] = abs(s[k] / m[k].w - m[k].rgb * m[k].rgb);

                    float sigma2 = s[k].r + s[k].g + s[k].b;
                    float w = 1.0f / (1.0f + pow(max(_Hardness * 1000.0f * sigma2, 1e-6), max(0.5f * _Q, 0.01)));

                    output += float4(m[k].rgb * w, w);
                }
                
                return float4(output.rgb / max(output.w, 1e-6), 1);
            }
            ENDHLSL
        }
    }
}
