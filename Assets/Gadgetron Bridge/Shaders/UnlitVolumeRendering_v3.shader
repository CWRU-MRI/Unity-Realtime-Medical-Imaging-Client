Shader "VolumeRendering/UnlitVolumeRendering"
{
	Properties
	{
		_Volume("Volume", 3D) = "" {}
		_BlackCutoff("Black Cutoff", Range(0.0,1.0)) = 0.02
		_RaymarchAlpha("Raymarch Alpha", Range(0.0,1.0)) = 0.5
		_MaxSteps("Max Steps", Range(0,512)) = 64
	}

	CGINCLUDE
	sampler3D _Volume;
	half  _BlackCutoff, _RaymarchAlpha;
	float _MaxSteps;

	struct Ray {
		float3 origin;
		float3 dir;
	};

	struct AABB {
		float3 min;
		float3 max;
	};

	bool intersect(Ray r, AABB aabb, out float t0, out float t1)
	{
		float3 invR = 1.0 / r.dir;
		float3 tbot = invR * (aabb.min - r.origin);
		float3 ttop = invR * (aabb.max - r.origin);
		float3 tmin = min(ttop, tbot);
		float3 tmax = max(ttop, tbot);
		float2 t = max(tmin.xx, tmin.yz);
		t0 = max(t.x, t.y);
		t = min(tmax.xx, tmax.yz);
		t1 = min(t.x, t.y);
		return t0 <= t1;
	}

	float3 localize(float3 p) {
		return mul(unity_WorldToObject, float4(p, 1)).xyz;
	}

	float3 get_uv(float3 p) {
		return (p + 0.5);
	}

	bool outside(float3 uv) {
		const float EPSILON = 0.01;
		float lower = -EPSILON;
		float upper = 1 + EPSILON;
		return (
			uv.x < lower || uv.y < lower || uv.z < lower ||
			uv.x > upper || uv.y > upper || uv.z > upper
		);
	}

	struct appdata
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
	};

	struct v2f
	{
		float4 vertex : SV_POSITION;
		float2 uv : TEXCOORD0;
		float3 world : TEXCOORD1;
	};

	v2f vert(appdata v)
	{
		v2f o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = v.uv;
		o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
		return o;
	}

	ENDCG

	SubShader {
		Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

		Cull Back
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite On
		ZTest Less

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			fixed4 frag (v2f i) : SV_Target
			{
				Ray ray;
				ray.origin = localize(i.world);

				// world space direction to object space
				float3 dir = normalize(i.world - _WorldSpaceCameraPos);
				ray.dir = normalize(mul((float3x3)unity_WorldToObject, dir));

				AABB aabb;
				aabb.min = float3(-0.5, -0.5, -0.5);
				aabb.max = float3( 0.5,  0.5,  0.5);

				float tnear, tfar;
				intersect(ray, aabb, tnear, tfar);
				tnear = max(0.0, tnear);

				// float3 start = ray.origin + ray.dir * tnear;
				float3 start = ray.origin;
				float3 end = ray.origin + ray.dir * tfar; 
				float dist = abs(tfar - tnear); // float dist = distance(start, end);
				float step_size = dist / float(_MaxSteps);
				float3 ds = normalize(end - start) * step_size;
				float voxelAlpha = 0;
				float4 volumeValue = 0;

				float3 finalColor = 0;
				float3 p = end;	
						
				for (int iter = 0; iter < _MaxSteps; iter++) {
					float3 uv = get_uv(p);
					volumeValue = tex3D(_Volume, uv);
					// TODO: Implement using alphas provided by the volume texture - currently, only works with RGB not RGBA
					if ((volumeValue.r + volumeValue.g + volumeValue.b) > _BlackCutoff) {
						voxelAlpha = _RaymarchAlpha;
						finalColor = finalColor * (1.0f - voxelAlpha) + (volumeValue.rgb * voxelAlpha);
					}
					p -= ds;
				}
				if (length(finalColor) == 0) {
					return float4(0,0,0,0);
				}
				return float4(finalColor,1);
			}
			ENDCG
		}
	}
}
