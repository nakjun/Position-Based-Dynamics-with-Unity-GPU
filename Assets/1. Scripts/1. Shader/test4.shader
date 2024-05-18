
Shader "Custom/ParticlePipeline"
{
	CGINCLUDE
			
	#include "UnityCG.cginc"
	
	struct ToFrag
	{
		float4 vertex : SV_POSITION;
	};
	
	StructuredBuffer<float3> positions;
	
	
	ToFrag Vert( uint vi : SV_VertexID )
	{
		ToFrag o;
		o.vertex = UnityObjectToClipPos( positions[vi], 1.0f );
		return o;
	}
	
	
	fixed4 Frag( ToFrag i ) : SV_Target
	{
		return fixed4( 1, 1, 0, 1 ); // Yellow
	}
	
	ENDCG
	

	SubShader
	{
		Tags { "RenderType"="Opaque" }
		
		Pass
		{
			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			ENDCG
		}
	}
}