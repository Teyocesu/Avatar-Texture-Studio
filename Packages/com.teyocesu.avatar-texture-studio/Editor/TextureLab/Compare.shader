Shader "Hidden/AvatarTextureStudio/Compare"
{
    Properties
    {
        _MainTex ("Preview", 2D) = "white" {}
        _TexA ("A", 2D) = "white" {}
        _TexB ("B", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _TexA, _TexB;
            float _Mode, _Opacity, _Gain, _RawValues, _AlphaOnly, _LinearProject, _SrgbA, _SrgbB;

            float4 Interpret(float4 value, float srgb)
            {
                if (_LinearProject > 0.5 && (_RawValues < 0.5 || srgb > 0.5))
                    value.rgb = LinearToGammaSpace(value.rgb);
                if (_AlphaOnly > 0.5) return float4(value.aaa, 1);
                return value;
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float4 a = tex2Dlod(_TexA, float4(i.uv, 0, 0));
                float4 b = tex2Dlod(_TexB, float4(i.uv, 0, 0));
                float alphaDifference = abs(a.a - b.a);
                a = Interpret(a, _SrgbA);
                b = Interpret(b, _SrgbB);
                float4 c = a;
                if (_Mode > 2.5)
                {
                    float3 difference = _AlphaOnly > 0.5 ? alphaDifference.xxx : max(abs(a.rgb - b.rgb), alphaDifference.xxx);
                    c = float4(saturate(difference * _Gain), 1);
                }
                else if (_Mode > 1.5)
                {
                    float alpha = b.a * _Opacity;
                    c.a = alpha + a.a * (1 - alpha);
                    c.rgb = c.a > 0 ? (b.rgb * alpha + a.rgb * a.a * (1 - alpha)) / c.a : 0;
                }
                else if (_Mode > 0.5) c = b;
                float2 cell = floor(i.pos.xy / 12);
                float checker = fmod(cell.x + cell.y, 2) < 1 ? 0.7 : 0.45;
                float3 display = lerp(checker.xxx, saturate(c.rgb), saturate(c.a));
                if (_LinearProject > 0.5) display = GammaToLinearSpace(display);
                return float4(display, 1);
            }
            ENDCG
        }
    }
}
