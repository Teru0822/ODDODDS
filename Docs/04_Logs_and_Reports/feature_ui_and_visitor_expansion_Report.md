# 作業報告書: Get/Lost UIアニメーションの実装・MoneyData独立化・訪問者トレード機能の拡張

**対応日**: 2026-07-29  
**ブランチ**: `feature/feature/visitor_system2`  

---

## 目的

1. **アイテム・お金・エフェクト入手/喪失通知アニメーション（Get/Lost UI）の実装と通知キュー統合**  
   - ゲーム内でアイテム・お金・エフェクトを獲得または失った際に、プレイヤーに分かりやすく視覚的フィードバックを行うポップアップ通知UI（`_getOrLostPanel`）のアニメーションおよびキュー管理ロジックを導入。

2. **`MoneyData` クラスの独立スクリプト化**  
   - 従来 `ItemData.cs` 内に同居していた `MoneyData` クラスを取り出し、専用の `MoneyData.cs` スクリプトとして分離・整理。

3. **訪問者（Visitor）システムにおける要求・報酬データの多角化および判定拡張**  
   - 訪問者の要求（Requests）および報酬（Rewards）を、単一のアセット指定から、複数種類・個数（`num`）を保持・管理できる `Request` / `RequestElement` および `Reward` / `RewardElement` 構造へ再設計。
   - 実行時の安全なデータ操作のためにディープクローン機能（`Clone()`）を導入し、アイテムの複数所持数チェックや複数報酬選択分岐に対応。

4. **訪問者選択肢UIのホバー挙動コンポーネント追加**  
   - 訪問者イベント等の選択肢表示時に、マウスホバー検知およびアニメーション選択制御を行う `VisitorSelectionHover.cs` を新規作成。

5. **不要アセットの整理・削除および新規データアセットの追加**  
   - 使用しなくなった旧エフェクトデータ（`EffectData01`, `EffectData02`）を削除し、新規エフェクトデータ（`Gargantua's Plunder`, `Gargantua's Trial`）を追加・設定。

---

## 変更内容

### 1. 入手/喪失通知演出（Get / Lost UI）と通知キューシステム
- **`GameUIManager.cs`**:
  - `DOTween` を用いて、獲得（`Get` / SkyBlue）および喪失（`Lost` / Red）の通知パネルアニメーション（`GetOrLostAnimation`）を実装。
  - キャンバスグループのフェードイン/フェードアウト、およびアイコンの順次スケールアップ（`DOScale`）アニメーション制御を追加。
  - アイテム・エフェクト・お金（`MoneyData`）のデータを安全にキューイングして順次再生する `AddPopupQueue` ロジックを拡張。
- **`ItemPanelManager.cs` / `EffectManager.cs`**:
  - アイテムやエフェクトの追加・削除時、および所持数変動時に自動で `GameUIManager.AddPopupQueue` が呼び出されるように連携処理を追記。

### 2. `MoneyData` の独立ファイル化
- **`MoneyData.cs` [新規追加]**:
  - `Assets/Scripts/Item/MoneyData.cs` を新たに作成し、`MoneyData` クラスを独立して定義。
- **`ItemData.cs` [更新]**:
  - `ItemData.cs` 内の `MoneyData` 定義を削除し、コードの密結合を解消。

### 3. 訪問者システムの拡張とデータクローン構造の導入
- **`VisitorData.cs`**:
  - `RequestElement`, `Request`, `RewardElement`, `Reward` クラスを追加。各要素に `ScriptableObject content` と `int num` を保持可能にし、データ安全性のための `Clone()` メソッドを実装。
- **`VisitorSystem.cs`**:
  - 複数要素の要求提出および報酬獲得処理に対応。
  - `Faust` や `Gargantua` 等の訪問者における選択分岐に応じた報酬削除・付与ロジックを更新。
  - 要求クリア判定 `isClearRequest` でアイテム所持数（`num`）のチェックに対応。

### 4. 訪問者選択肢UIホバー制御
- **`VisitorSelectionHover.cs` [新規追加]**:
  - `Assets/Scripts/VisitorSystem/VisitorSelectionHover.cs` を追加。選択肢UI上のマウスホバー状態を判定し、インタラクティブなUI挙動を制御。

### 5. アセット・シーンの調整
- **旧アセット削除**: `EffectData01.asset`, `EffectData02.asset`（および `.meta`）を削除。
- **新規アセット**: `Gargantua's Plunder.asset`, `Gargantua's Trial.asset`, `MoneyData/` ディレクトリを追加。
- **データ更新**: `Bell.asset`, `Gargantua.asset`, `GameUI.prefab`, `Scene_Visitor.unity`, `MainScene.unity` を更新。

---

## 対象ファイル

### スクリプト
- [NEW] `Assets/Scripts/Item/MoneyData.cs`
- [NEW] `Assets/Scripts/VisitorSystem/VisitorSelectionHover.cs`
- [MODIFY] `Assets/Scripts/Item/ItemData.cs`
- [MODIFY] `Assets/Scripts/VisitorSystem/VisitorData.cs`
- [MODIFY] `Assets/Scripts/VisitorSystem/VisitorSystem.cs`
- [MODIFY] `Assets/Scripts/UI/GameUI/GameUIManager.cs`
- [MODIFY] `Assets/Scripts/UI/GameUI/EffectManager.cs`
- [MODIFY] `Assets/Scripts/UI/GameUI/ItemPanelManager.cs`

### アセット・リソース
- [DELETE] `Assets/Resources/EffectData/EffectData01.asset` (.meta 含む)
- [DELETE] `Assets/Resources/EffectData/EffectData02.asset` (.meta 含む)
- [NEW] `Assets/Resources/EffectData/Gargantua's Plunder.asset` (.meta 含む)
- [NEW] `Assets/Resources/EffectData/Gargantua's Trial.asset` (.meta 含む)
- [NEW] `Assets/Resources/MoneyData/` (.meta 含む)
- [MODIFY] `Assets/Resources/VisitorData/Bell.asset`
- [MODIFY] `Assets/Resources/VisitorData/Gargantua.asset`
- [MODIFY] `Assets/Resources/Prefab/GameUI.prefab`

### シーン・設定
- [MODIFY] `Assets/Scenes/Additive/Scene_Visitor.unity`
- [MODIFY] `Assets/Scenes/MainScene.unity`
- [MODIFY] `ProjectSettings/EditorSettings.asset`

---

## 確認内容

1. **ポップアップ演出のアニメーション動作**:
   - `GameUIManager` の `GetOrLostAnimation` コルーチンにより、取得/失却アイテムが正常にスケール・フェード演出されることを確認。
2. **`MoneyData` 独立化によるコンパイルの健全性**:
   - 分離後もプロジェクト全体のスクリプト参照が正常に維持されていることを確認。
3. **訪問者トレードおよびアイテム数判定**:
   - `VisitorSystem` での要求アイテム判定（個数指定含む）および報酬獲得・演出連携が正常に機能することを確認。

---

## 今後の課題・TODO

- **コミット・マージ作業**:
  - Unity上で編集中のシーン・プレハブを保存し、変更内容をルール（`GitWorkflow.md`）に則って安全にコミット・マージする。
