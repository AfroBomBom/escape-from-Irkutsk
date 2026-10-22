Shader "Irkutsk/Exploration"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        _FogDynamic ("Hide outside current view", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_ExplorationMap); SAMPLER(sampler_ExplorationMap);
            float4 _ExplorationBounds;
            float _ExplorationEnabled;
            half4 _ExplorationDarkness;
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _FogDynamic;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.world);
                output.normal=TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=(input.world.xz-_ExplorationBounds.xy)/_ExplorationBounds.zw;
                half2 state=SAMPLE_TEXTURE2D(_ExplorationMap,sampler_ExplorationMap,uv).rg;
                if(any(uv<0) || any(uv>=1)) state=0;
                if(_ExplorationEnabled<.5) state=1;
                // Dynamic objects must not leave dark silhouettes in explored territory.
                if(_FogDynamic>.5) clip(state.g-.5);
                half lighting=.65+.35*saturate(dot(normalize(input.normal),normalize(half3(-.3,1,-.4))));
                half3 color=_BaseColor.rgb*lighting;
                half gray=dot(color,half3(.2126,.7152,.0722))*.48;
                half3 remembered=gray.xxx;
                half3 result=lerp(_ExplorationDarkness.rgb,remembered,state.r);
                result=lerp(result,color,state.g);
                return half4(result,1);
            }
            ENDHLSL
        }
    }
}
