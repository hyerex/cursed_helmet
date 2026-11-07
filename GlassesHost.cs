using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoolGlasses
{
    // Finds the standard ("hqh") helmets, swaps each for a head with a pair of glasses (HeadGlasses),
    // and owns the shared models, materials and the placement values.
    //
    // Keys to fit them in the game (Shift: a quarter step), saved to coolglasses.cfg next to the DLL.
    // The glasses, on the eyes: I/K higher/lower, U/O forward/back, L/J larger/smaller, N/M tilt.
    // Pushed up: Y/H more/less flip, T/G lift. P writes the values to the log.
    // The head, with Ctrl held: I/K higher/lower, U/O forward/back, T/G right/left, L/J larger/smaller,
    // N/M turn, Y/H tilt, B a quarter turn. Ctrl+P writes the values to the log.
    public class GlassesHost : MonoBehaviour
    {
        // How often new helmets are looked for (seconds)
        private const float ScanInterval = 2f;

        // Placement, in the helmet's frame: forward and up in half lengths/heights of the helmet shell
        // from its centre, the front's width in metres, tilt in degrees (the eye values are the ones
        // measured for the glasses inside the helmet in HelmetLivery). Pushed up: degrees the glasses
        // turn up around the ears, and metres they rise.
        public static float Forward = 0.65f, Up = -0.21f, Width = 0.16f, Tilt = 0f, FlipAngle = 60f, Lift = 0.04f;

        // The head, in the helmet's frame: its middle forward and up in half lengths/heights of the
        // helmet shell from the shell's centre, right in metres; its size (times the model's own,
        // 0: as tall as HeadHeight), turn and tilt in degrees
        // (The scan faces its -x and looks a little up: a quarter turn, tipped forward a little.)
        public static float HeadForward = 0f, HeadUp = -0.25f, HeadRight = 0f, HeadScale = 0f, HeadYaw = 90f, HeadPitch = 10f;
        // Crown to neck, the scan's whole height
        private const float HeadHeight = 0.26f;

        public static Mesh HeadMesh { get; private set; }
        // The scan's own colours (with its normal map and ambient occlusion); null: they couldn't be
        // loaded, the head takes the pilot's skin colour instead
        public static Material HeadMaterial { get; private set; }
        private static readonly List<Texture2D> headTextures = new List<Texture2D>();
        // For heads without the game's skin sphere to take the skin colour from
        public static Material SkinMaterial { get; private set; }

        public static Mesh GlassesMesh { get; private set; }
        public static Material FrameMaterial { get; private set; }
        public static Material LensMaterial { get; private set; }
        // The hinge the glasses turn around when pushed up: back along the temples, in model units
        // (the model's front is 1 wide)
        public static Vector3 Pivot { get; private set; }

        private static string CfgPath => Path.Combine(Path.GetDirectoryName(typeof(GlassesHost).Assembly.Location) ?? ".", "coolglasses.cfg");

        private float nextScan;
        private bool failed;

        private void Awake()
        {
            LoadValues();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            // The helmets back
            foreach (var g in HeadGlasses.All.ToArray()) if (g != null) DestroyImmediate(g.gameObject);
            if (GlassesMesh != null) Destroy(GlassesMesh);
            if (HeadMesh != null) Destroy(HeadMesh);
            if (SkinMaterial != null) Destroy(SkinMaterial);
            if (HeadMaterial != null) Destroy(HeadMaterial);
            foreach (var t in headTextures) if (t != null) Destroy(t);
            headTextures.Clear();
            if (FrameMaterial != null) Destroy(FrameMaterial);
            if (LensMaterial != null) Destroy(LensMaterial);
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode mode) => nextScan = Time.unscaledTime + 0.5f;

        private void Update()
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + ScanInterval;
                Scan();
            }
            Keys();
        }

        private void Scan()
        {
            if (failed || !Ready()) return;
            try
            {
                foreach (var mf in FindObjectsOfType<MeshFilter>())
                {
                    if (mf.sharedMesh == null || mf.sharedMesh.name != "helmet") continue;
                    Transform root = mf.transform.parent;
                    if (root == null || root.Find(HeadGlasses.ObjectName) != null) continue;
                    var r = mf.GetComponent<MeshRenderer>();
                    if (r == null || !IsShell(r)) continue;
                    HeadGlasses.Create(root, mf);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CoolGlasses] Looking for helmets failed: {e}");
            }
        }

        // A part of the standard helmet with its atlas on it (also as recoloured by HelmetLivery)
        public static bool IsShell(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || !m.HasProperty("_MainTex")) continue;
                Texture t = m.mainTexture;
                if (t != null && t.name.StartsWith("hqh_dif")) return true;
            }
            return false;
        }

        public static bool IsVisor(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
                if (m != null && m.name.StartsWith("mat_hqHelmetVisor")) return true;
            return false;
        }

        // ---- The model ----

        private bool Ready()
        {
            if (GlassesMesh != null && FrameMaterial != null && LensMaterial != null) return true;
            HeadMesh = LoadHead();
            SkinMaterial = MakeMaterial("CoolGlassesSkin", new Color(0.86f, 0.66f, 0.52f), 0.25f, 0f);
            if (HeadMesh == null) Debug.LogWarning("[CoolGlasses] The head model is missing: glasses only");
            else
            {
                HeadMaterial = MakeHeadMaterial();
                if (HeadScale <= 0f) HeadScale = HeadHeight / Mathf.Max(1e-4f, HeadMesh.bounds.size.y);
            }
            GlassesMesh = LoadMesh();
            // Black and glossy, like the glasses in HelmetLivery
            FrameMaterial = MakeMaterial("CoolGlassesFrame", new Color(0.05f, 0.05f, 0.05f), 0.8f, 0.6f);
            LensMaterial = MakeMaterial("CoolGlassesLenses", Color.black, 0.95f, 0f);
            if (GlassesMesh != null && FrameMaterial != null && LensMaterial != null)
            {
                Bounds b = GlassesMesh.bounds;
                // The temples' ends rest on the ears
                Pivot = new Vector3(0f, 0f, b.min.z * 0.85f);
                return true;
            }
            failed = true;
            Debug.LogWarning("[CoolGlasses] The glasses can't be shown (model or shader missing)");
            return false;
        }

        // Assets\glasses.bin: int vertexCount, per vertex position and normal (6 floats); int
        // partCount (frame, lenses), per part int indexCount and the indices
        private static Mesh LoadMesh()
        {
            using (var s = typeof(GlassesHost).Assembly.GetManifestResourceStream("CoolGlasses.Glasses.bin"))
            {
                if (s == null) return null;
                var r = new BinaryReader(s);
                int n = r.ReadInt32();
                // Every vertex twice, the second with its normal turned around: the lenses are single
                // faces and need a back side
                var v = new Vector3[2 * n];
                var nrm = new Vector3[2 * n];
                for (int i = 0; i < n; i++)
                {
                    v[i] = v[n + i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    nrm[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    nrm[n + i] = -nrm[i];
                }
                var mesh = new Mesh { name = "CoolGlasses", vertices = v, normals = nrm };
                int parts = r.ReadInt32();
                if (parts != 2) return null;
                mesh.subMeshCount = parts;
                for (int part = 0; part < parts; part++)
                {
                    int m = r.ReadInt32();
                    var idx = new int[2 * m];
                    for (int i = 0; i < m; i++) idx[i] = r.ReadInt32();
                    for (int i = 0; i < m; i += 3)
                    {
                        idx[m + i] = n + idx[i];
                        idx[m + i + 1] = n + idx[i + 2];
                        idx[m + i + 2] = n + idx[i + 1];
                    }
                    mesh.SetTriangles(idx, part);
                }
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // Assets\head.bin: int vertexCount, per vertex position, normal, uv (8 floats); int
        // indexCount and the indices
        private static Mesh LoadHead()
        {
            using (var s = typeof(GlassesHost).Assembly.GetManifestResourceStream("CoolGlasses.Head.bin"))
            {
                if (s == null) return null;
                var r = new BinaryReader(s);
                int n = r.ReadInt32();
                var v = new Vector3[n];
                var nrm = new Vector3[n];
                var uv = new Vector2[n];
                for (int i = 0; i < n; i++)
                {
                    v[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    nrm[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                }
                int m = r.ReadInt32();
                var idx = new int[m];
                for (int i = 0; i < m; i++) idx[i] = r.ReadInt32();
                var mesh = new Mesh { name = "CoolGlassesHead", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.vertices = v;
                mesh.normals = nrm;
                mesh.uv = uv;
                mesh.triangles = idx;
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // Standard, lit: the colour texture, the normal map (linear) and the ambient occlusion. Loaded
        // from the embedded JPEGs and compressed on the GPU (2048² each).
        private static Material MakeHeadMaterial()
        {
            Texture2D colour = LoadTexture("CoolGlasses.HeadColor.jpg", false);
            if (colour == null)
            {
                Debug.LogWarning("[CoolGlasses] The head's texture is missing: skin colour only");
                return null;
            }
            var m = MakeMaterial("CoolGlassesHead", Color.white, 0.3f, 0f);
            if (m == null) return null;
            m.mainTexture = colour;
            Texture2D normal = LoadTexture("CoolGlasses.HeadNormal.jpg", true);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            Texture2D ao = LoadTexture("CoolGlasses.HeadAO.jpg", true);
            if (ao != null) m.SetTexture("_OcclusionMap", ao);
            return m;
        }

        private static Texture2D LoadTexture(string resource, bool linear)
        {
            using (var s = typeof(GlassesHost).Assembly.GetManifestResourceStream(resource))
            {
                if (s == null) return null;
                var ms = new MemoryStream();
                s.CopyTo(ms);
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear) { name = resource, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
                if (!t.LoadImage(ms.ToArray()))
                {
                    Destroy(t);
                    return null;
                }
                t.Compress(true);
                t.Apply(true, true);
                headTextures.Add(t);
                return t;
            }
        }

        private static Material MakeMaterial(string name, Color colour, float smoothness, float metallic)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) return null;
            var m = new Material(shader) { name = name, color = colour };
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            return m;
        }

        // ---- Fitting them in the game ----

        private static void Keys()
        {
            if (!Input.anyKeyDown) return;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                HeadKeys();
                return;
            }
            float k = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 0.25f : 1f;
            bool changed = false;
            void Step(KeyCode key, ref float value, float by)
            {
                if (!Input.GetKeyDown(key)) return;
                value += by * k;
                changed = true;
            }
            Step(KeyCode.I, ref Up, 0.02f);
            Step(KeyCode.K, ref Up, -0.02f);
            Step(KeyCode.U, ref Forward, 0.02f);
            Step(KeyCode.O, ref Forward, -0.02f);
            Step(KeyCode.L, ref Width, 0.005f);
            Step(KeyCode.J, ref Width, -0.005f);
            Step(KeyCode.N, ref Tilt, 2f);
            Step(KeyCode.M, ref Tilt, -2f);
            Step(KeyCode.Y, ref FlipAngle, 5f);
            Step(KeyCode.H, ref FlipAngle, -5f);
            Step(KeyCode.T, ref Lift, 0.005f);
            Step(KeyCode.G, ref Lift, -0.005f);
            Width = Mathf.Max(0.02f, Width);
            if (Input.GetKeyDown(KeyCode.P)) Debug.Log($"[CoolGlasses] {Values}");
            if (!changed) return;
            SaveValues();
            Debug.Log($"[CoolGlasses] {Values}");
        }

        // With Ctrl held: the head (Shift: a fifth of a step)
        private static void HeadKeys()
        {
            bool fine = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float move = fine ? 0.004f : 0.02f, metres = fine ? 0.001f : 0.005f, size = fine ? 1.01f : 1.05f, turn = fine ? 1f : 5f;
            bool changed = true;
            if (Input.GetKeyDown(KeyCode.I)) HeadUp += move;
            else if (Input.GetKeyDown(KeyCode.K)) HeadUp -= move;
            else if (Input.GetKeyDown(KeyCode.U)) HeadForward += move;
            else if (Input.GetKeyDown(KeyCode.O)) HeadForward -= move;
            else if (Input.GetKeyDown(KeyCode.T)) HeadRight += metres;
            else if (Input.GetKeyDown(KeyCode.G)) HeadRight -= metres;
            else if (Input.GetKeyDown(KeyCode.L)) HeadScale *= size;
            else if (Input.GetKeyDown(KeyCode.J)) HeadScale /= size;
            else if (Input.GetKeyDown(KeyCode.N)) HeadYaw += turn;
            else if (Input.GetKeyDown(KeyCode.M)) HeadYaw -= turn;
            else if (Input.GetKeyDown(KeyCode.Y)) HeadPitch += turn;
            else if (Input.GetKeyDown(KeyCode.H)) HeadPitch -= turn;
            else if (Input.GetKeyDown(KeyCode.B)) HeadYaw += 90f;
            else changed = false;
            HeadYaw = Mathf.Repeat(HeadYaw, 360f);
            HeadScale = Mathf.Clamp(HeadScale, 0.01f, 100f);
            if (Input.GetKeyDown(KeyCode.P)) Debug.Log($"[CoolGlasses] Head: {HeadValues}");
            if (!changed) return;
            SaveValues();
            Debug.Log($"[CoolGlasses] Head: {HeadValues}");
        }

        private static string HeadValues => string.Format(CultureInfo.InvariantCulture,
            "forward {0:0.000}, up {1:0.000}, right {2:0.000} m, scale {3:0.000} ({4:0.000} m tall), turn {5:0.0}°, tilt {6:0.0}°",
            HeadForward, HeadUp, HeadRight, HeadScale, HeadMesh != null ? HeadMesh.bounds.size.y * HeadScale : 0f, HeadYaw, HeadPitch);

        private static void SaveValues()
        {
            try
            {
                File.WriteAllText(CfgPath, string.Format(CultureInfo.InvariantCulture,
                    "forward = {0:0.000}\nup = {1:0.000}\nwidth = {2:0.000}\ntilt = {3:0.0}\nflip = {4:0.0}\nlift = {5:0.000}\n" +
                    "head_forward = {6:0.000}\nhead_up = {7:0.000}\nhead_right = {8:0.000}\nhead_scale = {9:0.0000}\nhead_yaw = {10:0.0}\nhead_pitch = {11:0.0}\n",
                    Forward, Up, Width, Tilt, FlipAngle, Lift, HeadForward, HeadUp, HeadRight, HeadScale, HeadYaw, HeadPitch));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CoolGlasses] Could not save {CfgPath}: {e.Message}");
            }
        }

        private static string Values => string.Format(CultureInfo.InvariantCulture,
            "forward {0:0.000}, up {1:0.000}, width {2:0.000} m, tilt {3:0.0}°, flip {4:0.0}°, lift {5:0.000} m",
            Forward, Up, Width, Tilt, FlipAngle, Lift);

        private static void LoadValues()
        {
            try
            {
                if (!File.Exists(CfgPath)) return;
                foreach (string line in File.ReadAllLines(CfgPath))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    if (!float.TryParse(line.Substring(eq + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) continue;
                    switch (line.Substring(0, eq).Trim())
                    {
                        case "forward": Forward = v; break;
                        case "up": Up = v; break;
                        case "width": Width = v; break;
                        case "tilt": Tilt = v; break;
                        case "flip": FlipAngle = v; break;
                        case "lift": Lift = v; break;
                        case "head_forward": HeadForward = v; break;
                        case "head_up": HeadUp = v; break;
                        case "head_right": HeadRight = v; break;
                        case "head_scale": HeadScale = v; break;
                        case "head_yaw": HeadYaw = v; break;
                        case "head_pitch": HeadPitch = v; break;
                    }
                }
                Debug.Log($"[CoolGlasses] Read {Values}; head {HeadValues}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CoolGlasses] Could not read {CfgPath}: {e.Message}");
            }
        }
    }
}
