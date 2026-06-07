# 作業報告書: ゲームUIフレームワーク構築、セーブシステム刷新、およびローグライクスキル連携の実装

**対応日**: 2026-06-08  
**担当**: AIエージェント (Antigravity) & ユーザー様  
**ブランチ**: `feature/game_ui`  

---

## 目的

1. **ゲームUIフレームワークとステータス表示機能の構築（`feat: ゲームUIの作成` コミット分）**
   - ゲーム中のステータス（所持金、未洗浄金、徳ポイント、次回取り立てまでのターン数）を表示し、変動時にリアルタイムで更新されるUIシステムを構築する。
   - メニュー画面（プレイヤー情報、所持アイテム、スキルツリー）を Tab キーで開閉し、遷移可能にするメニューマネージャーを構築する。
   - 獲得したアイテムの追加や消失を検知し、スクロールビュー上のボタン表示を動的に更新するアイテム UI を作成する。

2. **UniRxの導入およびデータ・セーブシステムのインターフェース化への刷新（`feat: ゲームUIの作成` コミット分）**
   - UIのリアルタイム更新やイベント検知を効率化するため、UniRx（Reactive Extensions for Unity）を導入し、`PlayerWallet` などの変数設計をリアクティブに変更する。
   - セーブ・ロード時にシーン内の各モジュールが個別にデータの書き込み・読み込みを行えるよう、`IsaveDataProvider` インターフェースを用いた走査・集約型のセーブシステムを構築する。

3. **ローグライクシステム（スキル管理・アンロック）の構築（未コミット変更分）**  
   - ゲーム内で獲得・アンロックできる能力（スキル）を管理するためのローグライクシステムを実装する。
   - スキル情報を JSON ファイルから動的にロードし、ゲーム内の辞書データとして管理可能にする。
   - まだ獲得していないスキルからランダムに指定数を選択肢として抽出する機能を提供する。

4. **報酬選択UI（`RewardSelectionUI`）の刷新と説明欄ホバー表示機能の実装（未コミット変更分）**  
   - 報酬の型を単純なテキスト配列から、新規構築した `RoguelikeData` 構造体へ刷新し、ゲームプレイ効果とデータを統合する。
   - 報酬の選択肢ボタンにポインターを合わせた（ホバーした）際に、画面下部に追加した説明用パネルにスキルの詳細説明を即座に表示する機能を実装し、UXを向上させる。
   - 各ボタンのホバー・アウトイベントを動的に制御する `ButtonHover.cs` を追加する。

5. **タイプライター（報酬獲得インタラクション）との統合（未コミット変更分）**  
   - プレイヤーがタイプライターとインタラクトした際、ローグライクマネージャーから未アンロックのスキルをランダムに2個抽出し、報酬選択UIに提示する。
   - プレイヤーが選択したスキルを実際にアンロックし、タイプライターの打鍵演出を開始させるとともに、システム側のデータを更新してUIを終了する一連の連動フローを構築する。

6. **メニューUI（ゲームUI）およびアイテム管理の最適化（未コミット変更分）**  
   - ゲーム内メニューでの画面切り替えロジックをオブジェクトのアクティブ制御（`GameObject` のオン・オフ）に変更し、画面遷移およびメニュー表示をスマートにする。
   - アイテムデータ構造（`ItemData`）に実体オブジェクトとなる `prefabData` を追加する。
   - アイテム表示パネル（`ItemPanelManager`）の初期化順を修正し、TMPro によるテキスト更新処理に最適化する。

---

## 変更内容

### 1. ゲームUIフレームワークとアイテム所持UIの実装（`feat: ゲームUIの作成` コミット分）
* **`GameUIManager.cs` の新規実装**  
  - プレイヤー情報の初期化や表示（所持金、未洗浄金、徳ポイント、次回取り立てまでのターン数）を管理。
  - Tab キーの押下によってメニューの表示状態を切り替え、表示時にはマウスカーソルを表示してゲームプレイを一時ロックし、非表示時にはカーソルをロックする一連の制御を実装。
  - 前面パネルの切り替え処理（プレイヤー情報、アイテム一覧、ローグライクスキル一覧の切り替え）をページインデックスを用いて制御。
