using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Headphone Y-cable: a wire from each ear cup joins at a splitter, and one cable runs from there into the jack plug.
/// It's a lightweight rope simulation (Verlet): it hangs, swings and lies on the floor and table.
/// The main cable never pulls: it grows as you walk away from the player and shrinks back when you return.
/// Drawn as one generated tube mesh; cheap enough for Quest (around 100 simulated points).
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HeadphoneCable : MonoBehaviour
{
    [Header("Ends")]
    public Transform leftEar;
    public Transform rightEar;
    [Tooltip("The headphone socket on the CD player.")]
    public Transform jack;
    [Tooltip("The headphones' orientation (their up = up when worn). The wires leave the ear cups along its down direction.")]
    public Transform headphonesFrame;
    [Tooltip("The cable passes through these (the headphones' own colliders).")]
    [SerializeField] Collider[] ignoreColliders;

    [Header("Length")]
    [Tooltip("Length of each ear wire, from the ear cup to the splitter.")]
    [SerializeField] float earWireLength = 0.4f;
    [Tooltip("The main cable is never shorter than this.")]
    [SerializeField] float minCableLength = 0.5f;
    [Tooltip("How much longer than the straight distance the main cable is, so it always sags a little.")]
    [SerializeField, Min(1.01f)] float slack = 1.2f;
    [SerializeField] float segmentLength = 0.035f;
    [Tooltip("Above this many points the segments get longer instead, so a very long cable stays cheap.")]
    [SerializeField] int maxCablePoints = 240;

    [Header("Look")]
    [SerializeField] float cableRadius = 0.0018f;
    [SerializeField] float earWireRadius = 0.0013f;
    [SerializeField] float plugLength = 0.035f;
    [SerializeField] float plugRadius = 0.0035f;
    [SerializeField] float splitterLength = 0.025f;
    [SerializeField] float splitterRadius = 0.0045f;
    [SerializeField] float strainReliefLength = 0.015f;
    [SerializeField, Range(4, 12)] int sides = 6;

    [Header("Simulation")]
    [SerializeField, Range(4, 40)] int iterations = 16;
    [SerializeField, Range(0.9f, 1f)] float damping = 0.985f;
    [Tooltip("How much the cable sticks instead of sliding when it lies on something.")]
    [SerializeField, Range(0f, 1f)] float friction = 0.6f;
    [Tooltip("How much the cable resists sharp bends (0 = limp string that crumples into zigzags).")]
    [SerializeField, Range(0f, 1f)] float stiffness = 0.5f;
    [SerializeField] LayerMask collisionLayers = ~0;

    const int LegPoints = 12;              // per ear wire, including its pinned end
    const float StepTime = 1f / 90f;
    const float StraightExit = 0.01f;      // the cable leaves the plug and the ear cups straight for this long (m)
    const float BendLimit = 0.92f;         // points two apart stay at least this fraction of two segments apart (~45° max bend)
    const int MaxStepsPerFrame = 4;

    // Point layout: [0, L) left wire (0 = ear), [L, 2L) right wire (L = ear), 2L = splitter, then the main cable up to the plug.
    int Splitter => 2 * LegPoints;
    int MainStart => Splitter + 1;

    Vector3[] pos, prev;
    int count;
    float mainLength, legRest, mainRest, timeAccumulator;
    Vector3 earDirLeftLocal, earDirRightLocal, plugDirLocal;
    readonly RaycastHit[] hits = new RaycastHit[8];
    readonly HashSet<Collider> ignored = new HashSet<Collider>();

    Mesh mesh;
    readonly List<Vector3> vertices = new List<Vector3>();
    readonly List<Vector3> normals = new List<Vector3>();
    readonly List<int> triangles = new List<int>();
    readonly List<Vector3> path = new List<Vector3>();
    bool ready;

    void Start()
    {
        if (leftEar == null || rightEar == null || jack == null)
        {
            Debug.LogWarning("[HeadphoneCable] Ears or jack not assigned; the cable is hidden.", this);
            enabled = false;
            return;
        }
        if (ignoreColliders != null) foreach (var c in ignoreColliders) if (c != null) ignored.Add(c);

        // Wires leave the ear cups "down" as seen from the headphones; the plug sticks up out of the jack
        // (measured once, with the CD player standing on the table).
        Transform frame = headphonesFrame != null ? headphonesFrame : leftEar;
        earDirLeftLocal = leftEar.InverseTransformDirection(-frame.up);
        earDirRightLocal = rightEar.InverseTransformDirection(-frame.up);
        plugDirLocal = jack.InverseTransformDirection(Vector3.up);

        mesh = new Mesh { name = "Headphone Cable" };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        int capacity = MainStart + maxCablePoints + 2;
        pos = new Vector3[capacity];
        prev = new Vector3[capacity];
        legRest = earWireLength / LegPoints;
        mainLength = TargetMainLength();
        int mainPoints = MainPointCount(mainLength);
        count = MainStart + mainPoints;
        mainRest = mainLength / mainPoints;

        // Start as straight lines between the ends (above anything they rest on), then let it settle,
        // so the scene starts with a cable that's already hanging/lying naturally.
        Vector3 ears = (leftEar.position + rightEar.position) * 0.5f;
        Vector3 splitter = Vector3.MoveTowards(ears, PlugBack, earWireLength * 0.7f);
        for (int i = 0; i < LegPoints; i++)
        {
            float t = i / (float)LegPoints;
            pos[i] = Vector3.Lerp(leftEar.position, splitter, t);
            pos[LegPoints + i] = Vector3.Lerp(rightEar.position, splitter, t);
        }
        pos[Splitter] = splitter;
        for (int i = 0; i < mainPoints; i++)
            pos[MainStart + i] = Vector3.Lerp(splitter, PlugBack, (i + 1) / (float)mainPoints);
        System.Array.Copy(pos, prev, count);
        for (int i = 0; i < 240; i++) Step(StepTime);

        ready = true;
        Application.onBeforeRender += BuildMesh;
    }

    void OnDestroy()
    {
        Application.onBeforeRender -= BuildMesh;
        if (mesh != null) Destroy(mesh);
    }

    Vector3 EarDir(Transform ear, Vector3 local) => ear.TransformDirection(local).normalized;
    Vector3 PlugDir => jack.TransformDirection(plugDirLocal).normalized;
    Vector3 PlugBack => jack.position + PlugDir * plugLength;

    float TargetMainLength()
    {
        Vector3 ears = (leftEar.position + rightEar.position) * 0.5f;
        return Mathf.Max(minCableLength, Vector3.Distance(ears, PlugBack) * slack);
    }

    int MainPointCount(float length) => Mathf.Clamp(Mathf.CeilToInt(length / segmentLength), 4, maxCablePoints);

    void LateUpdate()
    {
        if (!ready) return;

        // Grow or shrink the main cable; new cable comes out of the plug, like it's unspooling.
        mainLength = Mathf.MoveTowards(mainLength, TargetMainLength(), 6f * Time.deltaTime);
        ResizeMain(MainPointCount(mainLength));
        mainRest = mainLength / (count - MainStart);

        timeAccumulator = Mathf.Min(timeAccumulator + Time.deltaTime, StepTime * MaxStepsPerFrame);
        while (timeAccumulator >= StepTime)
        {
            Step(StepTime);
            timeAccumulator -= StepTime;
        }
    }

    void ResizeMain(int mainPoints)
    {
        int wanted = MainStart + mainPoints;
        while (count < wanted)   // insert a point just before the plug's two pinned points
        {
            int at = count - 2;
            for (int i = count; i > at; i--) { pos[i] = pos[i - 1]; prev[i] = prev[i - 1]; }
            pos[at] = prev[at] = pos[at + 1];
            count++;
        }
        while (count > wanted)   // remove the point just before the plug's two pinned points
        {
            int at = count - 3;
            for (int i = at; i < count - 1; i++) { pos[i] = pos[i + 1]; prev[i] = prev[i + 1]; }
            count--;
        }
    }

    bool IsPinned(int i) => i == 0 || i == 1 || i == LegPoints || i == LegPoints + 1 || i >= count - 2;

    void PinEnds()
    {
        // Two points at each end are pinned, so the wire leaves the ear cup and the plug in a straight line.
        Vector3 l = leftEar.position, r = rightEar.position, p = PlugBack;
        pos[0] = prev[0] = l;
        pos[1] = prev[1] = l + EarDir(leftEar, earDirLeftLocal) * StraightExit;
        pos[LegPoints] = prev[LegPoints] = r;
        pos[LegPoints + 1] = prev[LegPoints + 1] = r + EarDir(rightEar, earDirRightLocal) * StraightExit;
        pos[count - 1] = prev[count - 1] = p;
        pos[count - 2] = prev[count - 2] = p + PlugDir * StraightExit;
    }

    void Step(float dt)
    {
        PinEnds();
        Vector3 gravity = Physics.gravity * (dt * dt);
        for (int i = 0; i < count; i++)
        {
            if (IsPinned(i)) continue;
            Vector3 velocity = (pos[i] - prev[i]) * damping;
            prev[i] = pos[i];
            pos[i] += velocity + gravity;
        }

        for (int k = 0; k < iterations; k++)
        {
            for (int leg = 0; leg < 2; leg++)
            {
                int start = leg * LegPoints;
                for (int i = start; i < start + LegPoints - 1; i++) Solve(i, i + 1, legRest);
                Solve(start + LegPoints - 1, Splitter, legRest);
            }
            Solve(Splitter, MainStart, mainRest);
            for (int i = MainStart; i < count - 1; i++) Solve(i, i + 1, mainRest);

            // Bending stiffness: keep every other point apart, so the cable curves in loops instead of folding flat.
            if (stiffness > 0f)
            {
                for (int leg = 0; leg < 2; leg++)
                {
                    int start = leg * LegPoints;
                    for (int i = start; i < start + LegPoints - 2; i++) KeepApart(i, i + 2, legRest);
                    KeepApart(start + LegPoints - 2, Splitter, legRest);
                }
                KeepApart(Splitter, MainStart + 1, mainRest);
                for (int i = MainStart; i < count - 2; i++) KeepApart(i, i + 2, mainRest);
            }
        }

        for (int i = 0; i < count; i++)
            if (!IsPinned(i)) Collide(i);
    }

    void Solve(int a, int b, float rest)
    {
        bool pinA = IsPinned(a), pinB = IsPinned(b);
        if (pinA && pinB) return;
        Vector3 delta = pos[b] - pos[a];
        float distance = delta.magnitude;
        if (distance < 1e-6f) return;
        Vector3 correction = delta * ((distance - rest) / distance);
        if (pinA) pos[b] -= correction;
        else if (pinB) pos[a] += correction;
        else
        {
            pos[a] += correction * 0.5f;
            pos[b] -= correction * 0.5f;
        }
    }

    void KeepApart(int a, int b, float rest)
    {
        bool pinA = IsPinned(a), pinB = IsPinned(b);
        if (pinA && pinB) return;
        float min = 2f * rest * BendLimit;
        Vector3 delta = pos[b] - pos[a];
        float distance = delta.magnitude;
        if (distance >= min || distance < 1e-6f) return;
        Vector3 correction = delta * ((distance - min) / distance * stiffness);
        if (pinA) pos[b] -= correction;
        else if (pinB) pos[a] += correction;
        else
        {
            pos[a] += correction * 0.5f;
            pos[b] -= correction * 0.5f;
        }
    }

    // Sweep each point from where it was to where it is; if that crosses a surface, rest it on the surface.
    void Collide(int i)
    {
        float radius = i < MainStart ? earWireRadius : cableRadius;
        Vector3 move = pos[i] - prev[i];
        float distance = move.magnitude;
        Vector3 direction = distance > 1e-6f ? move / distance : Vector3.down;
        Vector3 origin = prev[i] - direction * radius;
        int hitCount = Physics.RaycastNonAlloc(origin, direction, hits, distance + radius * 2f, collisionLayers, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        int bestIndex = -1;
        for (int h = 0; h < hitCount; h++)
        {
            var c = hits[h].collider;
            if (ignored.Contains(c) || c is CharacterController) continue;
            if (hits[h].distance < best) { best = hits[h].distance; bestIndex = h; }
        }
        if (bestIndex < 0) return;

        pos[i] = hits[bestIndex].point + hits[bestIndex].normal * radius;
        prev[i] = Vector3.Lerp(prev[i], pos[i], friction);
    }

    // ---------- Mesh ----------

    void BuildMesh()
    {
        if (!ready || !isActiveAndEnabled) return;
        PinEnds();   // ends follow the very latest pose of the headphones (they move again right before rendering)
        vertices.Clear(); normals.Clear(); triangles.Clear();

        Vector3 splitterDir = (pos[MainStart] - pos[Splitter]).normalized;
        Vector3 splitterTop = pos[Splitter] - splitterDir * splitterLength * 0.5f;

        // Ear wires end at the top of the splitter, the main cable starts at its bottom.
        for (int leg = 0; leg < 2; leg++)
        {
            path.Clear();
            int start = leg * LegPoints;
            for (int i = start; i < start + LegPoints; i++) path.Add(pos[i]);
            path.Add(splitterTop);
            AddSmoothTube(path, earWireRadius);
            Transform ear = leg == 0 ? leftEar : rightEar;
            Vector3 earDir = EarDir(ear, leg == 0 ? earDirLeftLocal : earDirRightLocal);
            AddCylinder(ear.position - earDir * 0.002f, ear.position + earDir * strainReliefLength, earWireRadius * 1.8f);
        }

        path.Clear();
        path.Add(pos[Splitter] + splitterDir * splitterLength * 0.5f);
        for (int i = MainStart; i < count; i++) path.Add(pos[i]);
        AddSmoothTube(path, cableRadius);

        AddCylinder(splitterTop, pos[Splitter] + splitterDir * splitterLength * 0.5f, splitterRadius);
        AddCylinder(jack.position, PlugBack, plugRadius);

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }

    readonly List<Vector3> smooth = new List<Vector3>();

    // Catmull-Rom: two extra points per segment, so the tube bends smoothly instead of in straight pieces.
    void AddSmoothTube(List<Vector3> points, float radius)
    {
        smooth.Clear();
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, points.Count - 1)];
            for (int s = 0; s < 3; s++)
            {
                float t = s / 3f;
                smooth.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t) + (3f * p1 - p0 - 3f * p2 + p3) * (t * t * t)));
            }
        }
        smooth.Add(points[points.Count - 1]);
        AddTube(smooth, radius, false);
    }

    void AddCylinder(Vector3 a, Vector3 b, float radius)
    {
        smooth.Clear();
        smooth.Add(a);
        smooth.Add(b);
        AddTube(smooth, radius, true);
    }

    void AddTube(List<Vector3> points, float radius, bool caps)
    {
        if (points.Count < 2) return;
        Vector3 tangent = (points[1] - points[0]).normalized;
        if (tangent == Vector3.zero) tangent = Vector3.down;
        Vector3 normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < 0.9f ? Vector3.up : Vector3.right).normalized;

        int firstRing = vertices.Count;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 next = i < points.Count - 1 ? points[i + 1] - points[i] : points[i] - points[i - 1];
            if (next.sqrMagnitude > 1e-12f)
            {
                Vector3 newTangent = next.normalized;
                // Parallel transport: turn the ring frame with the cable so the tube doesn't twist.
                normal = Quaternion.FromToRotation(tangent, newTangent) * normal;
                tangent = newTangent;
            }
            Vector3 binormal = Vector3.Cross(tangent, normal);
            Vector3 center = transform.InverseTransformPoint(points[i]);
            for (int s = 0; s < sides; s++)
            {
                float angle = s * Mathf.PI * 2f / sides;
                Vector3 n = normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
                Vector3 localN = transform.InverseTransformDirection(n);
                vertices.Add(center + transform.InverseTransformVector(n * radius));
                normals.Add(localN);
            }
        }

        for (int i = 0; i < points.Count - 1; i++)
        for (int s = 0; s < sides; s++)
        {
            int a = firstRing + i * sides + s;
            int b = firstRing + i * sides + (s + 1) % sides;
            int c = a + sides, d = b + sides;
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(b); triangles.Add(d); triangles.Add(c);
        }

        if (!caps) return;
        AddCap(firstRing, points[0], -(points[1] - points[0]).normalized, true);
        AddCap(firstRing + (points.Count - 1) * sides, points[points.Count - 1], (points[points.Count - 1] - points[points.Count - 2]).normalized, false);
    }

    void AddCap(int ring, Vector3 center, Vector3 outward, bool flip)
    {
        int centerIndex = vertices.Count;
        vertices.Add(transform.InverseTransformPoint(center));
        normals.Add(transform.InverseTransformDirection(outward));
        for (int s = 0; s < sides; s++)
        {
            int a = ring + s, b = ring + (s + 1) % sides;
            if (flip) { triangles.Add(centerIndex); triangles.Add(b); triangles.Add(a); }
            else { triangles.Add(centerIndex); triangles.Add(a); triangles.Add(b); }
        }
    }
}
