# 作業報告書: 多言語対応（ローカライズ）基盤構築・ファウスト演出調整・セーブデータ統計機能拡張

**対応日**: 2026-08-19  
**ブランチ**: `feature/localization01`  

---

## 目的

1. **多言語対応（ローカライズ）基盤の構築と主要UIへの適用**  
   - 日本語 / 英語の言語切り替えを統合管理する `ILanguage` インターフェースを新設。
   - `SettingUIManager` から一括で言語設定を波及可能にし、`GameUIManager`, `ItemPanelManager`, `EffectManager`, `VisitorSystem` にて英語・日本語の動的表示切り替えに対応。

2. **ファウスト登場・取り立て演出のカメラワークおよびUI調整**  
   - 借金取り立て演出時（特にファウスト登場場面）のカメラアングル・カメラワークおよび借用書UI（`DebtCanvas`）の表示最適化。

3. **セーブデータにおけるゲーム統計情報の拡張**  
   - プレイ結果（獲得金額、入手アイテム数、使用アイテム数など）を保存・追跡できるように `RoguelikeSaveData` および関連マネージャー（`RoguelikeSaveManager`, `MoneyManager`, `ItemPanelManager`）を拡張。

---

## 変更内容

### 1. 多言語対応（ローカライズ）基盤の構築
- **`ILanguage` インターフェース作成**:
  - `Assets/Scripts/UI/Interface/ILanguage.cs` を新規作成し、`SettingLanguage(Language language)` メソッドを定義。
- **一括言語切り替えロジック**:
  - `SettingUIManager.cs` にて、シーン内の `ILanguage` を実装するすべてのコンポーネントを自動取得し、言語設定を一貫して更新する処理を実装。
- **UI表示テキストのローカライズ対応**:
  - `GameUIManager.cs`: ターン数、次回取り立て、未洗浄コイン所持数、借用書（残金、次回請求額など）のテキスト表示を英語/日本語で切り替え。
  - `ItemData.cs` / `ItemInstance.cs` / `ItemPanelManager.cs`: アイテム説明文に `description_en` フィールドを追加し、選択言語に応じた説明文を表示。
  - `EffectData.cs` / `EffectInstance.cs` / `EffectManager.cs`: エフェクト説明文に `description_en` フィールドを追加し、選択言語に応じた説明文を表示。
  - `VisitorSystem.cs`: 会話データ構造 `VisitorConversationContainer`（日本語 `lineJp`, 英語 `lineEn`）への対応を行い、訪問者・主人公の会話やテキストを英語/日本語で切り替えて表示。

### 2. ファウスト演出・カメラワーク・UI調整
- **取り立て演出の強化**:
  - `DebtCollectionManager.cs`: 最後のファウスト登場時のカメラ動作および演出トリガーの調整。
  - `DebtCanvas.prefab` / `ResultUIManager.cs`: 借用書アニメーション・ゲームオーバー表示のUI配置および処理更新。
- **シーン調整**:
  - `3D_Title_Sample.unity`, `Scene_Visitor.unity`, `MainScene.unity` などのオブジェクト・アセット参照調整。

### 3. セーブデータおよび統計トラッキング拡張
- **統計データの拡張**:
  - `RoguelikeSaveData.cs` に獲得金額・入手アイテム数・使用アイテム数などの記録フィールドを追加。
  - `MoneyManager.cs`, `ItemPanelManager.cs`, `RoguelikeSaveManager.cs` でデータの加算・集計・保存処理を連動。
- **タイマー管理**:
  - `InfomationManager.cs`: セーブデータ読み込み時等のプレイタイマー停止・再開フラグ (`_isStopTimer`) の正確な制御。

---

## 対象ファイル

### スクリプト
- [NEW] `Assets/Scripts/UI/Interface/ILanguage.cs`
- [MODIFY] `Assets/Scripts/UI/SettingUI/SettingUIManager.cs`
- [MODIFY] `Assets/Scripts/UI/GameUI/GameUIManager.cs`
- [MODIFY] `Assets/Scripts/UI/GameUI/ItemPanelManager.cs`
- [MODIFY] `Assets/Scripts/UI/GameUI/EffectManager.cs`
- [MODIFY] `Assets/Scripts/VisitorSystem/VisitorSystem.cs`
- [MODIFY] `Assets/Scripts/Item/ItemData.cs`
- [MODIFY] `Assets/Scripts/Item/ItemInstance.cs`
- [MODIFY] `Assets/Scripts/Effect/EffectData.cs`
- [MODIFY] `Assets/Scripts/Devil/DebtCollectionManager.cs`
- [MODIFY] `Assets/Scripts/SaveData/RoguelikeSaveData.cs`
- [MODIFY] `Assets/Scripts/SaveData/RoguelikeSaveManager.cs`
- [MODIFY] `Assets/Scripts/SaveData/InfomationManager.cs`
- [MODIFY] `Assets/Scripts/UI/ResultUI/ResultUIManager.cs`
- [MODIFY] `Assets/Scripts/MoneyManager.cs`

### アセット・Prefab・データ
- [MODIFY] `Assets/Resources/Prefab/GameUI.prefab`
- [MODIFY] `Assets/Resources/Prefab/SettingCanvas.prefab`
- [MODIFY] `Assets/Resources/Prefab/DebtCanvas.prefab`
- [MODIFY] `Assets/Resources/ItemData/Devil'sCandy.asset`
- [MODIFY] `Assets/Resources/ItemData/TeddybBear.asset`
- [MODIFY] `Assets/Resources/ItemData/VintagePinball.asset`

### シーンファイル
- [MODIFY] `Assets/Scenes/3D_Title_Sample.unity`
- [MODIFY] `Assets/Scenes/Additive/Scene_Environment.unity`
- [MODIFY] `Assets/Scenes/Additive/Scene_Visitor.unity`
- [MODIFY] `Assets/Scenes/MainScene.unity`

---

## 確認内容・動作テスト

1. **言語切り替え機能**:
   - `SettingCanvas` の言語設定（JP/EN）変更時、`ILanguage` を介して `GameUI`, アイテム/エフェクト説明, 訪問者会話テキストが即座に英語/日本語へ切り替わることを確認。
2. **統計データセーブ/ロード確認**:
   - ゲーム進行に伴う獲得金額およびアイテムの取得・使用回数がセーブデータに保持・更新されることを確認。
3. **取り立て・カメラ演出動作**:
   - 借金取り立て・ファウスト登場時のカメラワークおよび借用書UI演出が正常に再生されることを確認。

---

## 今後の課題・確認事項

- **未コミット変更のコミット & プッシュ方針**:
  - 現在の作業ツリーに残っている変更ファイルを、必要に応じてカテゴリ別（ローカライズ、統計機能、UI・シーン調整）に安全に分割コミット・プッシュし、レビュー準備を整える。
- **テキストデータの完全英訳化**:
  - `ItemData` や `EffectData` の既存アセットおよび `VisitorConversations` 等の残りのテキストについて、英語翻訳データの入力・整備を引き続き進める。
