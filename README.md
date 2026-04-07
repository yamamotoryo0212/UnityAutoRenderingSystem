# Unity Auto Rendering System

複数のレンダリングPCに対して、Unityシーンの自動レンダリングをリモートで指示・管理するシステムです。

## ダウンロード

[**AutoRenderingSystemSetup.exe** (最新版)](https://github.com/yamamotoryo0212/UnityAutoRenderingSystem/releases/latest/download/AutoRenderingSystemSetup.exe)

## 概要

```
┌─────────────────────┐        OSC         ┌─────────────────────┐
│   Service (操作PC)   │ ──────────────────> │   Agent (描画PC)     │
│   シーン選択・指示UI  │ <────────────────── │   Unity起動・録画管理 │
└─────────────────────┘     コールバック      └────────┬────────────┘
                                                      │ -executeMethod
                                                      v
                                             ┌─────────────────────┐
                                             │   Unity Editor       │
                                             │   Timeline再生+録画  │
                                             └─────────────────────┘
```

| コンポーネント | 役割 |
|---|---|
| **AutoRendering Service** | 操作PC用のWindows Forms UI。エンドポイント管理、シーン選択、レンダリング開始/停止 |
| **AutoRendering Agent** | 描画PC上で常駐するバックグラウンドプロセス。OSCコマンドを受信し、Unityを起動して録画を実行 |
| **AutoRendering Agent Config** | Agentの設定（IP/ポート/Unityプロジェクトパス）をGUIで編集するツール |
| **AutoRendering (Unity)** | Unityプロジェクト内のスクリプト群。RecorderClip生成・Timeline再生・録画完了処理 |

## システムフロー

1. **Agent** が描画PCで起動し、設定されたポートでOSC受信を開始
2. **Service** がAgentに `/scenes/list` を送信し、利用可能なシーン一覧を取得
3. ユーザーがServiceのUIでシーンにチェックを入れ「Start Rendering」をクリック
4. Service が `/render/start` をAgentに送信（選択シーン名付き）
5. Agent が Unity Editor をバッチモードで起動（`-executeMethod` 経由）
6. Unity が対象シーンを開き、PlayMode に入る
7. `AutoRendering` コンポーネントが `RenderJobSettings` に基づき RecorderClip を生成・再生
8. Timeline 再生完了後、Recorder トラックをクリーンアップし Unity を終了
9. Agent が Take 番号をインクリメントし、次のシーンへ
10. 全シーン完了後、Agent が `/render/finished` を Service に返し、ボタンが Start に戻る

## インストール

### インストーラを使う場合

`AutoRenderingSystemSetup.exe` を実行し、インストールタイプを選択します。

| タイプ | 内容 | 用途 |
|---|---|---|
| **Full** | Agent + Agent Config + Service | 1台で全て使う場合 |
| **Agent only** | Agent + Agent Config | レンダリングPC |
| **Service only** | Service | 操作PC |
| **Custom** | 個別選択 | 必要なものだけ |

#### オプション
- **デスクトップショートカット作成**
- **Windows起動時にAgentを自動起動**

#### アンインストール
- **スタートメニュー** > 「Auto Rendering System」>「Uninstall」
- **Windowsの設定** > 「アプリ」>「インストールされているアプリ」> 「Auto Rendering System」

### 手動ビルドする場合

```bash
# Agent
dotnet publish AutoRenderingAgent -c Release -r win-x64 --self-contained -o publish/Agent

# Service
dotnet publish AutoRenderingService -c Release -r win-x64 --self-contained -o publish/Service

# Agent Config
dotnet publish AutoRenderingAgentConfig -c Release -r win-x64 --self-contained -o publish/AgentConfig
```

### Unityパッケージ（UPM）

Unity の Package Manager から Git URL でインストールできます。

1. Unity で **Window > Package Manager** を開く
2. 左上の **+** > **Add package from git URL...** を選択
3. 以下のURLを入力:

```
https://github.com/yamamotoryo0212/UnityAutoRenderingSystem.git?path=AutoRendering/Assets/AutoRendering
```

または `Packages/manifest.json` に直接追加:

```json
{
  "dependencies": {
    "com.yamamotoryo.autorendering": "https://github.com/yamamotoryo0212/UnityAutoRenderingSystem.git?path=AutoRendering/Assets/AutoRendering"
  }
}
```

## セットアップ

### 1. 描画PC（Agent側）

1. インストーラで **Agent only** を選択してインストール
2. **Agent Config** を開き、以下を設定して Save

| 項目 | 説明 | 例 |
|---|---|---|
| IP Address | Agentのリッスンアドレス | `0.0.0.0`（全インターフェース） |
| Port | リッスンポート | `9000` |
| Unity Project Path | Unityプロジェクトのルート（Assetsの親） | `D:\MyProject` |
| Take | 録画テイク番号（レンダリング毎に自動加算） | `1` |

3. Agent を起動（バックグラウンドで常駐）

### 2. 操作PC（Service側）

1. インストーラで **Service only** を選択してインストール
2. **AutoRendering Service** を起動
3. 「Add」で描画PCのIPとポートを登録
4. 「Refresh」でAgentからシーン一覧を取得
5. レンダリングしたいシーンにチェック

### 3. Unityプロジェクト（描画PC）

1. `AutoRendering/Assets/AutoRendering` フォルダをUnityプロジェクトにコピー
2. **必須パッケージ**（Package Manager でインストール）:
   - `com.unity.recorder` (4.0.3+)
   - `com.unity.timeline` (1.7.7+)
3. レンダリング対象シーンに以下をセットアップ:
   - `PlayableDirector` を持つ GameObject に `AutoRendering` コンポーネントを追加
   - `RenderJobSettings` ScriptableObject を作成（Create > Auto Rendering > Render Job Settings）
   - Recorder の種類（Movie / Image Sequence / Animation / Audio）と出力設定を構成
   - `AutoRendering` コンポーネントに `RenderJobSettings` と `PlayableDirector` を割り当て

## 使い方

### レンダリング開始

1. Service でシーンにチェックを入れる
2. **▶ Start Rendering** をクリック
3. ボタンが赤い **⏹ Recording** に変わり、ログにシーン名が表示される
4. 全シーン完了後、自動的に **▶ Start Rendering** に戻る

### レンダリング中断

以下のいずれかの方法で中断できます：

- **Service**: 赤い「⏹ Recording」ボタンをクリック
- **Agent Config**: 「Stop Rendering」ボタンをクリック（描画PC上で直接）

### エンドポイント管理

| 操作 | 方法 |
|---|---|
| 追加 | 「Add」ボタン |
| 編集 | エンドポイントノードをダブルクリック |
| 削除 | 選択して「Remove」ボタン |
| シーン更新 | 「Refresh」ボタン |

## 設定ファイル

全ての設定は `%LOCALAPPDATA%` 以下に保存され、PCを問わず安全にアクセスできます。

| ファイル | パス |
|---|---|
| Agent設定 | `%LOCALAPPDATA%\AutoRenderingAgent\agent-config.json` |
| Agentログ | `%LOCALAPPDATA%\AutoRenderingAgent\logs\YYYY-MM-DD.log` |
| Service設定 | `%LOCALAPPDATA%\AutoRenderingService\config.json` |

## OSCプロトコル

### Service → Agent

| アドレス | 引数 | 説明 |
|---|---|---|
| `/scenes/list` | なし | シーン一覧を要求 |
| `/render/start` | シーン名 (string...) | レンダリング開始 |
| `/render/stop` | なし | 現在のレンダリングを中断 |

### Agent → Service

| アドレス | 引数 | 説明 |
|---|---|---|
| `/scenes/result` | シーン名 (string...) | シーン一覧の応答 |
| `/render/started` | シーン名 (string) | シーンのレンダリング開始通知 |
| `/render/stopped` | シーン名 (string) | シーンのレンダリング失敗/中断通知 |
| `/render/finished` | なし | 全シーンのレンダリング完了通知 |

## 技術スタック

- **.NET 8.0** (Windows Forms)
- **CoreOSC 1.0.0** — OSC通信
- **Unity Recorder 4.0.3+** — 録画
- **Unity Timeline 1.7.7+** — Timeline再生
- **Inno Setup 6** — インストーラ作成