* **`ItemPanelManager.cs` の新規実装**  
  - UniRx の `ReactiveCollection<int>` を用いて所持アイテムIDリストを管理。
  - アイテムの追加（`ObserveAdd`）および削除（`ObserveRemove`）を購読し、リスト内のアイテム名ボタンおよび詳細説明（テキスト、アイコン画像）を動的に切り替える UI 同期ロジックを構築。
* **大量のUIスプライト素材の追加**  
  - `ClearCoin.png`（洗浄金コイン）、`SabiCoin.png`（錆びたコイン）、`akumaCoin.png`（悪魔コイン）、`TurnCountFrame.png` などのゲームUI画像アセットをインポート。

### 2. UniRxによるデータ駆動設計（リアクティブ化）とセーブシステムの刷新（`feat: ゲームUIの作成` コミット分）
* **`PlayerWallet.cs` の変更**  
  - 所持金（`_washedMoneyAmount`）、未洗浄金（`_unwashedMoneyAmount`）、徳ポイント（`_virtuePointAmount`）を UniRx の `ReactiveProperty` に変更。
  - 値の変動イベントを `GameUIManager` などのUI表示クラスがリアルタイムに検知し、DOTween アニメーションと連動する仕組みに改修。
* **`RoguelikeSaveManager.cs` のインターフェース駆動化（走査集約型）**  
  - セーブ・ロード処理時に特定のオブジェクトを指定するのではなく、シーン内の `IsaveDataProvider` を実装したオブジェクトを `InterfaceFinder` で自動走査・抽出し、データの書き込み（`WriteSaveData`）および読み込み（`ReadSaveData`）を一括で処理する汎用設計に刷新。
  - セーブファイルのデバッグモード適用判定を `EditorPrefs` の `LFEngine_DebugMode` キーから取得するように変更。
* **`InterfaceFinder.cs` の新規実装**  
  - シーン内のコンポーネントから指定インターフェース（`IsaveDataProvider` など）を実装したオブジェクトを走査・抽出するヘルパークラス。

### 3. ローグライクスキルのデータ・管理システムの構築（未コミット変更分）
* **`RoguelikeData.cs` の新規実装**  
  - スキルの属性を表す `SkillType` 列挙型（`None`, `PinBall`, `FallBall`, `UFOcatcher`）を定義。
  - スキル個々のID、名前、タイプ、説明、有効フラグ、獲得フラグを保持する `RoguelikeData` クラスおよび JSON からのデシリアライズ用コンテナである `RoguelikeDataContainer` を実装。
* **`RoguelikeManager.cs` の新規実装**  
  - 指定された JSON ファイル (`RoguelikeData.json`) からゲーム開始時にスキルデータをロードし、辞書（`_roguelikeDictionary`）に展開する。
  - 未獲得のスキルから指定個数分、重複しないようにランダムに抽出する `GetLockSkills()` 関数を実装。
  - 選択されたスキルをアンロック（`isGet = true`）にし、UI更新を走らせる `UnlockSkill()` 関数を実装.
  - エディタ実行時の動作確認用として、`I` キーを押下した際にランダムなスキルを強制アンロックしてデバッグログを吐き出すテストコードを追加。
* **`RoguelikeData.json` の新規作成**  
  - ピンボール、フォールボール、UFOキャッチャーに関連する合計7種類のスキルマスタデータを定義。

### 4. 報酬選択UI (`RewardSelectionUI.cs`, `ButtonHover.cs`) の刷新・拡張（未コミット変更分）
* **説明用パネルの構築**  
  - `BuildMockUI()` メソッドで、選択ボタンの下部に説明文を表示するための `ExplainPanel`（半透明の背景パネル）および `ExplainText` を生成する処理を追加。
