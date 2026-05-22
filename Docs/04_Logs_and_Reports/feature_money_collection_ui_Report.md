# 作業報告書: 悪魔の取り立て会話システム・フォントズレ解消・ゲームオーバーUI演出の実装

**対応日**: 2026-05-23  
**担当**: ユーザー様 (IwamotoHinata)  
**ブランチ**: `feature/money_collection_ui`  

---

## 目的
1. **悪魔の取り立て会話システム & シナリオロード機能の構築**  
   - ゲーム内でお金が減少し、悪魔（アクマ）が取り立てを行うイベント会話システムを構築する。
   - 会話シナリオをJSONファイルから動的にロードできるようにし、日本語・英語のローカライズに対応可能にする。
   - 取り立て時の「請求金額」と「所持金」のカウントダウンや、悪魔の表情変化、BGM/SE切り替えを組み合わせた臨場感ある演出を実装する。

2. **UIフォント表示ズレの解消（SDFフォント設定の修正）**  
   - テキストの表示において、日本語フォント内の数字データのフォント幅ズレを解消するため、欧文専用フォント（Roboto Mono等）をフォールバックに設定するなどのフォント周りの最適化を行う。

3. **ゲームオーバー時のリザルトUIおよびアニメーション演出の実装**  
   - プレイヤーの資金が尽きた際にゲームオーバーとなり、演出パネルやゲームのリザルトデータ、ローグライク要素ログが段階的に表示されるリザルト画面を構築する。
   - タイトル画面への遷移処理およびタイトルシーンを作成する。

---

## 変更内容

### 1. 悪魔の取り立て会話システム
* **JSONによる会話シナリオ管理の導入**  
  - 会話データの構造を `DevilConversationData.cs` (`DevilConversationContainer`) として定義。会話キー、次の会話キー、セリフ配列、表情配列、BGMキーを保持できるようにしました。
  - `DevilConversations_JP.json` および `DevilConversations_EN.json` を作成し、インポートした会話テキストを一元管理。
* **`DebtCollectionManager.cs` の新規実装**  
  - アクマの立ち絵（`Image`）や会話パネル、名前、本文、請求額カウンタ、所持金カウンタを制御するマネージャークラスを実装。
  - JSONファイルのパスを自動判別し、ゲーム開始時に辞書形式（`_conversations`）へデシリアライズ。
  - 一文字ずつ文字が表示されるタイピングエフェクト（下矢印 `↓` による自動改行対応、クリックによるスキップ対応）を実装。
  - DOTweenを駆使したカウントアニメーションを実装。請求金額が減少しながら所持金がその分減っていく様子をドラムロールSE付きでドラマチックに表現しました。
  - 取り立て後に所持金が `0` 以下の場合にはゲームオーバー処理（`ResultUIManager.GameOverAnimation`）を呼び出し、耐えきった場合には成功メッセージを表示して元のゲームに戻る仕組みを構築。
* **アセット・演出リソースの統合**  
  - アクマの表情差分イラスト（通常、不機嫌、怒り、困り、笑顔、口開き）をインポートし、会話の進行に合わせて自動で切り替えるよう統合。
  - 会話用のBGM（MusMus提供）やSE（`debtPay.wav`）をインポートし、会話データ内のキーに応じて動的に再生・制御するシステムを構築。

### 2. フォントズレの解消とフォント環境の構築
* **TextMesh Pro (SDF) フォントアセットの生成**  
  - 日本語フォント（`Noto_Sans_JP` / `Reggae_One`）および欧文フォント（`Roboto_Mono`）をインポートし、SDFアセットとして生成。
* **数字フォントの表示ズレ修正**  
  - 日本語フォントアセット（Reggae One 等）から数字のキャラクタデータを意図的に除外し、Fallbackフォントとして等幅の `Roboto` フォントを指定。これにより、UI上で数字が並んだ際にフォント幅の不揃いによってガタつく「表示ズレ」問題を解消しました。

