using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Lava lamp wax that rises, sinks, and merges or splits wherever blobs touch.
/// The blobs are metaballs: every frame a Burst job samples their combined "wax density" on a small grid
/// inside the glass and turns it into one mesh (marching tetrahedra). The jobs run on worker threads
/// between Update and LateUpdate, so the main thread only pays for the mesh upload.
/// </summary>
public class LavaLamp : MonoBehaviour
{
    [Header("Wax")]
    [Tooltip("Renders the generated wax mesh.")]
    [SerializeField] MeshFilter waxFilter;
    [Tooltip("Number of moving blobs (the resting pool at the bottom comes on top of this).")]
    [SerializeField, Range(1, 12)] int blobCount = 6;
    [Tooltip("Random diameter range for the blobs, in meters.")]
    [SerializeField] Vector2 blobDiameter = new Vector2(0.024f, 0.042f);
    [Tooltip("Seconds for one full rise-and-sink cycle (picked randomly per blob).")]
    [SerializeField] Vector2 cycleDuration = new Vector2(16f, 30f);
    [Tooltip("How much blobs stretch vertically while moving fastest (0 = never).")]
    [SerializeField, Range(0f, 1f)] float stretch = 0.45f;
    [Tooltip("How fast blobs drift around the lamp's axis, in degrees per second.")]
    [SerializeField] float swirlSpeed = 6f;
    [Tooltip("Change this to get a different arrangement of blobs.")]
    [SerializeField] int randomSeed = 1234;
    [Tooltip("Wax resting on the bottom of the glass: (radius, height) in meters. Rising blobs pull away from it.")]
    [SerializeField] Vector2 poolSize = new Vector2(0.036f, 0.011f);

    [Header("Quality")]
    [Tooltip("Grid cell size in meters. Smaller = smoother wax but more CPU time. 5 mm suits Quest 2.")]
    [SerializeField, Range(0.003f, 0.01f)] float cellSize = 0.005f;

    [Header("Glass shape (in this object's local space)")]
    [SerializeField] float glassBottom = 0.14f;
    [SerializeField] float glassTop = 0.42f;
    [Tooltip("Outer glass radius over normalized height (0 = bottom of the glass, 1 = top).")]
    [SerializeField] AnimationCurve glassRadius = AnimationCurve.Linear(0f, 0.045f, 1f, 0.032f);
    [Tooltip("Gap kept between the blobs' paths and the glass wall.")]
    [SerializeField] float wallPadding = 0.006f;

    // Density value at which the wax surface sits.
    const float Threshold = 0.5f;
    // Wax is kept this far inside the outer glass surface (glass thickness + shader wobble).
    const float WallClearance = 0.005f;
    // Wax flattens against the glass over this many grid cells (more than one, or the edge gets stair-stepped).
    const float WallSoftnessCells = 2f;
    // With the (1 - q)^3 falloff, a lone blob's surface sits at this fraction of its influence radius.
    static readonly float SurfaceFraction = math.sqrt(1f - math.pow(Threshold, 1f / 3f));

    const MeshUpdateFlags UploadFlags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
    static readonly VertexAttributeDescriptor[] VertexLayout =
    {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
        new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
    };

    struct BlobState
    {
        public float diameter, period, phase, startAngle, swirlDirection, radial;
    }

    struct Ellipsoid
    {
        public float3 center;
        public float3 inverseInfluence; // 1 / influence radius, per axis
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WaxVertex
    {
        public float3 position;
        public float3 normal;
    }

    BlobState[] states;
    NativeArray<Ellipsoid> ellipsoids;
    NativeArray<float> layerRadius; // inner glass radius at each grid layer
    NativeArray<float> density;
    NativeList<WaxVertex> vertices;
    NativeList<int> indices;
    NativeHashMap<long, int> edgeToVertex;
    int3 samples;
    float3 gridOrigin;
    Bounds waxBounds;
    bool needsRebuild = true;

    JobHandle job;
    bool jobRunning;
    Mesh runtimeMesh;
    int vertexCapacity, indexCapacity;

