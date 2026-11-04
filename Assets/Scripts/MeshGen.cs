using UnityEngine;
using Unity.Mathematics;
public class MeshGen : MonoBehaviour {
    public bool update;
    public bool showGradient;
    public enum MeshType {
        ConstantGradient,
        Terrain
    }
    public MeshType meshType;

    [Header("Mesh Properties")]
    public int meshSize;
    public Material terrainMat;
    [Header("Base Terrain Properties")]
    public float frequency;
    public float amplitude;
    public int octaves;
    public float power;
    [Header("Constant Gradient")]
    public Vector2 gradient;
    [Header("Erosion Properties")]
    public float erosionMagnitude;
    public float erosionFrequency;
    public int erosionLayers;
    public Vector2Int refPoint;
    
    public float ampFalloffClampMin;
    public float ampFalloffClampMax;
    public float bro;

    public bool offsetVertices;

    [Range(0,0.5f)]
    public float pivotOffset; //default to 0.5f
    [Header("Cell Properties")]
    public int cellSize;
    public float distanceWeight;

    Vector3[] vertices;
    Vector3[] normals;
    Vector2[] uvs;
    VertInfo[] points;
    int[] triangles;

    //testing purposes
    public float maxSlope;
    float maxErosion;


    void OnValidate() {
        maxSlope = 0;
        maxErosion = 0;

        if (update) {
            CreateMesh();
            update = false;
            print("done");
        }
    }

