// 文件：刀面收色.shader。职责：沿已确认的有限刀面路径把附近已有颜料推为薄带。
// 边界：只读实际作品RT，不替换成品图，不改变原始笔印或领域颜料账本。
Shader "RoomDemo/Atelier/CanvasSpread"
{
    Properties { _MainTex("原作品",2D)="black"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _SpreadPath[64];
            int _SpreadCount;
            half4 frag(v2f_img i):SV_Target
            {
                half4 original=tex2D(_MainTex,i.uv);
                float influence=0;
                float2 direction=float2(1,0);
                for(int n=0;n<_SpreadCount;n++)
                {
                    float2 a=_SpreadPath[n].xy,b=_SpreadPath[n].zw,d=b-a;
                    float t=saturate(dot(i.uv-a,d)/max(.00001,dot(d,d)));
                    float mask=1-smoothstep(.008,.034,length(i.uv-lerp(a,b,t)));
                    if(mask>influence) { influence=mask;direction=normalize(d+float2(.00001,0)); }
                }
                half3 pushed=tex2D(_MainTex,saturate(i.uv-direction*.012)).rgb*.5
                    +tex2D(_MainTex,saturate(i.uv-direction*.025)).rgb*.3
                    +tex2D(_MainTex,saturate(i.uv+direction*.008)).rgb*.2;
                return half4(lerp(original.rgb,pushed,influence*.62),1);
            }
            ENDHLSL
        }
    }
}
