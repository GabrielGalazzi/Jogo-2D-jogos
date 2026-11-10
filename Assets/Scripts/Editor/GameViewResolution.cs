using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RicochetPoC.EditorTools
{
    /// <summary>
    /// Adds a fixed 1680×840 entry to the Game view's resolution menu and selects it, so Play in the editor matches
    /// the standalone window. Runs once per editor session (and from Ricochet ▸ Game View 1680x840).
    /// Unity has no public API for this, so it goes through the internal GameViewSizes types; if a future Unity
    /// version changes them it logs a warning and the size can still be picked by hand.
    /// </summary>
    [InitializeOnLoad]
    static class GameViewResolution
    {
        const int Width = 1680;
        const int Height = 840;
        const string Label = "Ricochet";
        const string SessionKey = "RicochetPoC.GameViewResolution";
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        static GameViewResolution()
        {
            if (!SessionState.GetBool(SessionKey, false))
                EditorApplication.delayCall += () => SessionState.SetBool(SessionKey, Apply(false));
        }

        [MenuItem("Ricochet/Game View 1680x840")]
        static void ApplyFromMenu() => Apply(true);

        /// <returns>False if there was no Game view to apply it to yet.</returns>
        static bool Apply(bool openGameView)
        {
            try
            {
                var editorAssembly = typeof(Editor).Assembly;
                var sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
                var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
                var group = sizesType.GetProperty("currentGroup", Any).GetValue(sizes);

                int index = FindSize(group);
                if (index < 0)
                {
                    var sizeKind = Enum.Parse(editorAssembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution");
                    var size = Activator.CreateInstance(editorAssembly.GetType("UnityEditor.GameViewSize"), sizeKind, Width, Height, Label);
                    group.GetType().GetMethod("AddCustomSize", Any).Invoke(group, new[] { size });
                    sizesType.GetMethod("SaveToHDD", Any).Invoke(sizes, null);
                    index = FindSize(group);
                }

                var gameViewType = editorAssembly.GetType("UnityEditor.GameView");
                var views = Resources.FindObjectsOfTypeAll(gameViewType);
                var gameView = openGameView ? EditorWindow.GetWindow(gameViewType) : views.Length > 0 ? views[0] : null;
                if (gameView == null)
                    return false;
                gameViewType.GetMethod("SizeSelectionCallback", Any).Invoke(gameView, new object[] { index, null });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't set the Game view to {Width}x{Height}; pick it from the Game view's resolution menu instead. ({e.GetBaseException().Message})");
                return true; // don't retry on every script reload
            }
        }

        static int FindSize(object group)
        {
            var groupType = group.GetType();
            int count = (int)groupType.GetMethod("GetTotalCount", Any).Invoke(group, null);
            var getSize = groupType.GetMethod("GetGameViewSize", Any);
            for (int i = 0; i < count; i++)
            {
                var size = getSize.Invoke(group, new object[] { i });
                var sizeType = size.GetType();
                if ((int)sizeType.GetProperty("width", Any).GetValue(size) == Width
                    && (int)sizeType.GetProperty("height", Any).GetValue(size) == Height
                    && sizeType.GetProperty("sizeType", Any).GetValue(size).ToString() == "FixedResolution")
                    return i;
            }
            return -1;
        }
    }
}
