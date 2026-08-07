# 作業報告書: ゲームシステム拡張・セーブロード完全対応・訪問者イベント・導入ツアー連動および全体機能改善

**対応日**: 2026-08-06  
**対象ブランチ**: `feature/create_function_and_fix_someting` （および前後の統合 `feature/create_and_fix_many` 一連の改修作業）  

---

## 概要

本作業では、ゲーム全体の主要システムの結合安定化、データ永続化（セーブ＆ロード）の拡充、訪問者システム（`VisitorSystem`）のイベント進行と演出の強化、導入ツアー（`IntroTourDirector`）のロード連動・UI制御、および各種ゲーム内ギミック（タイプライター・取り立て・所持金・ローグライクスキル等）の細部調整・不具合修正を実施しました。

---

## 項目別詳細作業内容

### 1. ターン・返済システムとセーブデータ永続化の拡張 (`MoneyManager.cs`, `RoguelikeSaveData.cs`)

- **ターン・取り立てパラメータのセーブデータ追加**:
  - `RoguelikeSaveData` クラスに以下のフィールドを追加し、ゲーム進行状況の完全保存・復元を可能にしました。
    - `nowTurn`: 現在のターン数
    - `nextDebtTurn`: 次の取り立てが発生するまでのターン数
    - `nextDebtPrice`: 次回の取り立て（ノルマ）金額
    - `leftDebtAmount`: 残り借金総額
    - `debtClearTimes`: 借金取り立てを耐え切った回数
    - `isWatchTour`: 導入ツアーを閲覧済みかどうかの判定フラグ
- **ロード時デフォルト値・安全初期化ロジックの導入**:
  - セーブデータ内の値が `0`（新規データや旧データ）の場合でも正常にプレイ可能となるよう、`MoneyManager.ReadSaveData` 内でデフォルト値（初期ターン `1`、初期借金額 `10,000,000,000`、初期取り立て頻度等）を自動設定する初期化処理を実装。
- **返済耐え抜きカウントとゲームオーバー判定**:
  - `CheckGameOver()` メソッドをリファクタリングし、ゲームオーバーか返済成功かを示す bool 返り値を設定。取り立てを耐え切った際に `_debtClearTimes` をインクリメントする処理を追加。

---

### 2. ローグライクスキルセーブ機能の実装 (`RoguelikeManager.cs`)

- **スキル保存用クラス (`RoguelikeSaveClass`) の導入**:
  - スキルID (`SkillId`)、取得状態 (`isGet`)、有効化状態 (`isActive`) を保持するシリアライズ可能な `RoguelikeSaveClass` を定義し、`RoguelikeSaveData.roguelikeSaveDatas` リストとして保存・復元可能にしました。
- **JSONロード順序制御とアンロック状態の復元**:
  - JSONファイルの読み込み完了フラグ (`isFinishLoadJson`) を導入し、データロード処理が安全に実行できるタイミングを保証。
  - セーブデータ復元時、取得済み (`isGet == true`) のスキルに対して `UnlockSkill()` を再呼び出ししてゲーム内効果を適用し、`RoguelikePanelManager.Instance.UpdateUI()` でUI表現と同期。

---

### 3. 導入ツアーのロード連動・UI表示同期 (`IntroTourDirector.cs`, `GameUIManager.cs`)

- **ツアー既読スキップ処理**:
  - `IntroTourDirector` が `IsaveDataProvider` を実装し、`isWatchTour` フラグをセーブデータへ記録。
  - すでにツアーを視聴済みのセーブデータをロードした場合、ツアー演出を再生せず直接霧フェードおよびプレイヤーの操作解禁・UI有効化を行うショートカットロジックを実装。
- **`GameUIManager` 表示フラグとツアー再生同期**:
  - `GameUIManager` に外部参照用プロパティ `IsGameUIVisible` を公開。
  - ツアー開始前に `SetGameUIVisible(false)` で UI を非表示化し、`IsGameUIVisible == false` になるのを確認してからツアー演出を発足させる厳格なコルーチン同期を適用。ツアー終了イベント (`OnTourFinished`) で `SetGameUIVisible(true)` を実行しUIを安全に復帰。

---

### 4. 訪問者システムのイベント分岐・カメラ演出強化 (`VisitorSystem.cs`)

- **訪問者イベント進行の条件分岐制御**:
  - キャラクター（`Faust`, `Gargantua`）のイベント進捗回数 (`eventProgress`) に応じた選択肢 UI の表示・非表示判定を修正。
  - `Faust`: 1回目は確定イベントとして扱い、2回目以降で選択肢を出現させる。
  - `Gargantua`: 1, 2, 5回目のイベントでは特定の報酬要素（1回目は2番目削除、2回目は1番目削除、5回目は両方付与）を自動適用し、3〜4回目のイベント時に選択肢を表示する分岐処理を追加。
- **取引カメラワークとリバースアニメーションの改善**:
  - 取引演出開始時、カメラの移動・回転アニメーションに `SetEase(Ease.InSine)` を適用しスムーズな画角遷移を実現。
  - 取引・引き出し開閉演出が完了した後、カメラを元の視点・角度へ滑らかに戻すリバースアニメーション (`Append(_mainCamera.transform.DOMove...)`) を追加。
- **マネージャー自動参照**:
  - 参照が外れていた場合に備え、取引開始時に `ItemPanelManager` や `EffectManager` を `FindFirstObjectByType` で自動検索・再割り当てする安全ロジックを記述。

---

### 5. タイプライター・取り立てイベントの挙動調整 (`TypewriterInteractable.cs`, `DebtCollectionManager.cs`)

