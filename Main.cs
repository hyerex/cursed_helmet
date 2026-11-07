using ModLoader.Framework;
using ModLoader.Framework.Attributes;
using UnityEngine;

[assembly: System.Reflection.AssemblyVersion("0.3.0")]

namespace CoolGlasses
{
    // No Harmony patches; the mod loader would apply them itself anyway (don't call PatchAll here)
    [ItemId("hyerex.coolglasses")]
    public class Main : VtolMod
    {
        private GameObject host;

        private void Awake()
        {
            Debug.Log($"[CoolGlasses] Loaded v{typeof(Main).Assembly.GetName().Version.ToString(3)}");
            // The mod object gets no Update in missions; this one does, and survives scene loads
            host = new GameObject("CoolGlassesHost");
            DontDestroyOnLoad(host);
            host.AddComponent<GlassesHost>();
        }

        public override void UnLoad()
        {
            if (host != null) DestroyImmediate(host);
            Debug.Log("[CoolGlasses] Unloaded");
        }
    }
}
