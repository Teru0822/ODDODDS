# 作業報告書: 訪問者（Visitor）システムの実装・アセット構造の刷新・Git LFS導入

**対応日**: 2026-07-27  
**ブランチ**: `feature/visitor_system`  

---

## 目的

1. **Git LFS の初期化とローカル環境の有効化**  
   - 大容量アセット（画像、音声、Prefab、モデルデータ等）のバージョン管理を最適化するため、リポジトリに導入された Git LFS を `git lfs install` によりローカル環境で有効化。

2. **訪問者（Visitor）システムおよびインターホン連動機能の構築**  
   - プレイヤーの拠点・店等に現れる訪問者（Visitor）との会話イベントや行動分岐、インターホンによる対応ロジックを実装。
   - 会話データの追加およびシナリオ・立ち絵・効果音と連動したインタラクティブなイベントフローを構築。

3. **アイテム・エフェクトデータ構造の刷新とアセット更新**  
   - 開発初期のサンプルアセット（`ItemSample00`〜`04`、`EffectData00`）を整理・削除。
   - 新しいアイテムアセット（`AnkhOfTheCleric`, `Devil'sCandy`, `Jin'spendant`, `TeddybBear`, `VintagePinball`等）およびエフェクトアセット（`Bell'sBounty`等）を追加・設定。

4. **UI・マネージャー・セーブデータ層の機能拡張**  
   - `ItemPanelManager` や `EffectManager`, `GameUIManager`, `MoneyManager` 等の表示・データ更新ロジックを最適化。
   - `RoguelikeSaveData` を更新し、各種新データやアイテム取得・消費フラグの保持に対応。

---

## 変更内容

### 1. Git LFS の導入・環境構築
- リポジトリに対して `git lfs install` を実行。
- フックの更新および Git LFS の初期化を完了し、大容量ファイルの追跡準備を整えました。

### 2. 訪問者（Visitor）システムの実装
- **ロジック & コントローラー**:
  - `Assets/Scripts/VisitorSystem/` 配下に訪問者の状態管理・イベント判定を行うモジュール群を追加。
  - `IntercomController.cs` を更新し、インターホン操作時の演出および訪問者イベント呼び出しを同期。
- **リソース統合**:
  - 訪問者用データアセット (`VisitorData`) や会話用シナリオデータ (`DevilConversations_JP.json`) を更新・追加。
  - 立ち絵素材 (`fantasy_maou_devil.png`) および効果音 (`引き戸を開ける1.mp3` 等) を追加。

### 3. アイテム & エフェクトデータの整理・新規追加
- **旧サンプルアセットの削除**:
  - `ItemSample00.asset`〜`04.asset` および `EffectData00.asset`（およびそれぞれの `.meta`）を削除し、古いテストコード依存を排除。
- **新データの追加・設計拡張**:
  - `ItemData.cs`, `ItemDataBase.cs`, `EffectData.cs`, `EffectDataBase.cs` のプロパティや処理を更新。
  - クレリックのアンク (`AnkhOfTheCleric`)、悪魔のキャンディ (`Devil'sCandy`)、ジンのペンダント (`Jin'spendant`)、テディベア (`TeddybBear`)、ヴィンテージピンボール (`VintagePinball`)、ベルの恵み (`Bell'sBounty`) などのアセットを作成・配置。

### 4. UIマネージャーおよびセーブデータの改修
- **UI表示・管理ロジック**:
  - `ItemPanelManager.cs` および `EffectManager.cs` でのアイテム・エフェクト表示更新や切り替え処理を拡充。
  - `GameUIManager.cs` を修正し、訪問者UIや各種ポップアップとのイベント連携を円滑化。
- **資金・データ保存**:
  - `MoneyManager.cs` の変更に伴う資金操作の安全性を確保。
  - `RoguelikeSaveData.cs` に新しいデータ保持用フィールド・メソッドを追加。

### 5. フォント・シーンおよびプロジェクト設定
- **TextMesh Pro (SDF) フォント**:
  - `ZenOldMincho-Bold SDF.asset` および `DSEG7Classic-Regular SDF.asset` の文字設定・マテリアル参照を調整。