* **ホバー連動用コンポーネント `ButtonHover.cs` の新規実装**  
  - 報酬選択肢ボタンのそれぞれに対し、生成時に動的に `ButtonHover` コンポーネントをアタッチする。
  - ポインターがボタンに入った際（`OnPointerEnter`）に、該当スキルの `skillDescription` を説明欄に代入し、外れた際（`OnPointerExit`）にはテキストをクリアするロジックを実装。
* **引数およびデータの移行**  
  - UIの表示（`Show()`）やコールバック（`_onSelected`）の型を `string`（文字列）から `RoguelikeData` に変更し、アンロック処理との直結を可能にした。

### 5. タイプライターとの連携 (`TypewriterInteractable.cs`)（未コミット変更分）
* **実データへの接続**  
  - 従来 `RewardOptionsRepository` というモックデータを参照していた箇所を、シーン内の `RoguelikeManager` の `GetLockSkills(2)` を呼び出す形に書き換え。
  - 報酬が選択された際、`RoguelikeManager.UnlockSkill()` を通してゲーム内データとしてスキルを獲得させ、タイプライターが打鍵演出（`TypeAndUnblock`）に入りキーをロック解除するように統合。

### 6. その他の UI/データ制御の最適化（未コミット変更分）
* **`GameUIManager.cs` のメニュー切り替えロジック変更**  
  - メニュータイトルの辞書 `_menuTitle` の型を `SerializeDictionary<int, string>`（テキスト）から `SerializeDictionary<int, GameObject>`（オブジェクト）に変更。
  - メニューの開閉時に、タイトルテキストを切り替えるのではなく、該当する GameObject 自体のアクティブ状態を切り替える方式に刷新。
  - メニューを閉じた際、インデックスを `0` にリセットし、カーソルロック状態に戻す処理を明確化。
* **`ItemData.cs` / `ItemPanelManager.cs` の修正**  
  - `ItemData` にゲーム中に生成・参照可能なオブジェクトとして `prefabData` フィールドを追加。
  - `ItemPanelManager` で `ObserveAdd` / `ObserveRemove` などの購読処理を `Start` から `Awake` に移すことで、初期化順による null 参照リスクを回避。また、`TMP_Text` に限定したテキスト更新に最適化し、デバッグ確認用のキー押下ログを追加。

---

## 対象ファイル

* **新規ファイル [NEW]**
  - `Assets/Scripts/UI/GameUI/GameUIManager.cs` （ゲーム内ステータスUI/メニュー開閉管理マネージャー）
  - `Assets/Scripts/UI/GameUI/ItemPanelManager.cs` （所持アイテム表示UI管理クラス）
  - `Assets/Scripts/UI/GameUI/ItemData.cs` （アイテムのデータ定義 ScriptableObject）
  - `Assets/Scripts/UI/GameUI/ItemDataBase.cs` （アイテムマスタデータベース ScriptableObject）
  - `Assets/Scripts/Convenience/InterfaceFinder.cs` （シーン内のインターフェース実装コンポーネント走査クラス）
  - `Assets/Scripts/Roguelike/RoguelikeData.cs` （ローグライクスキルのデータ構造・列挙型定義）
  - `Assets/Scripts/Roguelike/RoguelikeManager.cs` （JSONロード、抽選、アンロック等のスキルシステム制御）
  - `Assets/Scripts/Typewriter/ButtonHover.cs` （報酬選択UIボタンのホバーイベント制御）
  - `Assets/Scripts/UI/GameUI/RoguelikePanelManager.cs` （アンロック済みスキル一覧の表示・フィルタ制御パネル）
  - `Assets/Resources/Roguelike/RoguelikeData.json` （スキルマスタデータ）
  - `Assets/Resources/illust/GameUI_tmp/` （コインやターンフレームなどのUIスプライト素材群）
  - `Assets/Plugins/UniRx/` （Reactive Extensions for Unity プラグイン群）