- **取り立て発生判定と会話分岐**:
  - 次回取り立てターンカウントの判定を `NextDebtCollectionTurnCount == 0` に補正。
  - 初回取り立て (`DebtClearTimes == 0`) の場合は `Conversation_00` を再生し、2回目以降は通常の取り立て会話を表示する分岐を記述。
- **ターン経過演出 (`ShowTurnTransition`) と動作完了の順序変更**:
  - シーン遷移フェード画面の表示タイミングで `AdvanceTurn()` を確実にコールバック実行するよう順序を補正し、アニメーションとターン更新のズレを解消。
- **取り立て成功会話抽選ログの修正**:
  - `DebtCollectionManager` の成功会話シナリオ抽選ロジックで Random インデックス範囲を修正し、ログ (`Debug.LogError`) を出力してデバッグ性を向上。

---

### 6. エフェクト・持続ターンの減少タイミング補正 (`EffectManager.cs`)

- **UniRx 初回通知スキップ**:
  - ターンカウント変更の UniRx ストリーム (`OnCurrentTurnChange`) 購読時に `.Skip(1)` を挿入し、初期化時・シーン読み込み直後の不要なエフェクトターン減少処理の発火を防止。
- **無限持続エフェクト (`IsInfinity`) の保護**:
  - ターン更新時に `IsInfinity` フラグのついたエフェクトはターン数を減算せずスキップする制御を維持・再確認。

---

### 7. デバッグツール・UI排他制御・各種アセットの調整

- **`DebugTool.cs`（エディター拡張）**:
  - 所持金・各種コイン・徳ポイントの手動設定入力欄を追加し、負の値の自動補正バリデーションおよびセーブデータ生成機能を整備。
- **`SettingUIManager.cs` / `MouseHoverOutline.cs`**:
  - 設定メニューのシングルトン化および UI 開閉時の 3D アウトライン判定 (`IsOpenUI`)・視点操作の排他制御を導入。
- **各種アセット・シーンの同期**:
  - `Gargantua's Trial`, 各種 `GargantuaReward`, `Devil'sCandy`, `VintagePinball` などのScriptableObjectアセット値および `MainScene.unity`, `Scene_Visitor.unity` の配置コンポーネントパラメータを更新。

---

## 変更ファイル一覧

### スクリプト
- **[MODIFY]** `Assets/Scripts/VisitorSystem/VisitorSystem.cs`
- **[MODIFY]** `Assets/Scripts/MoneyManager.cs`
- **[MODIFY]** `Assets/Scripts/SaveData/RoguelikeSaveData.cs`
- **[MODIFY]** `Assets/Scripts/Roguelike/RoguelikeManager.cs`
- **[MODIFY]** `Assets/Scripts/Intro/IntroTourDirector.cs`
- **[MODIFY]** `Assets/Scripts/UI/GameUI/GameUIManager.cs`
- **[MODIFY]** `Assets/Scripts/UI/GameUI/EffectManager.cs`
- **[MODIFY]** `Assets/Scripts/Typewriter/TypewriterInteractable.cs`
- **[MODIFY]** `Assets/Scripts/Devil/DebtCollectionManager.cs`
- **[MODIFY]** `Assets/App/Intercom/Scripts/IntercomController.cs`
- **[MODIFY]** `Assets/Scripts/UI/SettingUI/SettingUIManager.cs`
- **[MODIFY]** `Assets/Scripts/Title/MouseHoverOutline.cs`
- **[MODIFY]** `Assets/Scripts/Editor/DebugTool.cs`

### シーン・プレハブ・アセット
- **[MODIFY]** `Assets/Scenes/MainScene.unity`
- **[MODIFY]** `Assets/Scenes/Additive/Scene_Visitor.unity`
- **[MODIFY]** `Assets/Resources/EffectData/Gargantua's Trial.asset`
- **[MODIFY]** `Assets/Resources/ItemData/Devil'sCandy.asset`
- **[MODIFY]** `Assets/Resources/ItemData/VintagePinball.asset`
- **[MODIFY]** `Assets/Resources/MoneyData/GargantuaReward1.asset`〜`5.asset`
- **[MODIFY]** `Assets/Resources/DebugData/DebugSaveData.dat`
- **[NEW]** `Assets/Resources/Prefab/TradeItem/` (.meta 含む)

---

## 確認内容・検証結果

1. **データロード＆永続化検証**:
   - ターン数、次回取り立て金額、ローグライクスキル取得状態、ツアー視聴完了フラグがセーブ＆ロード前後で正確に保存・復元されることを確認。
   - 新規セーブデータ読み込み時にもデフォルト初期値が自動設定され、例外が発生しないことを確認。
2. **訪問者システム動作検証**:
   - `Faust` および `Gargantua` の各イベント回数に応じた分岐・確定処理が意図通り動作し、取引カメラアニメーションが終了後にスムーズに元の視点へ戻ることを確認。
3. **導入ツアー＆UI連動検証**:
   - 初回起動時・2回目以降のセーブデータ読み込み時で導入ツアーの再生・スキップ挙動が正しく分かれ、UI表示状態 (`GameUIManager`) が破綻しないことを確認。
4. **タイプライター・取り立て遷移検証**:
   - タイプライター使用時のターン進展アニメーションと取り立て会話表示の切り替えタイミングが一致することを確認。

---

## 報告と今後の手順

- **Unity上での最終保存確認**: `GitWorkflow.md` に基づき、変更したシーン (`MainScene.unity`, `Scene_Visitor.unity`) やアセットがUnity側で保存完了されていることを確認。
- **ブランチの確認とPR準備**: 本作業結果をもとにコミットを行い、`GitWorkflow.md` に沿って報告およびPRの作成を進行します。