    /// <summary>Vertices and triangles in the most recently generated wax mesh.</summary>
    public int LastVertexCount { get; private set; }
    public int LastTriangleCount { get; private set; }

    void OnEnable()
    {
        Allocate();
        runtimeMesh = new Mesh { name = "LavaWax (runtime)" };
        runtimeMesh.MarkDynamic();
        vertexCapacity = indexCapacity = 0;
        if (waxFilter != null) waxFilter.sharedMesh = runtimeMesh;
    }

    void OnDisable()
    {
        job.Complete();
        jobRunning = false;
        Dispose();
        if (runtimeMesh != null) Destroy(runtimeMesh);
    }

    void OnValidate()
    {
        states = null;
        needsRebuild = true;
    }

    void Update()
    {
        if (!vertices.IsCreated) return;
        Schedule(Time.time);
        jobRunning = true;
    }

    void LateUpdate()
    {
        if (!jobRunning) return;
        job.Complete();
        jobRunning = false;
        Upload(runtimeMesh, ref vertexCapacity, ref indexCapacity, false);
    }

    /// <summary>
    /// Generates the wax for a given time straight into <paramref name="target"/>, e.g. for an editor preview
    /// or a baked mesh. Works outside Play mode; allocates and frees its own buffers when needed.
    /// </summary>
    public void BakeMesh(float time, Mesh target)
    {
        bool hadBuffers = vertices.IsCreated;
        if (!hadBuffers) Allocate();
        Schedule(time);
        job.Complete();
        int v = 0, i = 0;
        Upload(target, ref v, ref i, true);
        if (!hadBuffers) Dispose();
    }

    void Schedule(float time)
    {
        job.Complete();
        if (needsRebuild)
        {
            Dispose();
            Allocate();
        }
        UpdateEllipsoids(time);

        var densityJob = new DensityJob
        {
            blobs = ellipsoids, layerRadius = layerRadius, samples = samples, origin = gridOrigin, cell = cellSize, density = density,
        };
        var meshJob = new MeshJob
        {
            density = density, blobs = ellipsoids, layerRadius = layerRadius, samples = samples, origin = gridOrigin, cell = cellSize,
            vertices = vertices, indices = indices, edgeToVertex = edgeToVertex,
        };
        job = meshJob.Schedule(densityJob.Schedule(density.Length, 128));
    }

    void UpdateEllipsoids(float time)
    {
        if (states == null || states.Length != blobCount) CreateStates();

        for (int i = 0; i < blobCount; i++)
        {
            BlobState s = states[i];
            float radius = s.diameter * 0.5f;

            // Cosine cycle: slow near the bottom and top (wax heating up / cooling down), fastest mid-way.
            float cycle = Mathf.Repeat(time / s.period + s.phase, 1f) * Mathf.PI * 2f;
            float height01 = 0.5f - 0.5f * Mathf.Cos(cycle);
            float speed01 = Mathf.Abs(Mathf.Sin(cycle));
            float y = Mathf.Lerp(glassBottom + radius, glassTop - radius * 1.5f, height01);

            // Stretch while moving, squash sideways so the volume stays about the same.
            float stretchY = 1f + stretch * speed01;
            float squash = 1f / Mathf.Sqrt(stretchY);
            float halfWidth = radius * squash;
            y = Mathf.Max(y, glassBottom + radius * stretchY);

            // Drift around the axis, staying inside the (curved) glass wall.
            float wall = glassRadius.Evaluate(Mathf.InverseLerp(glassBottom, glassTop, y));
            float room = Mathf.Max(0f, wall - wallPadding - halfWidth);
            float angle = (s.startAngle + s.swirlDirection * swirlSpeed * time) * Mathf.Deg2Rad;
            float distance = room * s.radial;

            var center = new float3(Mathf.Cos(angle) * distance, y, Mathf.Sin(angle) * distance);
            ellipsoids[i] = MakeEllipsoid(center, new float3(radius * squash, radius * stretchY, radius * squash));
        }

        // The resting pool: a flat blob lying on the bottom of the glass.
        var poolCenter = new float3(0f, glassBottom + poolSize.y * 0.35f, 0f);
        ellipsoids[blobCount] = MakeEllipsoid(poolCenter, new float3(poolSize.x, poolSize.y, poolSize.x));
    }

