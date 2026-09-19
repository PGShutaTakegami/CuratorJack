using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace EnvironmentSwitcher
{
    /// <summary>
    /// Dev/Stg 向けの枠なし HUD。
    /// 左上: FPS/メモリ、右上: ログ、右下: Dev/Stg のみ。
    /// </summary>
    public sealed class EnvironmentStatusHud : MonoBehaviour
    {
        private const string BootstrapObjectName = "EnvironmentSwitcher_StatusHud";
        private const int MaxLogLines = 12;

        private Text _envText;
        private Text _perfText;
        private Text _logText;
        private Text _networkText;
        private bool _showPerf;
        private bool _showNetworkHud;

        private float _fpsTimer;
        private int _fpsFrames;
        private float _fpsValue;
        private float _networkHudTimer;

        private readonly Queue<string> _logLines = new Queue<string>();
        private readonly object _logGate = new object();
        private readonly List<string> _pendingLogs = new List<string>();
        private bool _logDirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnvironmentRuntime.ApplyAllPolicies();

            if (!ShouldShow(EnvironmentRuntime.Current))
            {
                return;
            }

            if (UnityEngine.Object.FindFirstObjectByType<EnvironmentStatusHud>() != null)
            {
                return;
            }

            GameObject host = new GameObject(BootstrapObjectName);
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<EnvironmentStatusHud>();
        }

        private void Start()
        {
            EnvironmentRuntime.ApplyAllPolicies();

            if (!ShouldShow(EnvironmentRuntime.Current))
            {
                Destroy(gameObject);
                return;
            }

            _showPerf = EnvironmentRuntime.Current == GameEnvironment.Development
                        && EnvironmentRuntime.Settings != null
                        && EnvironmentRuntime.Settings.DevDebug.enableFpsMemory;

            _showNetworkHud = EnvironmentRuntime.Current == GameEnvironment.Development;

            bool showLog = true;
            if (EnvironmentRuntime.Current == GameEnvironment.Development
                && EnvironmentRuntime.Settings != null)
            {
                showLog = EnvironmentRuntime.Settings.DevDebug.enableOnScreenLog;
            }

            BuildHud(showLog);
            if (showLog)
            {
                Application.logMessageReceivedThreaded += HandleLogThreaded;
            }
        }

        private void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= HandleLogThreaded;
        }

        private void Update()
        {
            UpdatePerf();
            UpdateNetworkHud();
            FlushLogsToUi();
        }

        private void BuildHud(bool showLog)
        {
            GameObject canvasGo = new GameObject(
                "EnvironmentStatusCanvas",
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4990;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Font font = ResolveUiFont();
            Color envColor = EnvironmentRuntime.Current == GameEnvironment.Staging
                ? new Color(1f, 0.75f, 0.25f, 1f)
                : new Color(0.45f, 1f, 0.55f, 1f);

            // 右下: Dev / Stg のみ（枠なし）
            _envText = CreatePlainText(
                canvasGo.transform,
                "EnvLabel",
                font,
                22,
                FontStyle.Bold,
                new Vector2(1f, 0f),
                new Vector2(-16f, 16f),
                new Vector2(120f, 36f),
                envColor);
            _envText.alignment = TextAnchor.LowerRight;
            _envText.text = EnvironmentRuntime.Current == GameEnvironment.Staging ? "Stg" : "Dev";

            // 左上: FPS / メモリ（枠なし）
            if (_showPerf)
            {
                _perfText = CreatePlainText(
                    canvasGo.transform,
                    "PerfLabel",
                    font,
                    16,
                    FontStyle.Normal,
                    new Vector2(0f, 1f),
                    new Vector2(16f, -16f),
                    new Vector2(320f, 28f),
                    Color.white);
                _perfText.alignment = TextAnchor.UpperLeft;
                _perfText.text = "FPS -- | -- MB";
            }

            // FPS の下: ネット ON/OFF + パケロス（Dev・枠なし）
            if (_showNetworkHud)
            {
                float networkY = _showPerf ? -48f : -16f;
                _networkText = CreatePlainText(
                    canvasGo.transform,
                    "NetworkLabel",
                    font,
                    15,
                    FontStyle.Normal,
                    new Vector2(0f, 1f),
                    new Vector2(16f, networkY),
                    new Vector2(360f, 28f),
                    Color.white);
                _networkText.alignment = TextAnchor.UpperLeft;
                RefreshNetworkHudText();
            }

            // 右上: ログ（枠なし）
            if (showLog)
            {
                _logText = CreatePlainText(
                    canvasGo.transform,
                    "LogLabel",
                    font,
                    14,
                    FontStyle.Normal,
                    new Vector2(1f, 1f),
                    new Vector2(-16f, -16f),
                    new Vector2(720f, 320f),
                    new Color(1f, 1f, 1f, 0.9f));
                _logText.alignment = TextAnchor.UpperRight;
                _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
                _logText.verticalOverflow = VerticalWrapMode.Truncate;
                _logText.text = string.Empty;
            }
        }

        private void UpdatePerf()
        {
            if (!_showPerf || _perfText == null)
            {
                return;
            }

            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer < 0.5f)
            {
                return;
            }

            _fpsValue = _fpsFrames / _fpsTimer;
            _fpsFrames = 0;
            _fpsTimer = 0f;
            long memoryMb = GC.GetTotalMemory(false) / (1024 * 1024);
            _perfText.text = $"FPS {_fpsValue:0} | {memoryMb} MB";
        }

        private void UpdateNetworkHud()
        {
            if (!_showNetworkHud || _networkText == null)
            {
                return;
            }

            _networkHudTimer += Time.unscaledDeltaTime;
            if (_networkHudTimer < 0.5f)
            {
                return;
            }

            _networkHudTimer = 0f;
            RefreshNetworkHudText();
        }

        private void RefreshNetworkHudText()
        {
            bool on = EnvironmentNetwork.IsEnabled;
            float loss = EnvironmentNetwork.PacketLossPercent;
            string lossLabel = loss < 0f ? "ロス --%" : $"ロス {loss:0.0}%";
            _networkText.text = $"{(on ? "NET ON" : "NET OFF")}  {lossLabel}";
        }

        private void HandleLogThreaded(string condition, string stackTrace, LogType type)
        {
            string line = $"[{type}] {TrimOneLine(condition)}";
            lock (_logGate)
            {
                _pendingLogs.Add(line);
            }
        }

        private void FlushLogsToUi()
        {
            lock (_logGate)
            {
                if (_pendingLogs.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < _pendingLogs.Count; i++)
                {
                    _logLines.Enqueue(_pendingLogs[i]);
                    while (_logLines.Count > MaxLogLines)
                    {
                        _logLines.Dequeue();
                    }
                }

                _pendingLogs.Clear();
                _logDirty = true;
            }

            if (!_logDirty || _logText == null)
            {
                return;
            }

            var sb = new StringBuilder();
            foreach (string line in _logLines)
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }

                sb.Append(line);
            }

            _logText.text = sb.ToString();
            _logDirty = false;
        }

        private static string TrimOneLine(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string one = value.Replace('\n', ' ').Replace('\r', ' ');
            return one.Length > 120 ? one.Substring(0, 117) + "..." : one;
        }

        private static Text CreatePlainText(
            Transform parent,
            string name,
            Font font,
            int fontSize,
            FontStyle style,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private static bool ShouldShow(GameEnvironment environment)
        {
            return environment == GameEnvironment.Development
                   || environment == GameEnvironment.Staging;
        }

        private static Font ResolveUiFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }
    }
}
