#include "Packages/jp.keijiro.noiseshader/Shader/SimplexNoise2D.hlsl"

float GetLighting(float3 normal, float3 lightDir) {
    return clamp(dot(normal, lightDir), 0.075, 1);
}

float3 GetNormal(float3 snoise) {
    float2 g = snoise.xy;

    float3 gradient = float3(g.x, g.x*g.x + g.y*g.y, g.y);
    float3 d = normalize(float3(-g.y, 0.0, g.x)); //perpendicular vector tangent to terrain

    float3 normal = normalize(cross(d, gradient));

    return normal;
}

float3 GetNoise(float2 p) {
    float f = _BaseFrequency;
    float a = _BaseAmplitude;

    float w = 0;
    float3 snoise = float3(0,0,0);

    for(int i = 0; i < _Octaves; i++) {
        float3 s = SimplexNoiseGrad(p.xy * f) * a; //x, y: partial derivatives; z: height
        s.xy *= f; //chain rule;

        snoise += s;
        w += a;

        f *= 2.0;
        a /= 2.0;
    }
    snoise / w;
    return snoise;
}

Varyings Erosion(float2 p) {
    Varyings OUT;
    
    float3 snoise = GetNoise(p);

    float h = snoise.z;
    float3 normal = GetNormal(snoise);
    
    float3 pos = float3(p.x, h, p.y);

    OUT.positionHCS = TransformObjectToHClip(pos);
    OUT.normalWS = normal;

    return OUT;
}