    static Ellipsoid MakeEllipsoid(float3 center, float3 radii) =>
        new Ellipsoid { center = center, inverseInfluence = SurfaceFraction / radii };

    void CreateStates()
    {
        var random = new System.Random(randomSeed);
        float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        states = new BlobState[blobCount];
        for (int i = 0; i < states.Length; i++)
        {
            states[i] = new BlobState
            {
                diameter = Range(blobDiameter.x, blobDiameter.y),
                period = Range(cycleDuration.x, cycleDuration.y),
                phase = Range(0f, 1f),
                startAngle = Range(0f, 360f),
                swirlDirection = random.NextDouble() < 0.5 ? -1f : 1f,
                radial = Range(0f, 0.85f),
            };
        }
    }

    void Allocate()
    {
        needsRebuild = false;
        states = null;

        float maxRadius = 0f;
        for (int i = 0; i <= 32; i++) maxRadius = Mathf.Max(maxRadius, glassRadius.Evaluate(i / 32f));

        // The grid covers the inside of the glass plus one empty layer on every side, which closes the surface.
        float halfWidth = maxRadius - WallClearance + cellSize;
        gridOrigin = new float3(-halfWidth, glassBottom - cellSize, -halfWidth);
        int across = Mathf.CeilToInt(2f * halfWidth / cellSize) + 1;
        int up = Mathf.CeilToInt((glassTop - glassBottom + 2f * cellSize) / cellSize) + 1;
        samples = new int3(across, up, across);

        density = new NativeArray<float>(samples.x * samples.y * samples.z, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        layerRadius = new NativeArray<float>(samples.y, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (int y = 0; y < samples.y; y++)
        {
            float layerY = gridOrigin.y + y * cellSize;
            layerRadius[y] = glassRadius.Evaluate(Mathf.InverseLerp(glassBottom, glassTop, layerY)) - WallClearance;
        }

        ellipsoids = new NativeArray<Ellipsoid>(blobCount + 1, Allocator.Persistent);
        vertices = new NativeList<WaxVertex>(4096, Allocator.Persistent);
        indices = new NativeList<int>(16384, Allocator.Persistent);
        edgeToVertex = new NativeHashMap<long, int>(4096, Allocator.Persistent);

        waxBounds = new Bounds(
            new Vector3(0f, (glassBottom + glassTop) * 0.5f, 0f),
            new Vector3(2f * maxRadius, glassTop - glassBottom + 2f * cellSize, 2f * maxRadius));
    }

    void Dispose()
    {
        if (density.IsCreated) density.Dispose();
        if (layerRadius.IsCreated) layerRadius.Dispose();
        if (ellipsoids.IsCreated) ellipsoids.Dispose();
        if (vertices.IsCreated) vertices.Dispose();
        if (indices.IsCreated) indices.Dispose();
        if (edgeToVertex.IsCreated) edgeToVertex.Dispose();
    }

    void Upload(Mesh mesh, ref int vertexCap, ref int indexCap, bool exactSize)
    {
        int vertexCount = vertices.Length, indexCount = indices.Length;
        LastVertexCount = vertexCount;
        LastTriangleCount = indexCount / 3;

        // Buffers only grow, so the GPU buffers aren't reallocated every frame.
        if (exactSize || vertexCount > vertexCap)
        {
            vertexCap = exactSize ? vertexCount : math.ceilpow2(vertexCount + 1);
            mesh.SetVertexBufferParams(vertexCap, VertexLayout);
        }
        if (exactSize || indexCount > indexCap)
        {
            indexCap = exactSize ? indexCount : math.ceilpow2(indexCount + 1);
            mesh.SetIndexBufferParams(indexCap, IndexFormat.UInt32);
            mesh.subMeshCount = 1;
        }

        mesh.SetVertexBufferData(vertices.AsArray(), 0, 0, vertexCount, 0, UploadFlags);
        mesh.SetIndexBufferData(indices.AsArray(), 0, 0, indexCount, UploadFlags);
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount) { bounds = waxBounds, vertexCount = vertexCount }, UploadFlags);
        mesh.bounds = waxBounds;
    }

