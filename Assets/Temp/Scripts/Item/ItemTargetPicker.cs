using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// アイテムの相手を選ぶ画面。選べる人の頭の上に名前入りのボタンを出し、押されたら Picked で知らせる。
/// 表示とクリックの通知だけを受け持ち、いつ選び始める・やめるかは操作役（LocalPlayerAgent）が決める。
/// </summary>
public class ItemTargetPicker : MonoBehaviour
{
    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private PlayerNameDirectory names;

    [Tooltip("頭の位置を画面に映すときの、自分のカメラ")]
    [SerializeField] private TableInspectCamera viewer;

    [Tooltip("ボタンを並べる親（画面に直接重ねるキャンバスの下）")]
    [SerializeField] private RectTransform container;

    [Tooltip("選んでいる間だけ出す案内")]
    [SerializeField] private TMP_Text guide;

    [SerializeField] private TMP_FontAsset font;

    [SerializeField] private Vector2 buttonSize = new Vector2(220f, 60f);

    [Tooltip("頭からボタンを上げる高さ")]
    [SerializeField] private float headLift = 0.45f;

    [SerializeField] private Color backgroundColor = new Color(0.05f, 0.08f, 0.14f, 0.92f);

    [SerializeField] private Color outlineColor = new Color(0.55f, 0.85f, 1f, 1f);

    [SerializeField] private Color textColor = new Color(0.85f, 0.95f, 1f, 1f);

    readonly Dictionary<int, RectTransform> buttons = new Dictionary<int, RectTransform>();
    readonly HashSet<int> candidates = new HashSet<int>();

    public bool IsPicking { get; private set; }

    /// <summary>相手が選ばれた（プレイヤー番号）</summary>
    public event Action<int> Picked;

    /// <summary>今表示している相手のボタン（案内で明るくするため）</summary>
    public RectTransform[] VisibleButtons => buttons.Values.Where(button => button.gameObject.activeSelf).ToArray();

    void Awake()
    {
        guide.gameObject.SetActive(false);
    }

    /// <summary>players の中から選び始める</summary>
    public void Begin(IEnumerable<int> players, string message)
    {
        candidates.Clear();
        candidates.UnionWith(players);
        guide.text = message;
        guide.gameObject.SetActive(true);
        IsPicking = true;
    }

    public void End()
    {
        IsPicking = false;
        candidates.Clear();
        guide.gameObject.SetActive(false);
        foreach (RectTransform button in buttons.Values)
            button.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (!IsPicking)
            return;

        Camera camera = viewer.LocalCamera;
        foreach (int player in candidates)
        {
            if (!buttons.TryGetValue(player, out RectTransform button))
                buttons[player] = button = CreateButton(player);
            Place(button, player, camera);
        }
    }

    void Place(RectTransform button, int player, Camera camera)
    {
        Vector3 point = Vector3.zero;
        bool visible = false;
        if (camera != null && registry.TryGet(player, out Transform body))
        {
            point = camera.WorldToScreenPoint(PlayerBody.HeadPosition(body) + Vector3.up * headLift);
            visible = point.z > 0f && camera.pixelRect.Contains(point);
        }
        if (button.gameObject.activeSelf != visible)
            button.gameObject.SetActive(visible);
        if (visible)
            button.position = point;
    }

    RectTransform CreateButton(int player)
    {
        var go = new GameObject($"Target_Player{player}", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Button));
        go.layer = gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(container, false);
        rect.sizeDelta = buttonSize;

        var background = go.GetComponent<Image>();
        background.color = backgroundColor;
        var outline = go.GetComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(2f, -2f);

        var label = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.layer = gameObject.layer;
        var labelRect = (RectTransform)label.transform;
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 4f);
        labelRect.offsetMax = new Vector2(-8f, -4f);
        var text = label.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 30f;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.text = names.NameOf(player);

        var button = go.GetComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() =>
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            Picked?.Invoke(player);
        });

        go.SetActive(false);
        return rect;
    }
}
