# 作業報告書: DebugTool資産設定拡張・SettingUIシングルトン化・UI操作制御およびセーブデータの安定化

**対応日**: 2026-08-05  
**ブランチ**: `feature/create_and_fix_many`  

---

## 目的

1. **`DebugTool`（エディター拡張）における資産手動設定機能の拡張**  
   - デバッグ・テスト作業の効率化のため、`DebugTool.cs` において所持金および各種コイン（金・銀・銅コイン、ブラックダイヤモンド、徳ポイント等）を手動で入力・設定し、セーブデータを直接生成・更新できる機能を追加。
   - 不正な値の入力を防ぐため、`IntField` による負の値の入力バリデーションおよび `RoguelikeSaveData` との整合性を確保。

2. **設定画面 UI (`SettingUIManager`) の構造刷新とシングルトン化**  
   - 設定マネージャー (`SettingUIManager`) をシングルトンパターン化および `DontDestroyOnLoad` に対応させ、シーン遷移後も設定状態を保持可能に改善。
   - タイトル画面からの設定メニュー呼び出しに対応するため、`TitleSettingButton.cs` を新規追加。
   - 設定のリセット確認やタイトル画面への戻り処理を行うポップアップダイアログ (`CheckActionView` コルーチン) を DOTween アニメーション付きで実装。

3. **UI操作時のインタラクション競合防止 (`MouseHoverOutline` 連動)**  
   - `GameUIManager` や `SettingUIManager` などの各種UIメニューを開いている最中に、背景の3Dオブジェクトのアウトライン表示や視点操作 (`FirstPersonController`) が誤動作しないよう、`IsOpenUI` フラグによる排他制御を導入。

4. **セーブ・ロード機能 (`RoguelikeSaveManager`) のログ拡張とタイトル終了時安全対策**  
   - セーブおよびロード実行時、全フィールド（資産・フラグ・所持アイテム数など）の詳細ログを出力する機能を追加し、データのロード失敗や異常を早期検知可能に改善。
   - タイトルシーン（buildIndex == 0）でのアプリ終了時にはセーブ処理をスキップする安全ロジックを `SceneTransitionManager` に追記。

---

## 変更内容

### 1. デバッグツール (`DebugTool.cs`) の拡張
- **資産・コイン設定項目の追加**:
  - `RoguelikeSaveData` の更新に伴い、`money`, `unwashedMoney`, `bronzeCoin`, `silverCoin`, `goldCoin`, `blackDiamond`, `virtuePoints` を Inspector / エディターウィンドウから直接指定してセーブデータを生成・保存する UI フィールドを追加。
- **入力バリデーション**:
  - 負の数値が入力された場合に 自動的に `0` に補正する処理を実装。

### 2. 設定 UI システム (`SettingUIManager.cs` / `TitleSettingButton.cs`)
- **`SettingUIManager.cs`**:
  - `Instance` プロパティおよび `DontDestroyOnLoad` によるシングルトン構造を適用。
  - シーン読み込み時やボタンクリック時の開閉メソッド (`OpenSettingMenu`, `CloseSettingMenu`) を追加。
  - 設定メニュー開閉時に `FirstPersonController` の有効/無効切り替えおよびカーソルのロック状態切り替え処理を追加。
  - リセット処理 (`ResetSetting`) とダイアログ表示コルーチン (`CheckActionView`) を追加。
- **`TitleSettingButton.cs` [新規作成]**:
  - タイトル画面上の設定ボタンから `SettingUIManager.Instance.OpenSettingMenu()` を呼び出す専用スクリプトを作成。

### 3. マウスホバー・UI排他制御 (`MouseHoverOutline.cs` / `GameUIManager.cs`)
- **`MouseHoverOutline.cs`**:
  - `IsOpenUI` プロパティを追加し、`IsOpenUI == true` の間は Raycast 判定およびアウトライン更新処理をスキップするロジックを追加。
- **`GameUIManager.cs` / `SettingUIManager.cs`**:
  - メニュー表示・非表示の切り替え時にシーン内の `MouseHoverOutline` インスタンスを検索し、`IsOpenUI` フラグを連携更新。