    // ---- Density field (shared by both jobs) ----

    static float Density(float3 p, NativeArray<Ellipsoid> blobs)
    {
        float sum = 0f;
        for (int i = 0; i < blobs.Length; i++)
        {
            float3 d = (p - blobs[i].center) * blobs[i].inverseInfluence;
            float q = math.dot(d, d);
            if (q < 1f)
            {
                float k = 1f - q;
                sum += k * k * k;
            }
        }
        return sum;
    }

    static float WallFade(float3 p, float innerRadius, float cell) => math.saturate((innerRadius - math.length(p.xz)) / (cell * WallSoftnessCells));

    [BurstCompile]
    struct DensityJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Ellipsoid> blobs;
        [ReadOnly] public NativeArray<float> layerRadius;
        public int3 samples;
        public float3 origin;
        public float cell;
        [WriteOnly] public NativeArray<float> density;

        public void Execute(int index)
        {
            int x = index % samples.x;
            int y = index / samples.x % samples.y;
            int z = index / (samples.x * samples.y);
            if (x == 0 || y == 0 || z == 0 || x == samples.x - 1 || y == samples.y - 1 || z == samples.z - 1)
            {
                density[index] = 0f; // empty border closes the surface
                return;
            }
            float3 p = origin + new float3(x, y, z) * cell;
            density[index] = Density(p, blobs) * WallFade(p, layerRadius[y], cell);
        }
    }

    /// <summary>Marching tetrahedra: each grid cube is split into 6 tetrahedra and the surface is cut through them.</summary>
    [BurstCompile]
    struct MeshJob : IJob
    {
        [ReadOnly] public NativeArray<float> density;
        [ReadOnly] public NativeArray<Ellipsoid> blobs;
        [ReadOnly] public NativeArray<float> layerRadius;
        public int3 samples;
        public float3 origin;
        public float cell;
        public NativeList<WaxVertex> vertices;
        public NativeList<int> indices;
        public NativeHashMap<long, int> edgeToVertex;

        public void Execute()
        {
            vertices.Clear();
            indices.Clear();
            edgeToVertex.Clear();

            int sx = samples.x, sxy = samples.x * samples.y;
            int c1 = 1, c2 = 1 + sx, c3 = sx, c4 = sxy, c5 = 1 + sxy, c6 = 1 + sx + sxy, c7 = sx + sxy;

            for (int z = 0; z < samples.z - 1; z++)
            for (int y = 0; y < samples.y - 1; y++)
            for (int x = 0; x < samples.x - 1; x++)
            {
                int b = x + y * sx + z * sxy;
                int inside = Inside(b) + Inside(b + c1) + Inside(b + c2) + Inside(b + c3)
                           + Inside(b + c4) + Inside(b + c5) + Inside(b + c6) + Inside(b + c7);
                if (inside == 0 || inside == 8) continue;

                // Split around the 0-6 diagonal; neighbouring cubes then cut shared faces the same way.
                Tetrahedron(b, b + c5, b + c1, b + c6);
                Tetrahedron(b, b + c1, b + c2, b + c6);
                Tetrahedron(b, b + c2, b + c3, b + c6);
                Tetrahedron(b, b + c3, b + c7, b + c6);
                Tetrahedron(b, b + c7, b + c4, b + c6);
                Tetrahedron(b, b + c4, b + c5, b + c6);
            }
        }

        int Inside(int g) => density[g] >= Threshold ? 1 : 0;