* **変更ファイル [MODIFY]**
  - `Assets/Scripts/Player/PlayerWallet.cs` （所持金や徳ポイント変数の ReactiveProperty 化）
  - `Assets/Scripts/SaveData/RoguelikeSaveManager.cs` （IsaveDataProvider を走査・集約するセーブ/ロード設計へのリファクタリング）
  - `Assets/Scripts/Typewriter/RewardSelectionUI.cs` （説明用パネル of 構築、ホバーコンポーネント付与、引数の型変更）
  - `Assets/Scripts/Typewriter/TypewriterInteractable.cs` （モックから RoguelikeManager のデータ接続への切り替え）
  - `Assets/Scripts/UI/GameUI/GameUIManager.cs` （メニュー切り替え時の GameObject アクティブ切り替え対応）
  - `Assets/Scripts/UI/GameUI/ItemData.cs` （`prefabData` の追加）
  - `Assets/Scripts/UI/GameUI/ItemPanelManager.cs` （初期化順修正、TMP_Text最適化、エディタテスト機能追加）
  - `Assets/Scenes/MainScene.unity` （各種マネージャー、新UIの配置と設定適用）
  - `Assets/Resources/ItemData/ItemSample02.asset` （インスペクター上でのアセット設定調整）

* **削除ファイル [DELETE]**
  - `Assets/Scripts/UnwashedMoneyManager.cs` （PlayerWalletのリアクティブ化統合に伴い削除）

---

## 確認内容

- **リアクティブUI更新動作**:
  - ゲーム中、所持金・未洗浄金・徳ポイントが増減した際に、`PlayerWallet` の ReactiveProperty 変更イベントを `GameUIManager` が正常に検知し、ドラムロールアニメーション等を伴って画面表示が即座に同期されること。
- **メニュー表示・タブ遷移**:
  - ゲーム中に Tab キーを押下した際、メニューパネルが表示され、マウスカーソルが有効化されてゲームプレイが一時停止されること。
  - メニュー画面内で、プレイヤー情報、所持アイテム、アンロックしたスキルの一覧ページが、インデックス制御により正常に切り替わること。
  - 再度 Tab キーを押して閉じた場合に、カーソルがロックされ、初期インデックスがリセットされること。
- **アイテム所持UIの動的同期**:
  - `ItemPanelManager` でアイテムIDが追加・消失した際、UniRxイベント（`ObserveAdd` / `ObserveRemove`）経由で Scroll View 内のアイテム名ボタンが即時に生成・破棄され、ボタンを選択するとその詳細テキスト・画像が説明欄に表示されること。
- **統合セーブ・ロード**:
  - ゲームのセーブ/ロードを実行した際、`InterfaceFinder` によって `ItemPanelManager` などの `IsaveDataProvider` 実装オブジェクトが走査され、アイテム所持状態などのデータが暗号化セーブデータに集約保存され、次回ゲーム起動時に正しく復元されること。
- **ローグライクシステムおよびUIホバー表示**:
  - エディタ実行中の `I` キーによるアンロック動作、タイプライター連携、ホバーでのスキル説明テキスト表示が正常に行えること。

---

## 今後の課題・TODO

- **スキルアンロック時の実効果の実装**: 
  - 現時点ではスキルがアンロック状態 (`isGet = true`) になるデータ・UIの連携までが構築されているため、今後は各スキル（ピンボールの分裂、フォールボール、UFOキャッチャーの吸引など）が実際にゲーム内で機能を発揮するように、対応するミニゲーム側の制御クラスから `RoguelikeManager.GetUnlockSkillDictionary` のデータを参照・適用するロジックを実装する必要がある。
- **多人数（マルチプレイ）対応**:
  - `TypewriterInteractable` や `RoguelikeManager` の TODO コメントにも記載の通り、将来的にマルチプレイ時にプレイヤー個別でスキル獲得・ツリー進行を行えるよう、ローカルプレイヤーごとのインスタンス識別や同期処理を拡張する。
- **セーブデータ機能との連動**:
  - 獲得したスキル一覧（`_roguelikeDictionary` で `isGet` が true のもの）を `RoguelikeSaveData` のセーブスキーマに追加し、`RoguelikeSaveManager` を介してセーブ・ロード時に状態が完全に復元されるようにする。
