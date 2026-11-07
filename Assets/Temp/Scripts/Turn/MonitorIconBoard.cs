using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 自分以外のプレイヤーの頭のモニターの画面いっぱいに、そのプレイヤーのアイコンを表示する（縦横は画面に合わせて引き伸ばす）。
/// 準備中・オークション中を問わず常に表示する。アイコンのない人には何も出さない。
/// 脱落したら、ノイズのかかった素材に切り替える（アイコンのない人はノイズだけの画面を出す）。
/// </summary>
public class MonitorIconBoard : MonoBehaviour
{
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private PlayerIconDirectory icons;

    [Tooltip("生きている間の素材（CJ/CRTScreen。アイコンを _BaseMap に入れる）")]
    [SerializeField] private Material aliveMaterial;

    [Tooltip("脱落したあとの素材（CJ/CRTScreen のノイズを強めたもの）")]
    [SerializeField] private Material droppedOutMaterial;

    [Tooltip("モニターの画面のオブジェクト名")]
    [SerializeField] private string screenName = "Monitor_Screen";

    [Tooltip("画面の面から手前へ浮かせる距離")]
    [SerializeField] private float surfaceOffset = 0.01f;

    sealed class Monitor
    {
        public MeshRenderer renderer;
        public Texture shownIcon;
        public bool isShown;
        public bool isDroppedOut;
    }

    readonly Dictionary<int, Monitor> monitors = new Dictionary<int, Monitor>();
    MaterialPropertyBlock properties;
    Mesh quad;

    void Awake()
    {
        properties = new MaterialPropertyBlock();
        quad = CreateQuad();
    }

    void OnDestroy()
    {
        foreach (Monitor monitor in monitors.Values)
        {
            if (monitor.renderer != null)
                Destroy(monitor.renderer.transform.parent.gameObject);
        }
        if (quad != null)
            Destroy(quad);
    }

    void Update()
    {
        foreach (int playerNumber in registry.PlayerNumbers)
        {
            if (playerNumber == registry.LocalPlayerNumber)
                continue;

            bool isDroppedOut = game.TryGet(playerNumber, out PlayerState player) && !player.IsAlive;
            icons.TryGetIcon(playerNumber, out Texture2D icon);
            bool show = icon != null || isDroppedOut;

            if (!monitors.TryGetValue(playerNumber, out Monitor monitor) || monitor.renderer == null)
            {
                if (!show || !TryCreateMonitor(playerNumber, out monitor))
                    continue;
                monitors[playerNumber] = monitor;
            }

            Show(monitor, show, icon, isDroppedOut);
        }
    }

    void Show(Monitor monitor, bool show, Texture icon, bool isDroppedOut)
    {
        if (monitor.isShown == show && monitor.shownIcon == icon && monitor.isDroppedOut == isDroppedOut)
            return;

        monitor.isShown = show;
        monitor.shownIcon = icon;
        monitor.isDroppedOut = isDroppedOut;
        monitor.renderer.gameObject.SetActive(show);
        if (!show)
            return;

        monitor.renderer.sharedMaterial = isDroppedOut ? droppedOutMaterial : aliveMaterial;
        properties.Clear();
        if (icon != null)
            properties.SetTexture(BaseMapId, icon);
        monitor.renderer.SetPropertyBlock(properties);
    }

    bool TryCreateMonitor(int playerNumber, out Monitor monitor)
    {
        monitor = null;
        if (!registry.TryGet(playerNumber, out Transform player))
            return false;
        if (!HeadMonitorScreen.TryCreateRoot(player, screenName, surfaceOffset, $"MonitorIcon_Player{playerNumber}", out Transform root, out Vector2 size))
            return false;

        var face = new GameObject("Face");
        face.transform.SetParent(root, false);
        face.transform.localScale = new Vector3(size.x, size.y, 1f);
        face.AddComponent<MeshFilter>().sharedMesh = quad;

        var renderer = face.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        face.SetActive(false);

        monitor = new Monitor { renderer = renderer };
        return true;
    }

    /// <summary>幅と高さが 1 の板。手前（-Z）から見て左下が UV の 0</summary>
    static Mesh CreateQuad()
    {
        var mesh = new Mesh { name = "MonitorIconQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
