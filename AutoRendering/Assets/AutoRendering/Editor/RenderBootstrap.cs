using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityAutoRendering
{
    public static class RenderBootstrap
    {
        public static int Take { get; private set; } = 1;

        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();

            string sceneName = null;
            Take = 1;

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-scene")
                    sceneName = args[i + 1];
                else if (args[i] == "-take" && int.TryParse(args[i + 1], out var take))
                    Take = take;
            }

            if (string.IsNullOrEmpty(sceneName))
            {
                EditorApplication.Exit(1);
                return;
            }

            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            var scenePath = sceneGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileName(p) == sceneName);

            if (string.IsNullOrEmpty(scenePath))
            {
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(scenePath);
            EditorApplication.EnterPlaymode();
        }
    }
}
