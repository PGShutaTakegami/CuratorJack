# Environment Switcher

Unity 6 向け UPM パッケージ。Development / Staging / Release の 3 環境を Editor から切り替え、Scripting Define Symbols の適用、実行時オーバーレイ、Dev 用 DEBUG パネル、Release ビルドガードまでをまとめて提供します。

## 機能

- **環境切替** — Dev / Stg / Prod を選択し、Define を一括適用
- **環境ごとの設定** — API URL、クラッシュレポート、解析/IAP サンドボックス、セーブ分離
- **実行時 UI** — 環境バッジ、FPS/メモリ（Dev）、画面ログ（Dev/Stg）、ネットワーク状態
- **Dev DEBUG パネル** — シーン変更、終了、セーブ初期化などの標準機能 + ゲーム側拡張
- **Production ガード** — Release ビルド時の Define 不整合・Dev シンボル残存を検出
- **ネット通信フラグ** — `ENV_NETWORK` と Settings を連動（通信自体はゲーム側で実装）

## 要件

- Unity **6000.0** 以上（Unity 6）
- `com.unity.ugui`

## インストール

### Git URL（UPM）

Package Manager → **Add package from git URL...**

```
https://github.com/PGShutaTakegami/EnvironmentSwitcher.git?path=/com.environmentswitcher
```

### ローカル参照

`Packages/manifest.json` に追加:

```json
{
  "dependencies": {
    "com.environmentswitcher": "file:../path/to/com.environmentswitcher"
  }
}
```

## クイックスタート

1. パッケージをインストールする
2. `Assets/Resources/EnvironmentSettings.asset` を作成する  
   （Project 右クリック → **Create → Environment Switcher → Environment Settings**）
3. **Window → Environment Switcher**（または **Tools → Environment Switcher**）を開く
4. 環境を選び **Apply Environment** を押す（Scripting Define が更新され、再コンパイルされます）

> **重要:** `EnvironmentSettings` は `Resources/EnvironmentSettings` として配置してください。実行時は `Resources.Load` で読み込みます。

## Editor（Environment Switcher ウィンドウ）

| セクション | 内容 |
|---|---|
| **Apply Environment** | 選択した環境の Define を Standalone / Android / iOS / WebGL に適用 |
| **Dev 設定** | DEBUG UI の ON/OFF、標準機能トグル、FPS/ログ表示、スクロール感度 |
| **環境エントリ** | 各環境の API URL、解析/IAP/クラッシュ、セーブ分離 |
| **共通** | ネット通信 ON/OFF（Apply で `ENV_NETWORK` も連動） |

Apply 時は Domain Reload（再コンパイル）が走ります。Inspector 上のネット通信フラグは読み取り専用で、変更は Switcher ウィンドウから行います。

## Scripting Define Symbols

パッケージが管理するシンボル（デフォルト）:

| 環境 | Define |
|---|---|
| Development | `ENV_DEV` |
| Staging | `ENV_STG` |
| Release | `ENV_RELEASE` |
| ネット通信 ON | `ENV_NETWORK` |

`EnvironmentRuntime.Current` は **コンパイル済み Define を最優先** し、Define が無い場合のみ `EnvironmentSettings.ActiveEnvironment` を参照します。

## 実行時の挙動

| 項目 | Dev | Stg | Prod |
|---|---|---|---|
| 右下 | `Dev` / `Stg` テキスト | 同左 | 表示なし |
| 左上 | FPS / メモリ / ネット状態 | なし | なし |
| 右上 | 直近ログ | 直近ログ | なし |
| DEBUG ボタン | あり（設定で OFF 可） | なし | なし（ガードで破棄） |
| Unity ログレベル | Log 以上 | Warning 以上 | Error のみ |
| セーブ分離 | `EnvironmentSave.Key()` | 同左 | 同左 |

Dev DEBUG パネルの標準機能:

- シーン変更
- ゲーム終了
- この環境のセーブ初期化 / PlayerPrefs 全削除
- ログフォルダを開く

