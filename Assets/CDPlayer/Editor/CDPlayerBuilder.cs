using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Turns the CD Player FBX into an interactive prefab (Assets/CDPlayer/CDPlayer.prefab, a variant of the model):
/// real-world scale, colliders, the CD tray socket, pokeable buttons, the volume slider, a speaker and the CDPlayer logic.
/// It also creates the CDs (CD.prefab and the "CD Mixtape N" variants) if they don't exist yet.
/// Run it from Tools > CD Player > Rebuild Prefab. Re-running replaces CDPlayer.prefab, so only do that when the FBX changes
/// (changes made by hand on CDPlayer.prefab would be lost; the CD prefabs and materials are kept).
/// </summary>
public static class CDPlayerBuilder
{
    const string ModelPath = "Assets/Assets/FinalScene/CD Player.fbx";
    const string Root = "Assets/CDPlayer";
    const string PrefabPath = Root + "/CDPlayer.prefab";
    const float CDDiameter = 0.12f;      // A real CD is 12 cm across; the whole model is scaled to match.
    const float SlotRadius = 0.05f;      // How close (m) the CD has to be to the tray before it snaps in.
    const float ButtonSinkDepth = 0.0025f;

    [MenuItem("Tools/CD Player/Rebuild Prefab")]
    public static void Build()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError($"[CDPlayerBuilder] Model not found at {ModelPath}");
            return;
        }

        var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            root.transform.position = Vector3.zero;

            Transform lid = Find(root, "CD_Lid");
            Transform cd = Find(root, "CD");
            Transform landing = Find(root, "CD_Landing_Point");
            Transform body = Find(root, "CD_Player_Body");
            Transform holder = Find(root, "CD_Holder");

            // Scale so the CD is 12 cm.
            var cdMesh = cd.GetComponent<MeshFilter>().sharedMesh;
            float cdSizeAtScale1 = Mathf.Max(cdMesh.bounds.size.x, cdMesh.bounds.size.y) * cd.lossyScale.x / root.transform.localScale.x;
            root.transform.localScale = Vector3.one * (CDDiameter / cdSizeAtScale1);

            // Lid: the FBX has it open. Closed is flat on the tray (identity in this model).
            Quaternion lidOpen = lid.localRotation;
            Quaternion lidClosed = Quaternion.identity;
            lid.localRotation = lidClosed;

            // Body collider: the full body, but its top stops at the tray so the CD inside stays reachable.
            var bodyCollider = body.gameObject.AddComponent<BoxCollider>();
            Bounds bodyMesh = body.GetComponent<MeshFilter>().sharedMesh.bounds;
            float trayTopWorld = holder.GetComponent<Renderer>().bounds.max.y;
            Vector3 trayTopLocal = body.InverseTransformPoint(new Vector3(0f, trayTopWorld, 0f));
            int upAxis = DominantAxis(body.InverseTransformDirection(Vector3.up));
            float min = bodyMesh.min[upAxis];
            float max = Mathf.Min(bodyMesh.max[upAxis], trayTopLocal[upAxis]);
            Vector3 center = bodyMesh.center, size = bodyMesh.size;
            center[upAxis] = (min + max) * 0.5f;
            size[upAxis] = max - min;
            bodyCollider.center = center;
            bodyCollider.size = size;

            // The CD that comes with the model isn't used: the CDs are separate mixtape prefabs (see BuildDiscs).
            cd.gameObject.SetActive(false);

            // Tray socket.
            var slotTrigger = landing.gameObject.AddComponent<SphereCollider>();
            slotTrigger.isTrigger = true;
            slotTrigger.radius = SlotRadius / landing.lossyScale.x;
            var slot = landing.gameObject.AddComponent<XRSocketInteractor>();

            // Buttons.
            var player = root.AddComponent<CDPlayer>();

            // Speaker: 3D sound coming from the player, audible across a room.
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            audio.minDistance = 0.5f;
            audio.maxDistance = 10f;
            audio.dopplerLevel = 0f;

            Transform power = Find(root, "Button_Power");
            SetupButton(power, player.PressPower);
            SetupButton(Find(root, "Button_Pause/Play"), player.PressPlayPause);
            SetupButton(Find(root, "Button_Eject"), player.PressEject);
            SetupButton(Find(root, "Button_Fast-forward"), player.PressFastForward);
            SetupButton(Find(root, "Button_Rewind"), player.PressRewind);

            // Volume slider: the knob slides between the Volume_Low and Volume_High empties. Starts halfway.
            Transform knob = Find(root, "Volume_Knob");
            Transform low = Find(root, "Volume_Low");
            Transform high = Find(root, "Volume_High");
            knob.gameObject.AddComponent<BoxCollider>();
            var slider = knob.gameObject.AddComponent<CDVolumeSlider>();
            Vector3 knobOffset = knob.localPosition - low.localPosition;
            knob.localPosition = Vector3.Lerp(low.localPosition, high.localPosition, 0.5f) + knobOffset;
            var sliderSo = new SerializedObject(slider);
            sliderSo.FindProperty("knob").objectReferenceValue = knob;
            sliderSo.FindProperty("low").objectReferenceValue = low;
            sliderSo.FindProperty("high").objectReferenceValue = high;
            sliderSo.FindProperty("knobOffset").vector3Value = knobOffset;
            sliderSo.FindProperty("value").floatValue = 0.5f;
            sliderSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(slider.valueChanged, player.SetVolume);

            // Materials: glass lid so the spinning CD shows through, and a printed CD.
            EnsureFolder("Materials");
            EnsureFolder("Textures");
            Material glass = LoadOrCreateMaterial("CDLidGlass", "ShiftStudios/CDGlass", null);
            lid.GetComponent<Renderer>().sharedMaterial = glass;
            lid.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var so = new SerializedObject(player);
            so.FindProperty("lid").objectReferenceValue = lid;
            so.FindProperty("discSlot").objectReferenceValue = slot;
            so.FindProperty("lidClosedRotation").quaternionValue = lidClosed;
            so.FindProperty("lidOpenRotation").quaternionValue = lidOpen;
            so.FindProperty("powerLight").objectReferenceValue = power.GetComponent<Renderer>();
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("headphoneJack").objectReferenceValue = Find(root, "Headphone_Jack");
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[CDPlayerBuilder] Built {PrefabPath} (scale {root.transform.localScale.x:F2})");

            BuildDiscs(cd, cdMesh);
            BuildHeadphones();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// The CDs: CD.prefab (the grabbable disc) and one variant per mixtape with its own label and songs.
    /// These are only created when missing, so song lists and labels set up by hand are never overwritten.
    /// Delete a prefab to have it regenerated.
    /// </summary>
    static void BuildDiscs(Transform modelCD, Mesh cdMesh)
    {
        string basePath = Root + "/CD.prefab";
        float radius = Mathf.Max(cdMesh.bounds.extents.x, cdMesh.bounds.extents.y);
        Material mixtape1Mat = DiscMaterial("CDDisc", "CDLabel", radius,
            new Color(1f, 0.55f, 0.15f), new Color(0.95f, 0.2f, 0.4f), new Color(0.3f, 0.1f, 0.5f), 1);
        Material mixtape2Mat = DiscMaterial("CDDisc Mixtape 2", "CDLabel Mixtape 2", radius,
            new Color(0.2f, 0.85f, 0.75f), new Color(0.15f, 0.45f, 0.95f), new Color(0.35f, 0.15f, 0.6f), 2);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(basePath) == null)
        {
            var disc = new GameObject("CD");
            disc.transform.SetPositionAndRotation(Vector3.zero, modelCD.rotation);   // label side up
            disc.transform.localScale = modelCD.lossyScale;
            disc.AddComponent<MeshFilter>().sharedMesh = cdMesh;
            var renderer = disc.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mixtape1Mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            disc.AddComponent<MeshCollider>().convex = true;
            var rb = disc.AddComponent<Rigidbody>();
            rb.mass = 0.016f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // The CD is under 1 mm thick: speculative CCD let it fall through the floor in testing, a real sweep doesn't.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            disc.AddComponent<XRGrabInteractable>().useDynamicAttach = true;
            disc.AddComponent<CDDisc>();
            PrefabUtility.SaveAsPrefabAsset(disc, basePath);
            Object.DestroyImmediate(disc);
        }

        else
        {
            // The CD prefab already exists: only refresh its mesh and size from the model, so a re-imported FBX
            // (which gets new mesh references) doesn't leave invisible CDs without collision. Everything else is kept.
            var contents = PrefabUtility.LoadPrefabContents(basePath);
            contents.transform.localScale = modelCD.lossyScale;
            contents.GetComponent<MeshFilter>().sharedMesh = cdMesh;
            var meshCollider = contents.GetComponent<MeshCollider>();
            if (meshCollider != null) meshCollider.sharedMesh = cdMesh;
            PrefabUtility.SaveAsPrefabAsset(contents, basePath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
        var songs = new[]
        {
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/FinalScene/Audio/The Smiths - Asleep (Official Video).mp3"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/FinalScene/Audio/The Medication Is Wearing Off.mp3"),
        };
        MixtapeVariant(basePrefab, "Mixtape 1", mixtape1Mat, songs);
        MixtapeVariant(basePrefab, "Mixtape 2", mixtape2Mat, new AudioClip[0]);
    }

    static void MixtapeVariant(GameObject basePrefab, string title, Material material, AudioClip[] songs)
    {
        string path = $"{Root}/CD {title}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        instance.name = $"CD {title}";
        instance.GetComponent<MeshRenderer>().sharedMaterial = material;
        var so = new SerializedObject(instance.GetComponent<CDDisc>());
        so.FindProperty("title").stringValue = title;
        var tracks = so.FindProperty("tracks");
        tracks.arraySize = 5;
        for (int i = 0; i < 5; i++)
            tracks.GetArrayElementAtIndex(i).objectReferenceValue = i < songs.Length ? songs[i] : null;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
    }

    static Material DiscMaterial(string materialName, string labelName, float radius, Color a, Color b, Color c, int stripes)
    {
        Texture2D label = LoadOrCreateLabel(labelName, a, b, c, stripes);
        Material mat = LoadOrCreateMaterial(materialName, "ShiftStudios/CDDisc", m => m.SetTexture("_LabelTex", label));
        mat.SetFloat("_DiscRadius", radius);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    const string HeadphonesModelPath = "Assets/Assets/FinalScene/Headphones.fbx";
    const string HeadphonesPrefabPath = Root + "/Headphones.prefab";
    const float HeadphonesHeight = 0.21f;   // real over-ear headphones are about 20 cm tall

    /// <summary>
    /// Headphones.prefab (a variant of the model): real-world size, grabbable, snap onto your head (CDHeadphones),
    /// an AudioSource for the music, and the Y-cable (HeadphoneCable). Rebuilt every time, like CDPlayer.prefab.
    /// </summary>
    static void BuildHeadphones()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(HeadphonesModelPath);
        if (model == null)
        {
            Debug.LogWarning($"[CDPlayerBuilder] No headphones model at {HeadphonesModelPath}; skipped.");
            return;
        }

        // Values you tuned in the Inspector on the old prefab survive a rebuild.
        var oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeadphonesPrefabPath);
        var oldHeadphones = oldPrefab != null ? oldPrefab.GetComponent<CDHeadphones>() : null;
        var oldCable = oldPrefab != null ? oldPrefab.GetComponentInChildren<HeadphoneCable>(true) : null;

        var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            root.transform.position = Vector3.zero;
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            root.transform.localScale *= HeadphonesHeight / RendererBounds(renderers).size.y;

            Transform leftJack = Find(root, "Headphone_Left_Jack");
            Transform rightJack = Find(root, "Headphone_Right_Jack");

            // The point between the ear cups, facing the way the wearer looks. This is what snaps onto the head.
            Vector3 leftCup = NearestRenderer(renderers, leftJack.position).bounds.center;
            Vector3 rightCup = NearestRenderer(renderers, rightJack.position).bounds.center;
            var headAttach = new GameObject("Head Attach").transform;
            headAttach.SetParent(root.transform, false);
            headAttach.position = (leftCup + rightCup) * 0.5f;
            Vector3 toRight = (rightCup - leftCup).normalized;
            headAttach.rotation = Quaternion.LookRotation(Vector3.Cross(toRight, Vector3.up), Vector3.up);

            var colliders = new List<Collider>();
            foreach (var r in renderers)
            {
                var c = r.gameObject.AddComponent<MeshCollider>();
                c.convex = true;
                colliders.Add(c);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.25f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;      // hands hold them where they grab...
            grab.attachTransform = headAttach; // ...the head socket lines up this point with your ears

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 0.05f;
            audio.maxDistance = 2f;
            audio.dopplerLevel = 0f;

            var headphones = root.AddComponent<CDHeadphones>();
            var hso = new SerializedObject(headphones);
            hso.FindProperty("audioSource").objectReferenceValue = audio;
            hso.FindProperty("headAttach").objectReferenceValue = headAttach;
            var rendererList = hso.FindProperty("modelRenderers");
            rendererList.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) rendererList.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            hso.ApplyModifiedPropertiesWithoutUndo();

            // Y-cable. Its jack end is connected in the scene (Tools > CD Player > Connect Headphones In Scene).
            var cableObject = new GameObject("Cable");
            cableObject.transform.SetParent(root.transform, false);
            cableObject.AddComponent<MeshFilter>();
            var cableRenderer = cableObject.AddComponent<MeshRenderer>();
            cableRenderer.sharedMaterial = LoadOrCreateMaterial("HeadphoneCable", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.035f, 0.035f, 0.04f));
                m.SetFloat("_Smoothness", 0.35f);
            });
            cableRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var cable = cableObject.AddComponent<HeadphoneCable>();
            cable.leftEar = leftJack;
            cable.rightEar = rightJack;
            cable.headphonesFrame = headAttach;
            var cso = new SerializedObject(cable);
            var ignore = cso.FindProperty("ignoreColliders");
            ignore.arraySize = colliders.Count;
            for (int i = 0; i < colliders.Count; i++) ignore.GetArrayElementAtIndex(i).objectReferenceValue = colliders[i];
            cso.ApplyModifiedPropertiesWithoutUndo();

            CopyTunedValues(oldHeadphones, headphones);
            CopyTunedValues(oldCable, cable);
            PrefabUtility.SaveAsPrefabAsset(root, HeadphonesPrefabPath);
            Debug.Log($"[CDPlayerBuilder] Built {HeadphonesPrefabPath} (scale {root.transform.localScale.x:F2})");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>Plugs the headphones in the open scene into the CD player in the open scene.</summary>
    [MenuItem("Tools/CD Player/Connect Headphones In Scene")]
    public static void ConnectHeadphonesInScene()
    {
        var player = Object.FindFirstObjectByType<CDPlayer>();
        var headphones = Object.FindFirstObjectByType<CDHeadphones>();
        if (player == null || headphones == null)
        {
            Debug.LogWarning("[CDPlayerBuilder] Need a CD Player and Headphones in the open scene.");
            return;
        }
        var so = new SerializedObject(player);
        so.FindProperty("headphones").objectReferenceValue = headphones;
        so.ApplyModifiedProperties();
        var cable = headphones.GetComponentInChildren<HeadphoneCable>(true);
        Undo.RecordObject(cable, "Connect headphone cable");
        cable.jack = player.HeadphoneJack;
        PrefabUtility.RecordPrefabInstancePropertyModifications(cable);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Debug.Log("[CDPlayerBuilder] Headphones connected to the CD player.");
    }

    /// <summary>
    /// Copies the plain settings (numbers, toggles, vectors, colours, layer masks) from an old component to its rebuilt copy.
    /// References to other objects and events are left alone: the builder sets those up itself.
    /// </summary>
    static void CopyTunedValues(Component from, Component to)
    {
        if (from == null || to == null) return;
        var source = new SerializedObject(from);
        var target = new SerializedObject(to);
        var property = source.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.name == "m_Script") continue;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Boolean:
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Color:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Enum:
                    target.CopyFromSerializedProperty(property);
                    break;
            }
        }
        target.ApplyModifiedPropertiesWithoutUndo();
    }

    static Bounds RendererBounds(Renderer[] renderers)
    {
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    static Renderer NearestRenderer(Renderer[] renderers, Vector3 point)
    {
        Renderer best = null;
        float bestDistance = float.MaxValue;
        foreach (var r in renderers)
        {
            float d = (r.bounds.center - point).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = r; }
        }
        return best;
    }

    static void SetupButton(Transform button, UnityAction onPressed)
    {
        button.gameObject.AddComponent<BoxCollider>();   // Auto-fits the button mesh.
        var interactable = button.gameObject.AddComponent<XRSimpleInteractable>();
        var poke = button.gameObject.AddComponent<XRPokeFilter>();

        // Poking means pushing the button straight down.
        poke.pokeConfiguration = new PokeThresholdDatumProperty(new PokeThresholdData
        {
            pokeDirection = AxisPointing(button, Vector3.down),
            enablePokeAngleThreshold = true,
            pokeAngleThreshold = 60f,
        });

        var pressable = button.gameObject.AddComponent<CDPlayerButton>();
        var so = new SerializedObject(pressable);
        so.FindProperty("pressDepth").floatValue = ButtonSinkDepth;
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(pressable.pressed, onPressed);
    }

    /// <summary>The local axis of <paramref name="t"/> that points most along <paramref name="worldDirection"/>.</summary>
    static PokeAxis AxisPointing(Transform t, Vector3 worldDirection)
    {
        Vector3 local = t.InverseTransformDirection(worldDirection);
        int axis = DominantAxis(local);
        bool negative = local[axis] < 0f;
        switch (axis)
        {
            case 0: return negative ? PokeAxis.NegativeX : PokeAxis.X;
            case 1: return negative ? PokeAxis.NegativeY : PokeAxis.Y;
            default: return negative ? PokeAxis.NegativeZ : PokeAxis.Z;
        }
    }

    static int DominantAxis(Vector3 v)
    {
        Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        return a.x >= a.y && a.x >= a.z ? 0 : (a.y >= a.z ? 1 : 2);
    }

    static void EnsureFolder(string name)
    {
        if (!AssetDatabase.IsValidFolder($"{Root}/{name}")) AssetDatabase.CreateFolder(Root, name);
    }

    // Existing materials are kept so tweaks made in the Inspector survive a rebuild.
    static Material LoadOrCreateMaterial(string name, string shaderName, System.Action<Material> setup)
    {
        string path = $"{Root}/Materials/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var mat = new Material(Shader.Find(shaderName)) { name = name };
        setup?.Invoke(mat);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    /// <summary>
    /// A placeholder CD label (only generated if it doesn't exist yet, so it can be swapped for real album art).
    /// It's deliberately lopsided (colour sweep + a stripe), otherwise you couldn't see the disc spin.
    /// </summary>
    static Texture2D LoadOrCreateLabel(string name, Color warm, Color pink, Color purple, int stripes)
    {
        string path = $"{Root}/Textures/{name}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int size = 512;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f) / size * 2f - Vector2.one;
            float r = p.magnitude;
            float a = (Mathf.Atan2(p.y, p.x) / (2f * Mathf.PI) + 1f) % 1f;
            Color c = a < 0.5f ? Color.Lerp(warm, pink, a * 2f) : Color.Lerp(pink, purple, (a - 0.5f) * 2f);
            c *= 0.85f + 0.15f * Mathf.Cos(r * 60f);                               // faint rings
            for (int s = 0; s < stripes; s++)                                     // one stripe per mixtape number
                if (Mathf.Abs(p.y - 0.25f + s * 0.2f) < 0.06f && p.x > -0.1f) c = Color.Lerp(c, new Color(1f, 0.95f, 0.85f), 0.9f);
            if ((p - new Vector2(-0.55f, -0.35f)).magnitude < 0.09f) c = new Color(0.1f, 0.08f, 0.12f);           // dot
            c.a = 1f;
            tex.SetPixel(x, y, c);
        }
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 512;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Names like "Button_Pause/Play" contain a slash, so Transform.Find can't be used.
    static Transform Find(GameObject root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        throw new System.Exception($"[CDPlayerBuilder] '{name}' not found in {ModelPath}");
    }
}
