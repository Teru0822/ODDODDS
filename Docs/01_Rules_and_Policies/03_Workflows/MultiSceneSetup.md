# Multi-Scene（シーン分割＋加法ロード）導入ガイド

MainScene.unity が巨大な単一ファイルで、複数人が触ると毎回マージ対象になる問題への根本対策。
役割ごとに**別シーンファイル**へ分割し、編集時・実行時に **Additive（加法）ロード**で重ねる。
各人が別ファイルを編集できるため、衝突が激減する。

関連: [SceneMergeConflicts.md](SceneMergeConflicts.md) / [GitWorkflow.md](GitWorkflow.md)

---

## 全体像

```
MainScene.unity            ← ルート（常駐）シーン。常に1つ。
 │  ・カメラ群 / EventSystem / 管理オブジェクト(RoundManager 等)
 │  ・MultiSceneLoader（起動時に下のサブシーンを加法ロード）
 │  ・ライティング設定はこの「アクティブシーン」が基準
 │
 ├─ Assets/Scenes/Additive/
 │   ├─ Scene_Environment.unity   ← WorldObjects(壁/床/天井/什器), Particle System, ライト
 │   ├─ Scene_Pinball.unity       ← PINBALL 一式
 │   ├─ Scene_UFO.unity           ← UFOキャッチャー / Player 周辺
 │   └─ Scene_UI.unity            ← Canvas / GameUI / RoundText
```

- **実装済み（コード）**: [MultiSceneLoader.cs](../../../../Assets/Scripts/SceneManagement/MultiSceneLoader.cs) … 起動時にサブシーンを加法ロードしアクティブ化。
- **実装済み（エディタ補助）**: [MultiSceneEditorTools.cs](../../../../Assets/Scripts/SceneManagement/Editor/MultiSceneEditorTools.cs) … メニュー **Tools > Multi-Scene** で「ルート＋全サブシーン」を一括加法オープン。
- **実装済み（エディタ補助）**: [MultiSceneSetupWindow.cs](../../../../Assets/Scripts/SceneManagement/Editor/MultiSceneSetupWindow.cs) … メニュー **Tools > Multi-Scene > サブシーン構成を同期...** で、Build Settings と `MultiSceneLoader.subScenes` への登録状況を一覧・一括適用（STEP 4・STEP 5 の登録忘れ防止）。
- **手動（Unity 上で実施が必須）**: オブジェクトをサブシーンへ移す作業。YAML 手編集は厳禁（破損する）。

---

## ⚠️ 最重要：シーンをまたぐ参照は壊れる

Unity は **保存済みシーンに「別シーンのオブジェクトへの直接参照」を保持できない**。
Inspector で別シーンのオブジェクトをドラッグ参照していると、**分割・保存した瞬間にその参照が `None`（null）になる**。

例：`GameUI` の表示スクリプトが `RoundManager` や `ExchangeStation` を Inspector で参照 → これらを別シーンに分けると参照が切れる。

### 対策（いずれか）
1. **相互参照するオブジェクトは同じサブシーンにまとめる**（最も簡単）。
2. **実行時に解決する**：`FindAnyObjectByType<T>()` / シングルトン（`Instance`）/ イベント（UnityEvent・ScriptableObject イベント）に置き換える。
   - 例：`RoundManager` は既に `FindAnyObjectByType` でシーン内を検索しており、分割に強い。
3. どうしても直接参照が必要なら、起動後にコードで再バインドする（`RebindOnStart` 的な仕組み）。

> このプロジェクトには既にシーンまたぎ参照が多数あるはずなので、**分割後は必ず Console の `Missing`/`NullReference` を確認し、潰すこと。**

---

## 推奨：いきなり全分割せず「1枚ずつ」検証しながら進める

大規模な一括分割は事故りやすい。**まず1つ（例: Scene_UI か Scene_Environment）だけ切り出して**、
起動・参照・ライティングが正常か確認してから次へ進むのが安全。

---

## 手順（Unity エディタで手動）

### STEP 0. 前提確認
- **Edit > Project Settings > Editor > Asset Serialization = Force Text**（必須。テキストでないと差分管理不可）。
- 作業前に必ず現在のシーンを保存（Ctrl+S）。

### STEP 1. サブシーン用フォルダとシーンを作る
1. `Assets/Scenes/Additive/` フォルダを作成。
2. その中に空シーンを作成（Project で右クリック > Create > Scene）。
   例: `Scene_Environment` / `Scene_Pinball` / `Scene_UFO` / `Scene_UI`。

