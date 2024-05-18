Shader "Custom/pipelineshader" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass {
            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct VertexData {
                float3 position;
            };

            struct appdata {
                float4 vertex : POSITION;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            RWStructuredBuffer<VertexData> positions : register(u1);

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                float2 uv = mul(unity_ObjectToWorld, v.vertex).xz * 0.1;
                o.uv = TRANSFORM_TEX(uv, _MainTex);

                // Modify the position of the vertex based on the Compute Shader output.
                uint id = v.vertex.x; // Assumes the vertex ID matches the index in the buffer.
                o.vertex.xyz += mul(unity_ObjectToWorld, float4(positions[id].position, 1)).xyz - v.vertex.xyz;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // Sample the main texture.
                fixed4 col = tex2D(_MainTex, i.uv);

                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
