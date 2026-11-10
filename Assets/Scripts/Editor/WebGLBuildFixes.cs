using UnityEditor;

namespace Ricochet.EditorTools
{
    /// <summary>
    /// App UI (pulled in by com.unity.ai.inference) ships a WebGL TextMate library compiled with an
    /// older Emscripten. It calls embind functions that this editor's toolchain no longer has, so the
    /// WebGL link fails. The game never uses it, so it is left out of every player build.
    /// </summary>
    [InitializeOnLoad]
    static class WebGLBuildFixes
    {
        const string TextMateLibrary = "/WebGL/Plugins/libAppUITextMateLib.a";

        static WebGLBuildFixes()
        {
            foreach (var importer in PluginImporter.GetAllImporters())
            {
                if (importer.assetPath.EndsWith(TextMateLibrary))
                    importer.SetIncludeInBuildDelegate(_ => false);
            }
        }
    }
}
