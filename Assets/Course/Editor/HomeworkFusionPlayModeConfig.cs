#if UNITY_EDITOR
using Fusion;

// MPPM's read-only asset database can miss Fusion's custom .fusion importer.
// Prepare Multiplayer Play Mode exports that same config as a normal Resource.
// Standalone builds continue to use the original Fusion configuration loader.
[assembly: FusionGlobalScriptableObjectResource(typeof(NetworkProjectConfigAsset),
    "HomeworkNetworkProjectConfig", Order = -2000, AllowEditMode = true, AllowFallback = true)]
#endif