### STEP 2. オブジェクトをサブシーンへ移す（加法オープンしてドラッグ）
1. MainScene を開いた状態で、対象サブシーンを **Hierarchy にドラッグ or 右クリック > Open Scene Additive** で加法表示。
2. MainScene の**ルートオブジェクトを、Hierarchy 上でサブシーン側へドラッグ**して移す。
   - 例: `WorldObjects` `Particle System` → Scene_Environment へ。
   - 例: `PINBALL` → Scene_Pinball へ。
   - 例: `Canvas` `GameUI` `RoundText` → Scene_UI へ。
3. **両方のシーンを保存**（Ctrl+S。複数シーンが開いていると各シーンに `*` が付く）。

> Unity が fileID と参照を正しく付け替えてくれるのは「Hierarchy 上のドラッグ移動」だから。テキストで切り貼りしないこと。

### STEP 3. ルートシーンに残すもの
MainScene には、常に必要な以下を残す：
- カメラ群（`Camera` / `DebugCamera` / `EntranceCamera`）
- `EventSystem`（UI 入力。プロジェクト内で1つだけ）
- 管理系（`RoundManager` / `GameObject` 等のマネージャ）
- **`MultiSceneLoader` を付けた空オブジェクト**（次の STEP で設定）

### STEP 4-5. Build Settings と MultiSceneLoader への登録（推奨: 同期ウィンドウ）

サブシーンを新規追加したら、**Build Settings** と **ルートシーンの `MultiSceneLoader.subScenes`** の
2箇所に登録が必要。どちらか片方を忘れると「ビルドで落ちる」「起動時にロードされない」が起きる。

**メニュー Tools > Multi-Scene > サブシーン構成を同期...** を使うと、`Assets/Scenes/Additive` 内の
全シーンについて両者の登録状況が一覧表示され、チェックボックスで一括適用できる。

- ルートシーン（MainScene）を開いた状態で実行すること。
- Build Settings に未登録の新規サブシーンは **NEW** と表示され、既定で両方オンになっている。
- 既存シーンの設定は現状維持されるため、意図的にオンデマンドにしてあるシーンが勝手に起動ロードされることはない。
- 「起動時にロード」をオンにすると Build Settings への登録も自動で行われる。
- 適用後は**ルートシーンの保存が必要**（ウィンドウが保存を促す）。

<details>
<summary>手動で行う場合</summary>

1. **File > Build Profiles > Scene List**（旧 Build Settings）に、MainScene と全サブシーンを追加。加法ロードするにはサブシーンも登録が必須。
2. MainScene に空オブジェクト（例: `[MultiSceneLoader]`）を作り `MultiSceneLoader` をアタッチ。
3. `Sub Scenes` に各サブシーン名（`Scene_Environment` `Scene_UI` 等）を追加する。
   **追加すれば既定でロードされる**（`Disable On Start` は通常オフのまま）。デバッグで一時的に外したいシーンだけ `Disable On Start` をオンにする。
   - ⚠️ ロードしたいシーンを**リストに入れ忘れない**こと。リストに無いシーンはロードされない。
4. `Active Scene After Load` に、ライティング基準にしたいシーン名を入れる（通常は `MainScene` か `Scene_Environment`）。

</details>

### STEP 6. 動作確認
- **Play して**：全サブシーンがロードされ、ゲームが従来通り動くか。
- **Console を確認**：`NullReference` / `Missing` が出ていないか（＝シーンまたぎ参照の切れ）。出たら §「最重要」の対策で再バインド。
- ライティング/スカイボックスが正しいか（アクティブシーンの設定が効く）。

### STEP 7. 日々の編集
- メニュー **Tools > Multi-Scene > ルート + 全サブシーンを開く** で一括加法オープン。
- 各人は**自分の担当サブシーンだけ**を編集・保存 → git では別ファイルなので衝突しない。

---

## 担当分けの例（チーム運用）

| サブシーン | 主な中身 | 想定担当 |
|---|---|---|
| Scene_Environment | 壁/床/天井/什器/ライト | 環境・レベルデザイン |
| Scene_Pinball | ピンボール台一式 | ピンボール担当 |
| Scene_UFO | UFOキャッチャー / Player 周辺 | UFO担当 |
| Scene_UI | Canvas / GameUI / HUD | UI担当 |

MainScene（ルート）は**触る人を限定**し、変更時は周知する（ここだけは依然として共有ファイル）。

---

## トラブル時
- 参照が切れた → §「最重要」を参照。`FindAnyObjectByType`/シングルトン化で解決。
- シーンが二重ロードされる → `MultiSceneLoader` は既ロードを検知してスキップするが、Play 前にエディタで開いていたサブシーンはそのまま使われる（正常）。
- ライティングが暗い/おかしい → アクティブシーンが意図通りか確認（`Active Scene After Load`）。
- 分割作業で壊した → 慌てず、`Temp/__Backupscenes/` の自動バックアップを退避して復旧（[GitWorkflow.md](GitWorkflow.md) §6.5.2）。
