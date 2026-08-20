# 作業報告書: アイテム・エフェクト多言語対応（ローカライズ）のデータ構造刷新および関連UIの多言語化対応

**対応日**: 2026-08-20  
**ブランチ**: `feature/Localization03`  

---

## 目的

1. **アイテム・エフェクトデータの多言語対応構造の刷新**
   - 従来の個別フィールド（`description` と `description_en` など）から、`string[]` 配列による言語インデックス統一管理へのリファクタリング。
   - `ItemData`, `EffectData`, `ItemInstance`, `EffectInstance` 等のデータアクセサを更新し、日本語・英語をシンプルかつ柔軟に取得可能な構造へ移行。

2. **各種UI・システムのローカライズ（ILanguage）対応の全面拡張**
   - ゲーム内UI（`ItemPanelManager`, `EffectManager`, `GameUIManager`）に加えて、図鑑UI（`ItemEncyclopediaUI`）、UFOアイテム取得表示（`UFOItemPickupDisplay`）、訪問者選択ホバー表示（`VisitorSelectionHover`）、ローグライク管理（`RoguelikeManager`）へ `ILanguage` インターフェースを拡張・実装。

3. **`UnityEngine.Localization` を利用した動的テキスト埋め込み**
   - `ItemPanelManager` 内のカテゴリ名（Category）や所持数（Count）の表示テキストに `LocalizedString` を導入し、言語設定に連動した正確な動的文字列整形に対応。

4. **ローグライク特定アイテム（ブラックダイヤモンド等）の日英対応**
   - ブラックダイヤモンド等の進行状態に応じた名称変化 (`_diamondStageNames`) を日本語・英語の両言語構造 (`DiamondStageNames`) に拡張。

---

## 変更内容

### 1. アイテム・エフェクトデータ構造の多言語（配列化）対応
- **`ItemData.cs` / `EffectData.cs`**:
  - `itemName` / `effectName` および `description` を `string` から `string[]` (配列) へ変更。
  - `(int)Language.JP`, `(int)Language.EN` 等のインデックスによって言語ごとのテキストを安全に取得可能にリファクタリング。
- **`ItemInstance.cs` / `EffectInstance.cs`**:
  - `ItemName`, `ItemDescription`, `EffectName`, `EffectDescription` のプロパティ型を `string[]` へ対応。

### 2. UI・システムへの `ILanguage` 実装とテキスト表示の更新
- **`ItemPanelManager.cs`**:
  - `UnityEngine.Localization.LocalizedString` を使用し、`Category` や `Count` のローカライズ表示に対応。
  - アイテム詳細パネル非アクティブ時の描画クリア処理 (`ClearExplainPanel`) の追加。
  - アイテム使用処理時の内部検索キーの修正。
- **`EffectManager.cs`**:
  - 言語切り替えイベント `SettingLanguage` 発生時に `UpdateUI()` を自動実行するように強化。
- **`VisitorSelectionHover.cs`**:
  - `ILanguage` を実装。訪問者イベントでの選択肢ホバー時、表示中のアイテム/エフェクト名・説明文を選択中の言語で表示。
- **`ItemEncyclopediaUI.cs`**:
  - `ILanguage` を実装。タイプライター（図鑑UI）でのプレビュー表示を言語設定に追従。
- **`UFOItemPickupDisplay.cs`**:
  - `ILanguage` を実装。UFOキャッチャーで獲得したアイテムの名称表示を言語設定に追従。
- **`RoguelikeManager.cs`**:
  - `DiamondStageNames` 構造体を新設し、ブラックダイヤモンドの研磨段階名（呪われたダイヤモンド → ゴッドダイヤモンドなど）の日英対訳に対応。`ILanguage` を実装し言語変更時の表示に連動。

### 3. イベント・UI微調整
- **`VisitorSystem.cs`**:
  - 会話早送り入力時の連打・押下判定文字数条件を微調整 (`charNum >= 3`)。

---

## 対象ファイル

### スクリプト
- [MODIFY] `Assets/Scripts/Item/ItemData.cs`
- [MODIFY] `Assets/Scripts/Item/ItemInstance.cs`
- [MODIFY] `Assets/Scripts/Effect/EffectData.cs`
- [MODIFY] `Assets/Scripts/UI/GameUI/ItemPanelManager.cs`
- [MODIFY] `Assets/Scripts/UI/GameUI/EffectManager.cs`
- [MODIFY] `Assets/Scripts/UI/GameUI/GameUIManager.cs`
- [MODIFY] `Assets/Scripts/VisitorSystem/VisitorSelectionHover.cs`
- [MODIFY] `Assets/Scripts/VisitorSystem/VisitorSystem.cs`
- [MODIFY] `Assets/Scripts/Typewriter/ItemEncyclopediaUI.cs`
- [MODIFY] `Assets/Scripts/UFO/UFOItemPickupDisplay.cs`
- [MODIFY] `Assets/Scripts/Roguelike/RoguelikeManager.cs`
- [MODIFY] `Assets/Scripts/ItemSpawner.cs`
- [MODIFY] `Assets/Scripts/Devil/DebtCollectionManager.cs`

### アセット・データファイル
- [MODIFY] `Assets/Localization/GameUITable_ja.asset`
- [MODIFY] `Assets/Resources/ItemData/*.asset` (AnkhOfTheCleric, VintagePinball, blackdiamond, gold, silver 等)
- [MODIFY] `Assets/Resources/EffectData/*.asset` (Bell'sBounty, EffectData03~07, Gargantua's Plunder 等)
- [MODIFY] `Assets/models/UFO/ItemShow.renderTexture`
- [MODIFY] `Assets/models/UFO/television.renderTexture`

---

## 確認内容・動作テスト

1. **言語切替に伴うUIの即時更新テスト**:
   - `SettingCanvas` 等から言語（日本語/英語）を切り替えた際、アイテムパネル・エフェクトパネル・ホバーUI・図鑑UI・UFO獲得画面等の各テキストが即座に選択言語に更新されることを確認。
2. **アイテム・エフェクトデータのローカライズ動作**:
   - ScriptableObject アセット（`ItemData`, `EffectData`）の配列データ（JP/EN）が正しく読み込まれ、文字化けやインデックス外エラーを起こさず表示されることを確認。
3. **動的ローカライズテキスト (`LocalizedString`)**:
   - カテゴリ表示および個数表示（`Category: {0}`, `Count: {0}`）が Localization Table 経由で正しく整形・表示されることを確認。

---

## 今後の課題・確認事項

- **未翻訳アセットデータの追加入力**:
  - 新規・既存アイテム/エフェクトのアセットで未設定になっている英語テキスト（配列の2番目の要素）の継続的な登録・データ確認。
- **他UIシステムへの展開**:
  - ミニゲーム画面や借金取り立て演出等の追加UI領域に関しても、同様に `ILanguage` インターフェースによる統合を進める。
