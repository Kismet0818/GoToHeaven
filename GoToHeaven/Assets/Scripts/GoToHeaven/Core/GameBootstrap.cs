using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// Creates the persistent game root after the boot scene has loaded.
    /// Keeping the bootstrapping in code (instead of a scene component) means the
    /// game also starts correctly when the project is opened on any other scene.
    /// </summary>
    public static class GameBootstrap
    {
        const string RootName = "GoToHeaven";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if (GameManager.Instance != null) return;
            if (Object.FindObjectOfType<GameManager>() != null) return;

            var root = new GameObject(RootName);
            Object.DontDestroyOnLoad(root);
            root.AddComponent<GameManager>();
        }
    }
}