### 4. セーブデータ管理・シーン遷移 (`RoguelikeSaveManager.cs` / `SceneTransitionManager.cs`)
- **`RoguelikeSaveManager.cs`**:
  - `Save()` および `Load()` 処理の完了時に、所持金・各種コイン・アンロック状態・所持リスト要素数を整形して出力するデバッグログ (`Debug.LogError` / `Debug.Log`) を追加。
- **`SceneTransitionManager.cs`**:
  - シーン遷移後のマルチシーンロード完了タイミングで `SettingUIManager.Instance.Init()` を実行する初期化処理を追記。
  - `OnApplicationQuit` 内で、タイトル画面からの終了時にはセーブを行わないチェック (`buildIndex != 0`) を追加。

### 5. その他の調整
- **`UFOItemGoal.cs`**: アイテム獲得時の過剰な手動 `Save()` 呼び出しを整理。
- **プレハブ・シーン調整**: `SettingCanvas.prefab`, `TitleSettingButton`, 各種シーン (`MainScene.unity`, `3D_Title_Sample.unity`, `Scene_UI.unity`) のコンポーネント配置・参照割り当てを更新。

---

## 対象ファイル

### スクリプト
- [NEW] `Assets/Scripts/Title/TitleSettingButton.cs` (.meta 含む)
- [MODIFY] `Assets/Scripts/Editor/DebugTool.cs`
- [MODIFY] `Assets/Scripts/Player/PlayerWallet.cs`
- [MODIFY] `Assets/Scripts/SaveData/RoguelikeSaveManager.cs`
- [MODIFY] `Assets/Scripts/System/SceneTransitionManager.cs`
- [MODIFY] `Assets/Scripts/Title/MouseHoverOutline.cs`
- [MODIFY] `Assets/Scripts/UFO/UFOItemGoal.cs`
- [MODIFY] `Assets/Scripts/UI/GameUI/GameUIManager.cs`
- [MODIFY] `Assets/Scripts/UI/SettingUI/SettingUIManager.cs`

### プレハブ・アセット・シーン
- [MODIFY] `Assets/Resources/Prefab/SettingCanvas.prefab`
- [MODIFY] `Assets/Resources/VisitorData/Bell.asset`
- [MODIFY] `Assets/Resources/VisitorData/Gargantua.asset`
- [MODIFY] `Assets/Scenes/3D_Title_Sample.unity`
- [MODIFY] `Assets/Scenes/Additive/Scene_Environment.unity`
- [MODIFY] `Assets/Scenes/Additive/Scene_UI.unity`
- [MODIFY] `Assets/Scenes/MainScene.unity`
- [MODIFY] `Assets/UI/Parchment_Damaged_Edge_v2.prefab`

---

## 確認内容

1. **DebugTool によるセーブデータ作成検証**:
   - エディター上から所持金・各種コインの値を設定してデータ作成を実行し、生成されたセーブファイルに正しく値が反映されていることを確認。
   - 負の数値を入力した際に 0 にクランプされることを確認。

2. **設定 UI（SettingUI）の遷移・制御検証**:
   - タイトル画面およびゲーム本編画面の両方から設定画面が問題なく開閉できることを確認。
   - 設定画面を開いている間、背景の3Dオブジェクトのアウトライン強調やプレイヤー視点移動が停止することを確認。

3. **セーブ・ロードログの正常出力確認**:
   - セーブ・ロード時にコンソールへ詳細な全データフィールドログが出力され、データの整合性が確認できることを検証。

---

## 今後の課題・TODO

- **Gitコミット・マージ作業**:
  - `GitWorkflow.md` に従い、Unityで未保存のシーン・プレハブ（`Scene_UI.unity`, `SettingCanvas.prefab` 等）をCtrl+Sで保存した上で、変更内容をコミットおよびリモートブランチへプッシュする。
- **PR作成と確認**:
  - ユーザーに報告を行い、確認およびマージの指示を待つ。
