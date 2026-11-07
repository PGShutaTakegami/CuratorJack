using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アイコンを描く画面。左に白いキャンバス、右にカラーサークルと道具（太さ・消しゴム・全部消す・1 つ戻す・1 つ進む）。
/// 決定で PlayerIconStore に保存し、キャンセルでは何も残さない。
/// </summary>
public class IconPainterWindow : MonoBehaviour
{
    [SerializeField] GameObject view;
    [SerializeField] RawImage canvasImage;
    [SerializeField] IconCanvasSurface surface;
    [SerializeField] ColorWheel colorWheel;
    [SerializeField] Slider brightnessSlider;
    [SerializeField] Image colorPreview;

    [Header("道具")]
    [SerializeField] Button[] sizeButtons;
    [SerializeField] float[] brushRadii = { 1f, 2.5f, 5f };
    [SerializeField] Button eraserButton;
    [SerializeField] Button clearButton;
    [SerializeField] Button undoButton;
    [SerializeField] Button redoButton;
    [SerializeField] Button okButton;
    [SerializeField] Button cancelButton;
    [SerializeField] int maxHistory = 30;

    [Header("選んでいる道具の色")]
    [SerializeField] Color normalToolColor = new(0.12f, 0.1f, 0.08f, 0.9f);
    [SerializeField] Color selectedToolColor = new(0.5f, 0.38f, 0.16f, 0.95f);

    IconCanvas canvas;
    Texture2D texture;
    int sizeIndex = 1;
    bool isErasing;
    bool isDrawing;
    Vector2 lastPoint;

    public bool IsOpen => view != null && view.activeSelf;

    /// <summary>決定・キャンセルで閉じた</summary>
    public event Action Closed;

    void Awake()
    {
        canvas = new IconCanvas(PlayerIconStore.Size, new Color32(255, 255, 255, 255), maxHistory);
        texture = new Texture2D(PlayerIconStore.Size, PlayerIconStore.Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        canvasImage.texture = texture;

        for (int i = 0; i < sizeButtons.Length; i++)
        {
            int index = i;
            sizeButtons[i].onClick.AddListener(() => SelectSize(index));
        }
        eraserButton.onClick.AddListener(SelectEraser);
        clearButton.onClick.AddListener(ClearAll);
        undoButton.onClick.AddListener(Undo);
        redoButton.onClick.AddListener(Redo);
        okButton.onClick.AddListener(Decide);
        cancelButton.onClick.AddListener(Close);

        view.SetActive(false);
    }

    void OnEnable()
    {
        surface.StrokeBegan += BeginStroke;
        surface.StrokeMoved += ContinueStroke;
        surface.StrokeEnded += EndStroke;
        colorWheel.Changed += OnWheelChanged;
        brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
    }

    void OnDisable()
    {
        surface.StrokeBegan -= BeginStroke;
        surface.StrokeMoved -= ContinueStroke;
        surface.StrokeEnded -= EndStroke;
        colorWheel.Changed -= OnWheelChanged;
        brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);
    }

    void OnDestroy()
    {
        if (texture != null)
            Destroy(texture);
    }

    /// <summary>保存しているアイコン（なければ白紙）を読み込んで開く</summary>
    public void Open()
    {
        if (IsOpen) return;

        canvas.Load(LoadSavedPixels());
        isDrawing = false;
        isErasing = false;
        view.SetActive(true);
        UpdatePreview();
        RefreshCanvas();
    }

    static Color32[] LoadSavedPixels()
    {
        Texture2D saved = PlayerIconStore.CreateTexture(PlayerIconStore.Current);
        if (saved == null) return null;

        Color32[] pixels = saved.width == PlayerIconStore.Size && saved.height == PlayerIconStore.Size
            ? saved.GetPixels32()
            : null;
        Destroy(saved);
        return pixels;
    }

    void Decide()
    {
        PlayerIconStore.Save(texture.EncodeToPNG());
        Close();
    }

    void Close()
    {
        if (!IsOpen) return;

        isDrawing = false;
        view.SetActive(false);
        Closed?.Invoke();
    }

    void BeginStroke(Vector2 uv)
    {
        isDrawing = true;
        lastPoint = ToPixel(uv);
        canvas.BeginStroke();
        canvas.DrawLine(lastPoint, lastPoint, CurrentRadius, CurrentColor);
        RefreshCanvas();
    }

    void ContinueStroke(Vector2 uv)
    {
        if (!isDrawing) return;

        Vector2 point = ToPixel(uv);
        canvas.DrawLine(lastPoint, point, CurrentRadius, CurrentColor);
        lastPoint = point;
        RefreshCanvas();
    }

    void EndStroke()
    {
        isDrawing = false;
    }

    void SelectSize(int index)
    {
        sizeIndex = index;
        isErasing = false;
        RefreshTools();
    }

    void SelectEraser()
    {
        isErasing = true;
        RefreshTools();
    }

    void ClearAll()
    {
        canvas.Clear();
        RefreshCanvas();
    }

    void Undo()
    {
        if (canvas.Undo())
            RefreshCanvas();
    }

    void Redo()
    {
        if (canvas.Redo())
            RefreshCanvas();
    }

    /// <summary>真っ黒（明るさ 0）のままだと円で選んだ色が出ないので、円を触ったら明るさを最大にする</summary>
    void OnWheelChanged()
    {
        if (brightnessSlider.value <= 0.01f)
            brightnessSlider.SetValueWithoutNotify(1f);
        OnColorChanged();
    }

    void OnColorChanged()
    {
        isErasing = false;
        UpdatePreview();
        RefreshTools();
    }

    void OnBrightnessChanged(float _) => OnColorChanged();

    float CurrentRadius => brushRadii[Mathf.Clamp(sizeIndex, 0, brushRadii.Length - 1)];

    Color32 CurrentColor => isErasing ? canvas.Background : PenColor;

    Color PenColor => Color.HSVToRGB(colorWheel.Hue, colorWheel.Saturation, brightnessSlider.value);

    Vector2 ToPixel(Vector2 uv) => uv * canvas.Size;

    void UpdatePreview()
    {
        colorPreview.color = PenColor;
    }

    void RefreshCanvas()
    {
        texture.SetPixels32(canvas.Pixels);
        texture.Apply();
        undoButton.interactable = canvas.CanUndo;
        redoButton.interactable = canvas.CanRedo;
        RefreshTools();
    }

    void RefreshTools()
    {
        for (int i = 0; i < sizeButtons.Length; i++)
            sizeButtons[i].image.color = i == sizeIndex ? selectedToolColor : normalToolColor;
        eraserButton.image.color = isErasing ? selectedToolColor : normalToolColor;
    }
}