## ゲーム側 API

```csharp
using EnvironmentSwitcher;

// 現在の環境（Define 優先）
GameEnvironment env = EnvironmentRuntime.Current;

// 環境別設定
string apiUrl = EnvironmentRuntime.ApiBaseUrl;
bool crash = EnvironmentRuntime.EnableCrashReporting;
bool analyticsSandbox = EnvironmentRuntime.UseAnalyticsSandbox;
string analyticsId = EnvironmentRuntime.AnalyticsAppId;
bool iapSandbox = EnvironmentRuntime.UseIapSandbox;
bool networkOn = EnvironmentRuntime.NetworkEnabled;

// 環境別 PlayerPrefs（キーに環境プレフィックスを付与）
EnvironmentSave.SetInt("score", 100);
int score = EnvironmentSave.GetInt("score");
string key = EnvironmentSave.Key("score"); // 例: "Development.score"
```

Settings をコードから差し替える場合:

```csharp
EnvironmentRuntime.BindSettings(mySettings);
```

## ネットワーク

`EnvironmentNetwork` は **通信 ON/OFF の共通フラグと統計** を提供します。HTTP クライアントそのものは含みません。

```csharp
if (!EnvironmentNetwork.TryBeginRequest("FetchUser"))
{
    return; // 通信 OFF → モックやスキップ
}

try
{
    // ゲーム側の通信処理
    EnvironmentNetwork.ReportSuccess();
}
catch (Exception e)
{
    EnvironmentNetwork.LogException(e, "FetchUser failed");
}
```

Dev 実行時、左上には `NET ON/OFF` とパケロス率（`ReportSuccess` / `ReportFailure` の集計）が表示されます。ゲーム側で Report を呼ばない限り `--%` のままです。

## DEBUG パネルの拡張

ゲーム固有のチートやデバッグ UI は `DevDebugRegistry` で追加します。

```csharp
using EnvironmentSwitcher;
using UnityEngine;

public static class MyGameDebug
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Register()
    {
        DevDebugRegistry.RegisterSection(
            id: "mygame",
            title: "My Game",
            order: 100,
            builder: ctx =>
            {
                ctx.AddButton("add-gold", "+1000 Gold", Color.green, () =>
                {
                    // ゲーム側処理
                });
            });
    }
}
```

`RegisterSection` は遅延呼び出しにも対応しています（`SectionsChanged` でパネルが再構築されます）。

## Production ガード

Release ビルド時、`EnvironmentBuildGuard` が以下を検査し、問題があればビルドを中止します。

- ActiveEnvironment と Define の不整合
- Release ビルドに Dev シンボル（`ENV_DEV` 等）が残存
- Release エントリで IAP / 解析サンドボックスが有効

実行時にも `EnvironmentProductionGuard` が Release で Dev オーバーレイを破棄します。

## 注意事項

- **SDK 連携はゲーム側の責務** — クラッシュレポート、解析、IAP、HTTP 通信の実装は各 SDK / ゲームコードで行ってください。本パッケージはフラグと設定値の提供が中心です。
- **Apply = 再コンパイル** — CI やビルド前は、意図した環境で Apply 済みか確認してください。
- **PlayerPrefs 分離** — `EnvironmentSave.Set*` 経由のキーは追跡され、環境クリアで削除できます。直接 `PlayerPrefs` を使うキーは `EnvironmentSaveKeyHints.Register` でヒント登録できます。

## ライセンス

[`LICENSE`](./LICENSE) を参照してください。

要約（詳細は LICENSE 本文が優先）:

- **使用・改造**: OK（自己利用）
- **二次配布**: 禁止（パッケージ／ソースの再公開・譲渡など）
- **改造品の配布**: 禁止
- **ゲーム等への組み込み配布**: OK（本パッケージ単体の再配布ではない場合）
- **保証・責任**: なし（現状有姿・自己責任）