    void CreateMesh() {
        int i = 0;

        vertices = new Vector3[(meshSize + 1) * (meshSize + 1)];
        normals = new Vector3[vertices.Length];
        points = new VertInfo[vertices.Length];
        uvs = new Vector2[vertices.Length];
        triangles = new int[meshSize * meshSize * 6];

        for(int y = 0; y <= meshSize; y++) {
            for(int x = 0; x <= meshSize; x++) {
                VertInfo v = CalculateHeight(new Vector2(x, y));

                vertices[i] = v.pos;
                normals[i] = v.gradient;
                points[i] = v;

                uvs[i] = new Vector2((float)x/meshSize, (float)y/meshSize);

                i++;
            }
        }
        i = 0;
        for(int y = 0; y < meshSize; y++) {
            for(int x = 0; x < meshSize; x++) {
                triangles[i + 0] = x + 0 + (meshSize + 1) * (y + 0);
                triangles[i + 1] = x + 0 + (meshSize + 1) * (y + 1);
                triangles[i + 2] = x + 1 + (meshSize + 1) * (y + 0);
                triangles[i + 3] = x + 0 + (meshSize + 1) * (y + 1);
                triangles[i + 4] = x + 1 + (meshSize + 1) * (y + 1);
                triangles[i + 5] = x + 1 + (meshSize + 1) * (y + 0);

                i += 6;
            }
        }

        Mesh mesh = new Mesh {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        MeshCollider meshCollider = GetComponent<MeshCollider>();

        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = terrainMat;

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        meshCollider.sharedMesh = mesh;

        CreateTexture();
    }
    VertInfo CalculateHeight(Vector2 coord) {
        VertInfo v = (meshType == MeshType.ConstantGradient) ? ConstantGradient(coord) : BaseNoise(coord);
        
        v = Erosion(v, v.gradient, 0, erosionFrequency, erosionMagnitude);

        return v;
    }

    void CreateTexture() {
        Texture2D tex = new Texture2D(meshSize, meshSize);
        int i = 0;
        for(int y = 0; y <= meshSize; y++) {
            for(int x = 0; x <= meshSize; x++) {
                Vector3 c = normals[i];
                //c = GetCellOrigin(new Vector2(x,y)) * 0.01f;
                float m = 1 - (points[i].derivative.magnitude / maxSlope);
                float er = points[i].erosion / maxErosion;
                c = new Vector3(m,er,0f);

                tex.SetPixel(x, y, new Color(c.x, c.y, c.z, 1f));

                i++;
            }
        }
        tex.Apply();
        
        if(showGradient) terrainMat.mainTexture = tex;
    }

    //Height map calculations + analytical derivatives
    VertInfo BaseNoise(Vector2 coord) {
        VertInfo v = new VertInfo();

        float f = frequency;
        float a = amplitude;
        
        float simplex = 0;
        Vector2 g = Vector2.zero;

        for(int i = 0; i < octaves; i++){
            Vector3 s = noise.srdnoise(coord * f) * a;
            simplex += Mathf.Pow(s.x / a, power) * a;
            g += new Vector2(s.y, s.z) * f;

            f *= 2f;
            a /= 3f;
        }

        float height = simplex;

        v.pos = new Vector3(coord.x, height, coord.y);
        v.SetDerivative(g);

        if(g.magnitude > maxSlope) maxSlope = g.magnitude; //get max *test*

        return v;
    }

    VertInfo ConstantGradient(Vector2 coord) {
        VertInfo v = new VertInfo();

        Vector2 del = new Vector2(gradient.x, gradient.y);
        float height = coord.x * del.x + coord.y * del.y;

        v.pos = new Vector3(coord.x, height, coord.y);
        v.SetDerivative(del);

        return v;
    }

    VertInfo Erosion(VertInfo vert, Vector2 baseG, int layer, float f, float a) { //p is the coordinate of the sampled point
        if(layer >= erosionLayers) return vert;

        Vector2 g = vert.derivative;
        if(g.sqrMagnitude <= 1e-8f) return Erosion(vert, baseG, layer + 1, f * 2f, a * 0.35f);

        float layerCell = cellSize / f;

        Vector2 d = new Vector2(-g.y, g.x).normalized;

        Vector2 p = new Vector2(vert.pos.x, vert.pos.z) / layerCell;
        Vector2 ip = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
        
        float C = 0f;
        float S = 0f;

        float slope = Mathf.Pow((baseG.magnitude - ampFalloffClampMin) / (ampFalloffClampMax - ampFalloffClampMin), bro); //power falloff -- creates sharper peaks
        //float slope = (1 - Mathf.Pow(1 - (baseG.magnitude - ampFalloffClampMin) / (ampFalloffClampMax - ampFalloffClampMin), bro)) * ampFalloffClampMax;

        const float TAU = Mathf.PI * 2f;
        float ka = Smoothstep(slope, ampFalloffClampMin, ampFalloffClampMax, 0f, 1f);

        float W = 0f;
        for(int y = -2; y <= 2; y++) {
            for(int x = -2; x <= 2; x++) {
                Vector2 cell = ip + new Vector2(x, y);
                Vector2 c = cell + Vector2.one * 0.5f + Hash2(cell) * pivotOffset; //pivot point per cell
                Vector2 o = p - c;

                float w = Mathf.Exp(-2f * Vector2.Dot(o,o) * distanceWeight * distanceWeight);

                float phase = Vector2.Dot(o, d) * TAU;

                C += Mathf.Cos(phase) * w;
                S += Mathf.Sin(phase) * w;
                W += w;
            }
        }

        float mag = Mathf.Sqrt(C*C + S*S);
        float norm = Mathf.Max(mag, 0.3f * W);

        float stripe = C / norm;
        Vector2 dstripe = -S / norm * d * TAU / layerCell; //cell to world space

        VertInfo v = vert;

        float erosion = Mathf.Lerp(stripe * a, 1f, ka);

        if(offsetVertices){
            v.pos += Vector3.up * erosion;
            v.SetDerivative(v.derivative + dstripe * a);
        }

        v.erosion = erosion;

        if(v.erosion > maxErosion) maxErosion = v.erosion; //get max *test*

        return Erosion(v, baseG, layer + 1, f * 2f, a * 0.35f);
    }

    static Vector2 Hash2(Vector2 q){   // deterministic per cell, returns [-1, 1]
        float hx = Mathf.Sin(Vector2.Dot(q, new Vector2(127.1f, 311.7f))) * 43758.5453f;
        float hy = Mathf.Sin(Vector2.Dot(q, new Vector2(269.5f, 183.3f))) * 43758.5453f;
        return new Vector2(hx - Mathf.Floor(hx), hy - Mathf.Floor(hy)) * 2f - Vector2.one;
    }

    public Vector2 GetCellOrigin(Vector2 coord) {
        Vector2 cellPos = new Vector2(Mathf.FloorToInt(coord.x / cellSize), Mathf.FloorToInt(coord.y / cellSize)) * cellSize;
        cellPos += Vector2.one * cellSize / 2f;

        return cellPos;
    }

    static float Smoothstep(float x, float minIn, float maxIn, float minOut, float maxOut) {
        float y;

        if(x < minIn) y = minOut;
        else if(x > maxIn) y = maxOut;
        else {
            float u = (x - minIn) / (maxIn - minIn);
            y = u*u * (3f - 2f * u) * (maxOut - minOut) + minOut;
        }

        return y;

        //https://en.wikipedia.org/wiki/Smoothstep
    }
}

public struct VertInfo {
    public Vector3 pos;
    public Vector2 derivative;
    public Vector3 gradient;
    public float erosion; //test
    public VertInfo(Vector3 pos, Vector2 derivative, float erosion) {
        this.pos = pos;
        this.derivative = derivative;
        gradient = Vector3.zero;
        this.erosion = erosion;
    }

    public void SetDerivative(Vector2 del) {
        derivative = del;
        gradient = new Vector3(del.x, del.x * del.x + del.y * del.y, del.y);
    }
}
