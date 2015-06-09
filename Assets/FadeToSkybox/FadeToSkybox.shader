Shader "Hidden/FadeToSkybox"
{
    Properties
    {
        _MainTex ("-", 2D) = "black" {}
        _SkyTint ("-", Color) = (.5, .5, .5, .5)
        [Gamma] _SkyExposure ("-", Range(0, 8)) = 1.0
        [NoScaleOffset] _SkyCubemap ("-", Cube) = "grey" {}
    }

    CGINCLUDE

    #include "UnityCG.cginc"

    #pragma multi_compile FOG_LINEAR FOG_EXP FOG_EXP2
    #pragma multi_compile Z_DIST RADIAL_DIST

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    sampler2D_float _CameraDepthTexture;

    float _DistanceOffset;
    float4 _SceneFogParams;

    // for fast world space reconstruction
    uniform float4x4 _FrustumCornersWS;
    uniform float4 _CameraWS;

    samplerCUBE _SkyCubemap;
    half4 _SkyCubemap_HDR;

    half4 _SkyTint;
    half _SkyExposure;
    float _SkyRotation;

    float4 RotateAroundYInDegrees (float4 vertex, float degrees)
    {
        float alpha = degrees * UNITY_PI / 180.0;
        float sina, cosa;
        sincos(alpha, sina, cosa);
        float2x2 m = float2x2(cosa, -sina, sina, cosa);
        return float4(mul(m, vertex.xz), vertex.yw).xzyw;
    }

    struct v2f {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float2 uv_depth : TEXCOORD1;
        float4 interpolatedRay : TEXCOORD2;
    };

    v2f vert (appdata_img v)
    {
        v2f o;
        half index = v.vertex.z;
        v.vertex.z = 0.1;
        o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
        o.uv = v.texcoord.xy;
        o.uv_depth = v.texcoord.xy;

        #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0)
            o.uv.y = 1-o.uv.y;
        #endif

        o.interpolatedRay = RotateAroundYInDegrees(_FrustumCornersWS[(int)index], -_SkyRotation);
        o.interpolatedRay.w = index;

        return o;
    }

    // Applies one of standard fog formulas, given fog coordinate (i.e. distance)
    half ComputeFogFactor (float coord)
    {
        float fogFac = 0.0;
        #if FOG_LINEAR
        // factor = (end-z)/(end-start) = z * (-1/(end-start)) + (end/(end-start))
        fogFac = coord * _SceneFogParams.z + _SceneFogParams.w;
        #elif FOG_EXP
        // factor = exp(-density*z)
        fogFac = _SceneFogParams.y * coord; fogFac = exp2(-fogFac);
        #else // FOG_EXP2
        // factor = exp(-(density*z)^2)
        fogFac = _SceneFogParams.x * coord; fogFac = exp2(-fogFac*fogFac);
        #endif
        return saturate(fogFac);
    }

    // Distance-based fog
    float ComputeDistance (float3 camDir, float zdepth)
    {
        float dist;
        #if RADIAL_DIST
        dist = length(camDir);
        #else // Z_DIST
        dist = zdepth * _ProjectionParams.z;
        #endif
        // Built-in fog starts at near plane, so match that by
        // subtracting the near value. Not a perfect approximation
        // if near plane is very large, but good enough.
        dist -= _ProjectionParams.y;
        return dist;
    }

    half4 frag(v2f i) : SV_Target
    {
        half4 sceneColor = tex2D(_MainTex, i.uv);

        // Reconstruct world space position & direction
        // towards this screen pixel.
        float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.uv_depth);
        float dpth = Linear01Depth(rawDepth);
        float4 wsDir = dpth * i.interpolatedRay;
        float4 wsPos = _CameraWS + wsDir;

        half4 skyTex = texCUBE (_SkyCubemap, wsDir);
        half3 skyColor = DecodeHDR (skyTex, _SkyCubemap_HDR);
        skyColor *= _SkyTint.rgb * unity_ColorSpaceDouble;
        skyColor *= _SkyExposure;

        // Compute fog distance
        float g = ComputeDistance(wsDir, dpth) - _DistanceOffset;

        // Compute fog amount
        half fogFac = ComputeFogFactor (max(0.0,g));
        // Do not fog skybox
        if (rawDepth >= 0.999999)
            fogFac = 1.0;

        // Lerp between fog color & original scene color
        // by fog amount
        return lerp (half4(skyColor, 1), sceneColor, fogFac);
    }

    ENDCG

    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    }
    Fallback off
}
