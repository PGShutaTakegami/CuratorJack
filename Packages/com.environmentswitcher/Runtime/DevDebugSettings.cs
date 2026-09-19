using System;
using UnityEngine;

namespace EnvironmentSwitcher
{
    /// <summary>
    /// Development 環境の Debug オーバーレイ／計測表示設定。
    /// </summary>
    [Serializable]
    public class DevDebugSettings
    {
        [Tooltip("Development 時に左下 DEBUG ボタンを出す")]
        public bool enableOverlay = true;

        [Tooltip("ボタン表示名")]
        public string buttonLabel = "DEBUG";

        [Tooltip("左下からのアンカー位置")]
        public Vector2 buttonPosition = new Vector2(16f, 16f);

        public Vector2 buttonSize = new Vector2(120f, 48f);

        public Vector2 panelSize = new Vector2(360f, 420f);

        [Header("標準機能")]
        [Tooltip("シーン変更 UI を出す")]
        public bool enableSceneChange = true;

        [Tooltip("ゲーム終了ボタンを出す")]
        public bool enableQuitGame = true;

        [Tooltip("セーブ／PlayerPrefs 初期化ボタン")]
        public bool enableSaveClear = true;

        [Tooltip("ログファイル出力とフォルダを開く")]
        public bool enableLogFile = true;

        [Tooltip("FPS / メモリを左上にテキスト表示（Dev のみ）")]
        public bool enableFpsMemory = true;

        [Tooltip("ログを右上にテキスト表示（Dev/Stg）")]
        public bool enableOnScreenLog = true;

        [Tooltip("マウスホイールのスクロール感度")]
        [Range(10f, 120f)]
        public float scrollSensitivity = 40f;
    }
}