- **シーン & プロジェクト設定**:
  - `Scene_Visitor.unity`, `GameScene.unity`, `MainScene.unity` のレイアウト・オブジェクト設定を更新。
  - `DynamicsManager.asset` の物理パラメータ・マテリアル参照を更新。

---

## 対象ファイル

### スクリプト
- [NEW] `Assets/Scripts/VisitorSystem/` (訪問者システムのロジック・コントローラー群)
- [MODIFY] `Assets/App/Intercom/Scripts/IntercomController.cs` (インターホン制御スクリプト)
- [MODIFY] `Assets/Scripts/Effect/EffectData.cs` / `EffectDataBase.cs`
- [MODIFY] `Assets/Scripts/Item/ItemData.cs` / `ItemDataBase.cs`
- [MODIFY] `Assets/Scripts/MoneyManager.cs`
- [MODIFY] `Assets/Scripts/SaveData/RoguelikeSaveData.cs`
- [MODIFY] `Assets/Scripts/UI/GameUI/EffectManager.cs`
- [MODIFY] `Assets/Scripts/UI/GameUI/GameUIManager.cs`
- [MODIFY] `Assets/Scripts/UI/GameUI/ItemPanelManager.cs`

### アセット・データ・リソース
- [DELETE] `Assets/Resources/EffectData/EffectData00.asset`
- [DELETE] `Assets/Resources/ItemData/ItemSample00.asset` 〜 `04.asset`
- [NEW] `Assets/Resources/EffectData/Bell'sBounty.asset`
- [NEW] `Assets/Resources/ItemData/AnkhOfTheCleric.asset`
- [NEW] `Assets/Resources/ItemData/Devil'sCandy.asset`
- [NEW] `Assets/Resources/ItemData/Jin'spendant.asset`
- [NEW] `Assets/Resources/ItemData/TeddybBear.asset`
- [NEW] `Assets/Resources/ItemData/VintagePinball.asset`
- [NEW] `Assets/Resources/Prefab/DebtCanvas.prefab`
- [NEW] `Assets/Resources/Prefab/Sphere.prefab`
- [NEW] `Assets/Resources/VisitorData/`
- [NEW] `Assets/Resources/Sound/SE/CharacterVoice/`
- [NEW] `Assets/Resources/Sound/SE/引き戸を開ける1.mp3`
- [NEW] `Assets/Resources/illust/GameUI_tmp/fantasy_maou_devil.png`
- [MODIFY] `Assets/Resources/Conversations/DevilConversations_JP.json`
- [MODIFY] `Assets/Fonts/Zen_Old_Mincho/ZenOldMincho-Bold SDF.asset`
- [MODIFY] `Assets/Fonts/fonts-DSEG_v046/DSEG7-Classic/DSEG7Classic-Regular SDF.asset`

### シーン・プロジェクト設定
- [MODIFY] `Assets/Scenes/Additive/Scene_Visitor.unity`
- [MODIFY] `Assets/Scenes/GameScene.unity`
- [MODIFY] `Assets/Scenes/MainScene.unity`
- [MODIFY] `ProjectSettings/DynamicsManager.asset`

---

## 確認内容

1. **Git LFS の正常稼働**:
   - `git lfs install` の実行結果として `Git LFS initialized.` を確認。
2. **訪問者イベント動作確認**:
   - インターホン操作からの訪問者呼び出し、立ち絵・音声（引き戸SE等）の連動が正しく行われること。
3. **アセット参照の健全性**:
   - 旧サンプルデータ削除後も、新規追加アイテム・エフェクトがインスペクターおよびゲーム画面でエラーなく正常にロード・表示されること。

---

## 今後の課題・TODO

- **Git ブランチのコミット・マージ**:
  - Unity 上で編集中のシーンおよび Prefab を保存（Ctrl+S）した上で、変更ファイルを機能カテゴリ単位で安全に分割コミット・プッシュし、`main` ブランチへ `--no-ff` マージを行う。
- **訪問者シナリオの拡充と分岐の調整**:
  - 会話シナリオ JSON の拡張および複数パターンの訪問者イベント追加。
