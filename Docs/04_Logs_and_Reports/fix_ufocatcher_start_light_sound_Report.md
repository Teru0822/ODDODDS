# UFOキャッチャー起動時のライト消灯音バグ修正およびライト点灯仕様の変更 報告書

## どの部分をどう変えたか
* [UFOCameraController.cs](file:///c:/Users/clock/FEVER-CAPITAL/Assets/Scripts/UFOCameraController.cs) を修正しました。
  * **起動時の消灯音バグの修正**:
    * `SetPlaySpotlight(bool active, bool playSound = true)` に効果音再生を制御する `playSound` フラグを追加。
    * `Start()` メソッド内で初期消灯を行う際に `SetPlaySpotlight(false, false)` とし、起動時のカチッという効果音を無効化。
  * **ライト点灯・消灯仕様の変更**:
    * 遷移完了時（`EnterUfoMode()`）に `SetPlaySpotlight(true)` を呼び出し、UFOキャッチャー画面に遷移した直後に自動点灯するように変更。
    * これに伴い、落下したコインが3枚破棄されたタイミングでのライト点灯処理（`NotifyCoinDestroyed` 内のトリガー）を削除。
    * タイマー切れ（プレイ終了）による自動消灯（`Update` 内の `SetPlaySpotlight(false)`）をコメントアウトし、プレイ画面に滞在している間は点灯状態を維持。
    * 元のFPS視点に戻る際（`TransitionBackToPlayerCamera()`）に `SetPlaySpotlight(false)` を実行して消灯・消灯音再生を行うように変更。

## 新たに何が出来るようになったか
* UFOキャッチャーにクリックして近づいた（ズーム遷移完了した）段階で、自動的にライトが点灯して中が明るくなるため、プレイ開始前の視認性が向上しました。
* 支払いを経て制限時間が終了しても、UFOキャッチャー画面にいる間はライトがついた状態がキープされるようになり、プレイヤーがFPS視点に戻るときに初めてライトが消えるといった自然な演出になりました。
* ゲーム起動時に一瞬消灯音が鳴ってしまう不具合が解消されました。

## 確認した内容
* コード修正後の構文チェック、および `git diff` による差分確認。

## 未確認事項 / 懸念点
* Unityエディタ上での実際の再生確認（音響・演出テスト）は未実施。
