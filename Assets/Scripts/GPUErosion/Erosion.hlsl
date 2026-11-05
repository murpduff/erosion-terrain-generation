#include "Packages/jp.keijiro.noiseshader/Shader/SimplexNoise2D.hlsl"

float GetLighting(float3 normal, float3 lightDir) {
    return clamp(dot(normal, lightDir), 0.075, 1);
}

float3 GetNormal(float2 g) {
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

VertInfo Erosion(VertInfo v, float f, float a) {
    VertInfo vert = v;

    float layerCell = _CellSize / f;
    float2 p = v.pos.xz / layerCell;
    float2 ip = floor(p);

    float2 g = v.gradient;
    float2 d = normalize(float2(-g.y, g.x));

    const float TAU = 2 * 3.14159265;

    float W = 0;
    float stripe = 0;
    float2 gradient = float2(0, 0);
    for(int y = -2; y <= 2; y++)
    for(int x = -2; x <= 2; x++) {
        float2 c = ip + float2(x, y) + float2(0.5, 0.5); //pivot point, set to the center of each cell
        float2 o = p - c;

        float w = exp(-dot(o,o));
        float phase = dot(o, d) * TAU;
        
        stripe += cos(phase) * a * w;
        gradient += -TAU * a * sin(phase) * d / layerCell * w; //chain rule

        W += w;
    }

    stripe /= W;
    gradient /= W;

    vert.pos += float3(0, stripe, 0);
    vert.gradient += gradient;

    return vert;
}

Varyings GetVertexInfo(float2 p) {
    Varyings OUT;
    
    float3 snoise = GetNoise(p);

    float h = snoise.z;
    float3 normal = GetNormal(snoise.xy);
    
    float3 pos = float3(p.x, h, p.y);

    VertInfo vert;
    vert.pos = pos;
    vert.gradient = snoise.xy;

    for(int i = 0; i < _ErosionLayers; i++) {
        float f = _ErosionFrequency * pow(2.0, i);
        float a = _ErosionMagnitude * pow(0.5, i);
        vert = Erosion(vert, f, a);
    }

    OUT.positionWS = vert.pos;
    OUT.positionHCS = TransformObjectToHClip(vert.pos);
    OUT.normalWS = GetNormal(vert.gradient);

    return OUT;
}