### 3. ゲームオーバーUIとアニメーション演出
* **`ResultUIManager.cs` および `TriangleButton.cs` の新規実装**  
  - ゲームオーバー時にパネルがフェードインし、続いて「GAME OVER」メッセージ、リザルト詳細パネル、ローグライク詳細ログパネルが段階的に時間差でフェードインする演出を `DOTween.Sequence` を用いて制御。
  - 各ゲームごとのログページ切り替え関数 `NextPage()` / `PreviousPage()` の枠組みを実装。
  - タイトル画面へ遷移する `BackToTitle()` 関数を実装。
* **シーンの構築と登録**  
  - タイトル画面となる `TitleScene.unity` を新規作成。
  - `EditorBuildSettings.asset` にタイトルシーン（Index 0）を登録し、ゲーム全体の遷移フローを確立。

---

## 対象ファイル

* **スクリプト類**
  - [NEW] `Assets/Scripts/Devil/DevilConversationData.cs` （会話データ構造の定義）
  - [NEW] `Assets/Scripts/Devil/DebtCollectionManager.cs` （悪魔の取り立てイベントの総合制御）
  - [NEW] `Assets/Scripts/Convenience/SerializeDictionary.cs` （インスペクター上での辞書シリアライズ補助）
  - [NEW] `Assets/Scripts/UI/ResultUI/ResultUIManager.cs` （ゲームオーバー・リザルトUIの演出と遷移制御）
  - [NEW] `Assets/Scripts/UI/ResultUI/TriangleButton.cs` （リザルト画面のページ遷移ボタン等）
  - [MODIFY] `Assets/Scripts/MoneyManager.cs` （取り立てに伴う資金変動メソッドの統合）

* **JSONデータ・リソース**
  - [NEW] `Assets/Resources/Conversations/DevilConversations_JP.json` （日本語会話シナリオ）
  - [NEW] `Assets/Resources/Conversations/DevilConversations_EN.json` （英語会話シナリオ）
  - [NEW] `Assets/Resources/Sound/BGM/` / `SE/` （会話中BGMおよびドラムロール・取り立てSE）
  - [NEW] `Assets/Resources/illust/devil_tmp/` （アクマの表情差分スプライト群）
  - [NEW] `Assets/Resources/Font/` （SDFフォントアセットおよびソースフォントファイル）

* **シーン・設定**
  - [NEW] `Assets/Scenes/TitleScene.unity` （タイトル画面シーン）
  - [MODIFY] `Assets/Scenes/GameScene.unity` （取り立てUI・ゲームオーバーUIオブジェクトの配置とコンポーネント設定）
  - [MODIFY] `ProjectSettings/EditorBuildSettings.asset` （タイトルシーンのビルド対象追加）

---

## 確認内容
- 悪魔の取り立てイベントが発生した際、JSONシナリオ通りに立ち絵の表情、会話テキスト、再生BGMが同期して切り替わること。
- 取り立て開始時、ドラムロール音とともに請求金額と所持金が同期してカウントダウンする演出アニメーションがスムーズに再生されること。
- 所持金がゼロ以下になった際、ゲームオーバーパネル、メッセージ、リザルトデータが順番に美しくフェードインすること。
- UIに表示される金額などの数字フォントが、ガタつくことなく綺麗に等幅で並んで表示されること。
- ゲームオーバー画面のタイトルボタン押下により、タイトルシーン（Index 0）へ正しく遷移すること。

---

## 今後の課題・TODO
- **多言語ローカライズのさらなる推進**: 残りのゲームUI要素についても Unity の Localization システムなどを用いた対応を順次実施する。
- **ゲームオーバー回避処理の実装**: 特殊アイテムなどの所持状況に応じて、悪魔の取り立て（ゲームオーバー）を回避できるロジックを `DebtCollectionManager` 内の `//TODO:アイテムでゲームオーバーを回避する` 箇所に組み込む。
- **ローグライク要素のログ取得**: `ResultUIManager` 内の `NextPage()`, `PreviousPage()` 関数において、プレイヤーがそのプレイで達成したローグライク要素の具体的な結果データを取得し、詳細ログテキストを動的に更新・反映する処理を実装する。
