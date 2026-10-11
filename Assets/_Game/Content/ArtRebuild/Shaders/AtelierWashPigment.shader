// 文件：AtelierWashPigment.shader。职责：复用原有高阶平流，洗笔仅从实际笔尖位置有限注色。
// 算法来源：Pavel Dobryakov MIT 半拉格朗日实现与 GPU Gems 3 的高阶平流思想，许可随资源保存。
Shader "Hidden/RoomDemo/Atelier/WashPigment"
{
    Properties
    {
        _MainTex("平流输入",2D)="black"{}
        _OldPigment("上一步颜料",2D)="black"{}
        _Backward("反向结果",2D)="black"{}
        _FeedPigment("上游颜料",2D)="black"{}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #pragma target 4.5
        #include "UnityCG.cginc"
        #define PIGMENT_SIMULATION
        sampler2D _FeedPigment;
        #include "../../TechnicalArt/LivingMatter/Shaders/PigmentField.hlsl"
        sampler2D _MainTex,_OldPigment,_Backward;
        float4 _MainTex_TexelSize;
        float _Delta;
        float2 Trace(float2 uv,float dt)
        {
            float2 mid=uv-Velocity(uv)*dt*.5;
            float2 p=uv-Velocity(mid)*dt;
            if(_Mode>.5 && _Mode<1.5)
            {
                float2 d=p-.5;
                if(length(d)>.475) p=.5+normalize(d)*.475;
            }
            return clamp(p,_MainTex_TexelSize.xy*.5,1-_MainTex_TexelSize.xy*.5);
        }
        float4 Init(v2f_img i):SV_Target { return InitialPigment(i.uv); }
        float4 Advect(v2f_img i):SV_Target { return tex2D(_MainTex,Trace(i.uv,_Delta)); }
        float4 Correct(v2f_img i):SV_Target
        {
            float2 uv=i.uv;
            float4 result=tex2D(_MainTex,uv)+.5*(tex2D(_OldPigment,uv)-tex2D(_Backward,uv));
            float2 old=Trace(uv,_Delta),ts=_MainTex_TexelSize.xy;
            float2 base=(floor(old/ts-.5)+.5)*ts;
            float4 a=tex2D(_OldPigment,base),b=tex2D(_OldPigment,base+float2(ts.x,0));
            float4 c=tex2D(_OldPigment,base+float2(0,ts.y)),d=tex2D(_OldPigment,base+ts);
            result=clamp(result,min(min(a,b),min(c,d)),max(max(a,b),max(c,d)));
            if(_Mode>1.5)
            {
                // 桌面湿膜进行少量邻域扩散；主范围仍由历史状态与表面速度推进。
                float4 average=(tex2D(_MainTex,uv+float2(ts.x,0))+tex2D(_MainTex,uv-float2(ts.x,0))+
                    tex2D(_MainTex,uv+float2(0,ts.y))+tex2D(_MainTex,uv-float2(0,ts.y)))*.25;
                result=lerp(result,average,.12);
            }
            // 洗笔结束后不再注入或衰减，只通过原有高阶平流搬运有限颜料。
            float2 tip=uv-float2(.70,.54);
            float amount=exp(-dot(tip,tip)/.00058)*_Injection*_Delta*3.4;
            return saturate(result+float4(amount,amount*.38,amount*.12,0));
        }
        ENDHLSL
        Pass { Name "初始颜料" HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Init
            ENDHLSL }
        Pass { Name "反追踪平流" HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Advect
            ENDHLSL }
        Pass { Name "高阶校正与限制器" HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Correct
            ENDHLSL }
    }
}
