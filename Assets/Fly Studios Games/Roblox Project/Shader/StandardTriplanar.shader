Shader "Custom/StandardTriplanar"
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
        // Folosim modelul de iluminare 'Standard' (PBR) care suporta umbre
        // 'fullforwardshadows' asigura ca primeste umbre corect de la toate luminile
        #pragma surface surf Standard fullforwardshadows

        // Shader Model 3.0 pentru compatibilitate buna
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _Tiling;

        // Functia principala
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // 1. Calculam UV-urile pentru cele 3 planuri (X, Y, Z) bazat pe pozitia in lume
            float2 uvX = IN.worldPos.zy * _Tiling;
            float2 uvY = IN.worldPos.xz * _Tiling;
            float2 uvZ = IN.worldPos.xy * _Tiling;

            // 2. Citim textura de 3 ori
            fixed4 colX = tex2D (_MainTex, uvX);
            fixed4 colY = tex2D (_MainTex, uvY);
            fixed4 colZ = tex2D (_MainTex, uvZ);

            // 3. Calculam cat de mult se vede fiecare fata (Blending)
            // abs() transforma normalele negative in pozitive
            float3 blend = abs(IN.worldNormal);
            // Normalizam ca suma lor sa fie 1
            blend /= dot(blend, 1.0);

            // 4. Combinam culorile
            fixed4 finalColor = colX * blend.x + colY * blend.y + colZ * blend.z;

            // 5. Aplicam culoarea de baza (Tint)
            finalColor *= _Color;

            // 6. Trimitem datele la placa video
            o.Albedo = finalColor.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = finalColor.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}