Shader "WorldGen/VertexColor"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _AmbientStrength("Ambient Strength", Range(0, 1)) = 0.3
        _DiffuseStrength("Diffuse Strength", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        // ── ForwardBase Pass（主方向光）─────────────
        Pass
        {
            Name "ForwardBase"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 color  : COLOR;
                SHADOW_COORDS(2)
            };

            float _AmbientStrength;
            float _DiffuseStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.normal);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = saturate(dot(N, L));

                // 阴影衰减
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);

                // 环境光（使用 unity 内置球谐最低阶 + 自定义强度）
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * i.color.rgb * _AmbientStrength;

                // 漫反射
                fixed3 diffuse = i.color.rgb * _LightColor0.rgb * NdotL * atten * _DiffuseStrength;

                fixed3 finalColor = ambient + diffuse;
                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }

        // ── ForwardAdd Pass（额外光源）──────────────
        Pass
        {
            Name "ForwardAdd"
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd_fullshadows

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 color  : COLOR;
                SHADOW_COORDS(2)
            };

            float _DiffuseStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.normal);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = saturate(dot(N, L));

                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);

                fixed3 diffuse = i.color.rgb * _LightColor0.rgb * NdotL * atten * _DiffuseStrength;

                return fixed4(diffuse, 1.0);
            }
            ENDCG
        }

        // ── ShadowCaster Pass ──────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                V2F_SHADOW_CASTER;
            };

            v2f vert(appdata v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
