// 太さのある線を描くためだけのシェーダ。
//
// 頂点の位置は線の端点そのもので、太さぶんの広がりは頂点シェーダの中で
// スクリーン空間へ出してから付ける。CPU 側で四角形に広げると、
// カメラごとに向きを変えられず、シーンビューとゲームビューで太さの向きが食い違う。
//
// LightMode タグを付けず、検証済みの URP では SRPDefaultUnlit として描く。
Shader "Hidden/StudioGaku/DrawLines"
{
    Properties
    {
        // 4 = LEqual（手前のものに隠れる）、8 = Always（常に最前面）。
        _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            ZTest [_ZTest]
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;   // この頂点が乗っている端点
                float4 other  : TEXCOORD0;  // xyz = 反対側の端点、w = 符号つきの半太さ（ピクセル）
                float4 color  : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 clipSelf  = UnityObjectToClipPos(v.vertex);
                float4 clipOther = UnityObjectToClipPos(float4(v.other.xyz, 1.0));

                // w で割ってスクリーン座標へ。カメラの裏に回った端点で 0 除算しないよう下限を置く。
                float2 screenSelf  = clipSelf.xy  / max(abs(clipSelf.w),  1e-5) * _ScreenParams.xy;
                float2 screenOther = clipOther.xy / max(abs(clipOther.w), 1e-5) * _ScreenParams.xy;

                float2 direction = screenOther - screenSelf;
                float length2 = length(direction);

                // 長さ 0 の線（同じ点を 2 回渡された場合）は向きが決まらない。適当な向きに倒して潰す。
                direction = length2 > 1e-5 ? direction / length2 : float2(1.0, 0.0);

                float2 normal = float2(-direction.y, direction.x);

                // ピクセル単位の広がりをクリップ空間へ。1 ピクセル = NDC で 2 / 画面幅、NDC からクリップは w 倍。
                clipSelf.xy += normal * v.other.w * 2.0 / _ScreenParams.xy * clipSelf.w;

                o.pos = clipSelf;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }

    Fallback Off
}
