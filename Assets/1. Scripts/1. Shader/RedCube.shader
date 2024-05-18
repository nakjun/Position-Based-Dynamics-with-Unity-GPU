Shader "Custom/RedCube" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            StructuredBuffer<float3> positions; // Compute Shader에서 계산된 위치 데이터

            struct appdata {
                uint vertexID : SV_VertexID;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v) {
                v2f o;
                float3 vertexPosition = positions[v.vertexID]; // Vertex 위치 업데이트
                o.vertex = UnityObjectToClipPos(float4(vertexPosition, 1.0));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // 빨간색으로 채우기
                return fixed4(1, 0, 0, 1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
