using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 在启动场景加载完成后创建常驻的游戏根节点。
    /// 引导逻辑放在代码里而不是场景组件上，这样即使项目是从任意场景进入播放模式，
    /// 游戏也能正常启动，不依赖某个场景是否被预先打开。
    /// </summary>
    public static class GameBootstrap
    {
        // 根节点名，同时充当层级窗口里能一眼认出的入口标记。
        const string RootName = "GoToHeaven";

        // 挂在场景加载之后而不是 Awake：任何场景加载完毕都会自动执行一次，无需场景挂脚本。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            // 双重检查：切场景后本方法会再次触发，而且场景里也可能手工放了 GameManager。
            if (GameManager.Instance != null) return;
            if (Object.FindObjectOfType<GameManager>() != null) return;

            var root = new GameObject(RootName);
            // 根节点必须常驻，否则首个场景卸载时会把它连同上面的服务一起销毁。
            Object.DontDestroyOnLoad(root);
            root.AddComponent<GameManager>();
        }
    }
}
