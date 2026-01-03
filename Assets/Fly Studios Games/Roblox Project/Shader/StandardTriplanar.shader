Shader "Custom/StandardTriplanar_Local"
{
    Properties
    {
        _MainTex ("Texture (RGB)", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _Tiling ("Tiling Scale", Float) = 0.5
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Adăugăm 'instancing' pentru a permite culori unice pe obiecte diferite
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma multi_compile_instancing
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        // Mutăm variabilele într-un bloc de instanțiere pentru a fi unice per obiect
        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
            UNITY_DEFINE_INSTANCED_PROP(half, _Glossiness)
            UNITY_DEFINE_INSTANCED_PROP(half, _Metallic)
        UNITY_INSTANCING_BUFFER_END(Props)

        float _Tiling;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 uvX = IN.worldPos.zy * _Tiling;
            float2 uvY = IN.worldPos.xz * _Tiling;
            float2 uvZ = IN.worldPos.xy * _Tiling;

            fixed4 colX = tex2D (_MainTex, uvX);
            fixed4 colY = tex2D (_MainTex, uvY);
            fixed4 colZ = tex2D (_MainTex, uvZ);

            float3 blend = abs(IN.worldNormal);
            blend /= dot(blend, 1.0);

            fixed4 finalColor = colX * blend.x + colY * blend.y + colZ * blend.z;

            // Citim culoarea unică a obiectului din buffer-ul de instanțiere
            fixed4 c = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
            finalColor *= c;

            o.Albedo = finalColor.rgb;
            o.Metallic = UNITY_ACCESS_INSTANCED_PROP(Props, _Metallic);
            o.Smoothness = UNITY_ACCESS_INSTANCED_PROP(Props, _Glossiness);
            o.Alpha = finalColor.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}