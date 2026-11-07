using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CoolGlasses
{
    // One standard helmet turned into a head with glasses: its shell is hidden, a head model (the
    // scan from Assets\head.bin, in its own colours) sits where it was, the game's own
    // skin sphere under the helmet is hidden, and the glasses (children of the helmet's root "hqh",
    // so they move with the head) follow the visor. Visor down = glasses on the
    // eyes, visor up = glasses pushed up onto the head, turning around the ears.
    //
    // The visor's state comes from the game, nothing is sent: your own helmet's HelmetController,
    // another player's remote helmet animation (PlayerModelSync.remoteHelmetAnimator, which the game
    // already syncs). Both run one animation where time 0 = visor down and 1 = up.
    //
    // Your own view: the glasses would sit a few centimetres in front of your eyes, so a camera
    // inside the head doesn't see them; it sees the game's visor instead (its darkening when down).
    // Every other camera (other players, external and spectator cameras) sees the glasses and no visor.
    // The head is likewise only for cameras outside it.
    public class HeadGlasses : MonoBehaviour
    {
        public const string ObjectName = "CoolGlasses";
        // A camera closer than this to the glasses (metres) is inside the head
        private const float InsideDistance = 0.15f;

        public static readonly List<HeadGlasses> All = new List<HeadGlasses>();
        private static bool hooked;

        private static readonly FieldInfo VisorTimeField = typeof(HelmetController).GetField("visorTime", BindingFlags.Instance | BindingFlags.NonPublic);

        private Transform root;
        private MeshRenderer glasses;
        // The head, and the game's skin sphere(s) it replaces (its material and skin colour are copied)
        private Transform head;
        private MeshRenderer headRenderer;
        private readonly List<Renderer> skin = new List<Renderer>();
        private MaterialPropertyBlock skinProps;
        // Shell parts (always hidden) and the visor (only for a camera inside the head), each with
        // whether it was shown before, for putting the helmet back
        private readonly List<Renderer> shell = new List<Renderer>();
        private readonly List<Renderer> visor = new List<Renderer>();
        private readonly Dictionary<Renderer, bool> wasEnabled = new Dictionary<Renderer, bool>();

        private HelmetController controller;
        private AnimationToggle remoteToggle;

        // The helmet's frame in root space: the measured front (-z) and up (+y) of the hqh helmet
        // (HelmetLivery measured them on the head camera), and the shell's centre and half sizes
        private static readonly Vector3 Front = Vector3.back, UpDir = Vector3.up;
        private Vector3 centre;
        private float halfLength, halfHeight;

        // How far down the visor is (0 up, 1 down)
        private float down = 1f;

        public static void Create(Transform root, MeshFilter shellMesh)
        {
            var go = new GameObject(ObjectName);
            go.transform.SetParent(root, false);
            go.layer = shellMesh.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = GlassesHost.GlassesMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { GlassesHost.FrameMaterial, GlassesHost.LensMaterial };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var g = go.AddComponent<HeadGlasses>();
            g.Setup(root, shellMesh, mr);
        }

        private void Setup(Transform helmetRoot, MeshFilter shellMesh, MeshRenderer own)
        {
            root = helmetRoot;
            glasses = own;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == own) continue;
                List<Renderer> list = GlassesHost.IsShell(r) ? shell : GlassesHost.IsVisor(r) ? visor : null;
                if (list == null) continue;
                list.Add(r);
                wasEnabled[r] = r.enabled;
            }
            Measure(shellMesh);
            FindVisor();
            MakeHead();
            Debug.Log($"[CoolGlasses] Head and glasses on '{Path(root)}': {shell.Count} helmet part(s), {visor.Count} visor(s), {skin.Count} skin sphere(s), visor from "
                      + (controller != null ? "your helmet" : remoteToggle != null ? "the player's synced helmet" : "nothing (glasses stay on the eyes)"));
        }

        // The shell's size and centre along the helmet's frame. Its vertices in the atlas' shell
        // area (u < 0.62, v > 0.5) when the mesh can be read, else the whole mesh's bounds.
        private void Measure(MeshFilter mf)
        {
            Matrix4x4 toRoot = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Vector3 right = Vector3.Cross(UpDir, Front);
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            void Add(Vector3 p)
            {
                p = toRoot.MultiplyPoint3x4(p);
                var d = new Vector3(Vector3.Dot(p, right), Vector3.Dot(p, UpDir), Vector3.Dot(p, Front));
                min = Vector3.Min(min, d);
                max = Vector3.Max(max, d);
            }
            Mesh mesh = mf.sharedMesh;
            int added = 0;
            if (mesh.isReadable)
            {
                Vector3[] v = mesh.vertices;
                Vector2[] uv = mesh.uv;
                if (uv.Length == v.Length)
                    for (int i = 0; i < v.Length; i++)
                        if (uv[i].x < 0.62f && uv[i].y > 0.5f)
                        {
                            Add(v[i]);
                            added++;
                        }
            }
            if (added < 10)
            {
                min = Vector3.one * float.MaxValue;
                max = Vector3.one * float.MinValue;
                Bounds b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                    Add(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
            }
            Vector3 mid = (min + max) * 0.5f;
            centre = right * mid.x + UpDir * mid.y + Front * mid.z;
            halfHeight = (max.y - min.y) * 0.5f;
            halfLength = (max.z - min.z) * 0.5f;
        }

        private void MakeHead()
        {
            if (GlassesHost.HeadMesh == null) return;
            // The game's head: a skin-coloured sphere beside the helmet ("Helmet/headSphere")
            Transform helmet = root.parent != null ? root.parent : root;
            foreach (var r in helmet.GetComponentsInChildren<Renderer>(true))
                if (r.name == "headSphere")
                {
                    skin.Add(r);
                    wasEnabled[r] = r.enabled;
                }
            var go = new GameObject(ObjectName + "Head") { layer = gameObject.layer };
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = GlassesHost.HeadMesh;
            headRenderer = go.AddComponent<MeshRenderer>();
            headRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // The scan's own colours; without them the pilot's skin colour, from the game's skin sphere
            headRenderer.sharedMaterial = GlassesHost.HeadMaterial != null ? GlassesHost.HeadMaterial
                : skin.Count > 0 && skin[0].sharedMaterial != null ? skin[0].sharedMaterial : GlassesHost.SkinMaterial;
            skinProps = new MaterialPropertyBlock();
            head = go.transform;
        }

        // What moves the visor: your own helmet's controller, or the remote animation the game syncs
        // from another player
        private void FindVisor()
        {
            controller = root.GetComponentInParent<HelmetController>();
            if (controller != null) return;
            foreach (var pms in FindObjectsOfType<PlayerModelSync>())
            {
                AnimationToggle t = pms.remoteHelmetAnimator;
                if (t == null || t.animator == null) continue;
                Transform a = t.animator.transform;
                if (root.IsChildOf(a) || a.IsChildOf(root))
                {
                    remoteToggle = t;
                    return;
                }
            }
        }

        private float VisorDown()
        {
            if (controller != null && VisorTimeField != null)
                return 1f - Mathf.Clamp01((float)VisorTimeField.GetValue(controller) / 0.999f);
            if (remoteToggle != null) return 1f - Mathf.Clamp01(remoteToggle.GetT());
            return 1f;
        }

        private void OnEnable()
        {
            All.Add(this);
            if (!hooked)
            {
                Camera.onPreCull += OnPreCull;
                hooked = true;
            }
        }

        private void OnDisable()
        {
            All.Remove(this);
            if (All.Count == 0 && hooked)
            {
                Camera.onPreCull -= OnPreCull;
                hooked = false;
            }
        }

        // Taken off (the mod switched off): the helmet back as it was
        private void OnDestroy()
        {
            if (head != null) Destroy(head.gameObject);
            foreach (var kv in wasEnabled)
                if (kv.Key != null) kv.Key.enabled = kv.Value;
        }

        private void LateUpdate()
        {
            if (root == null) return;
            // Hidden every frame: the game may switch parts back on (spawn, eject, settings)
            foreach (var r in shell)
                if (r != null && r.enabled) r.enabled = false;
            foreach (var r in skin)
                if (r != null && r.enabled) r.enabled = false;
            down = VisorDown();
            Place();
            PlaceHead();
        }

        private void Place()
        {
            float s = Mathf.Abs(root.lossyScale.x);
            if (s < 1e-6f) s = 1f;
            float width = GlassesHost.Width / s;
            Quaternion onEyes = Quaternion.LookRotation(Front, UpDir) * Quaternion.Euler(GlassesHost.Tilt, 0f, 0f);
            Vector3 eyes = centre + Front * (GlassesHost.Forward * halfLength) + UpDir * (GlassesHost.Up * halfHeight);

            // Visor up: turned up around the hinge at the ears, and lifted a bit to clear the forehead
            float up = 1f - Mathf.SmoothStep(0f, 1f, down);
            Quaternion rot = onEyes * Quaternion.Euler(-GlassesHost.FlipAngle * up, 0f, 0f);
            Vector3 pivot = GlassesHost.Pivot * width;
            Vector3 pos = eyes + onEyes * pivot - rot * pivot + UpDir * (GlassesHost.Lift / s * up);

            transform.localPosition = pos;
            transform.localRotation = rot;
            transform.localScale = Vector3.one * width;
        }

        private void PlaceHead()
        {
            if (head == null) return;
            float s = Mathf.Abs(root.lossyScale.x);
            if (s < 1e-6f) s = 1f;
            Vector3 right = Vector3.Cross(UpDir, Front);
            head.localPosition = centre + Front * (GlassesHost.HeadForward * halfLength) + UpDir * (GlassesHost.HeadUp * halfHeight) + right * (GlassesHost.HeadRight / s);
            head.localRotation = Quaternion.LookRotation(Front, UpDir) * Quaternion.Euler(GlassesHost.HeadPitch, GlassesHost.HeadYaw, 0f);
            head.localScale = Vector3.one * (GlassesHost.HeadScale / s);
            // The pilot's own skin colour, as the game gives it to its skin sphere
            if (skin.Count > 0 && skin[0] != null && headRenderer.sharedMaterial == skin[0].sharedMaterial)
            {
                skin[0].GetPropertyBlock(skinProps);
                headRenderer.SetPropertyBlock(skinProps);
            }
        }

        // Per camera, just before it draws: inside the head → the visor, no glasses; else the other way
        private static void OnPreCull(Camera cam)
        {
            if (cam == null) return;
            Transform eye = cam.transform;
            foreach (var g in All)
            {
                if (g == null || g.glasses == null) continue;
                bool inside = g.root.IsChildOf(eye)
                              || (eye.position - g.transform.position).sqrMagnitude < InsideDistance * InsideDistance;
                g.glasses.enabled = !inside;
                if (g.headRenderer != null) g.headRenderer.enabled = !inside;
                foreach (var v in g.visor)
                    if (v != null) v.enabled = inside && g.wasEnabled[v];
            }
        }

        private static string Path(Transform t)
        {
            string p = t.name;
            for (int i = 0; i < 4 && t.parent != null; i++)
            {
                t = t.parent;
                p = t.name + "/" + p;
            }
            return p;
        }
    }
}