        void Tetrahedron(int a, int b, int c, int d)
        {
            int ia = Inside(a), ib = Inside(b), ic = Inside(c), id = Inside(d);
            int count = ia + ib + ic + id;
            if (count == 0 || count == 4) return;

            if (count == 1 || count == 3)
            {
                // One corner is on its own side: cut it off with a single triangle.
                int lonely = count == 1 ? 1 : 0;
                int odd, o1, o2, o3;
                if (ia == lonely) { odd = a; o1 = b; o2 = c; o3 = d; }
                else if (ib == lonely) { odd = b; o1 = a; o2 = c; o3 = d; }
                else if (ic == lonely) { odd = c; o1 = a; o2 = b; o3 = d; }
                else { odd = d; o1 = a; o2 = b; o3 = c; }
                // Outward = from the inside corner(s) towards the outside corner(s). Grid corners are exact,
                // so this never flips the way interpolated normals can where blobs are just connecting.
                float3 away = (Position(o1) + Position(o2) + Position(o3)) / 3f - Position(odd);
                float3 outward = count == 1 ? away : -away;
                Triangle(Vertex(odd, o1), Vertex(odd, o2), Vertex(odd, o3), outward);
                return;
            }

            // Two corners inside, two outside: the surface is a quad.
            int in1 = -1, in2 = -1, out1 = -1, out2 = -1;
            Sort(a, ia, ref in1, ref in2, ref out1, ref out2);
            Sort(b, ib, ref in1, ref in2, ref out1, ref out2);
            Sort(c, ic, ref in1, ref in2, ref out1, ref out2);
            Sort(d, id, ref in1, ref in2, ref out1, ref out2);
            float3 quadOutward = Position(out1) + Position(out2) - Position(in1) - Position(in2);
            int q0 = Vertex(in1, out1), q1 = Vertex(in1, out2), q2 = Vertex(in2, out2), q3 = Vertex(in2, out1);
            Triangle(q0, q1, q2, quadOutward);
            Triangle(q0, q2, q3, quadOutward);
        }

        static void Sort(int corner, int isInside, ref int in1, ref int in2, ref int out1, ref int out2)
        {
            if (isInside == 1) { if (in1 < 0) in1 = corner; else in2 = corner; }
            else { if (out1 < 0) out1 = corner; else out2 = corner; }
        }

        /// <summary>Returns the vertex where the surface crosses the edge between two grid points, sharing it between triangles.</summary>
        int Vertex(int g0, int g1)
        {
            long key = g0 < g1 ? (long)g0 * density.Length + g1 : (long)g1 * density.Length + g0;
            if (edgeToVertex.TryGetValue(key, out int existing)) return existing;

            float d0 = density[g0], d1 = density[g1];
            float t = math.saturate((Threshold - d0) / (d1 - d0));
            float3 p = math.lerp(Position(g0), Position(g1), t);

            int index = vertices.Length;
            vertices.Add(new WaxVertex { position = p, normal = Normal(p) });
            edgeToVertex.Add(key, index);
            return index;
        }

        void Triangle(int a, int b, int c, float3 outward)
        {
            // Wind the triangle so it faces outward (Unity front faces are clockwise).
            float3 pa = vertices[a].position, pb = vertices[b].position, pc = vertices[c].position;
            float3 facing = math.cross(pb - pa, pc - pa);
            indices.Add(a);
            if (math.dot(facing, outward) >= 0f) { indices.Add(b); indices.Add(c); }
            else { indices.Add(c); indices.Add(b); }
        }

        float3 Position(int g)
        {
            int x = g % samples.x;
            int y = g / samples.x % samples.y;
            int z = g / (samples.x * samples.y);
            return origin + new float3(x, y, z) * cell;
        }

        float DensityAt(float3 p)
        {
            float fy = (p.y - origin.y) / cell;
            int y0 = math.clamp((int)math.floor(fy), 0, layerRadius.Length - 2);
            float inner = math.lerp(layerRadius[y0], layerRadius[y0 + 1], math.saturate(fy - y0));
            return Density(p, blobs) * WallFade(p, inner, cell);
        }

        float3 Normal(float3 p)
        {
            float e = cell * 0.5f;
            float3 gradient = new float3(
                DensityAt(p + new float3(e, 0f, 0f)) - DensityAt(p - new float3(e, 0f, 0f)),
                DensityAt(p + new float3(0f, e, 0f)) - DensityAt(p - new float3(0f, e, 0f)),
                DensityAt(p + new float3(0f, 0f, e)) - DensityAt(p - new float3(0f, 0f, e)));
            return math.normalizesafe(-gradient, new float3(0f, 1f, 0f));
        }
    }
}
