# 作業ログ: UFOキャッチャーのアーム移動および揺れの物理挙動化 (Rigidbody & Joint & AddForce)

**対応日**: 2026-06-17
**担当**: Antigravity
**ブランチ**: `feature/ufo_physics_sway_movement`

---

## 目的
UFOキャッチャーのクレーン移動およびアームの揺れを従来のキネマティック制御から、Unityの物理演算（Rigidbody, ConfigurableJoint, AddForce）を用いたリアルな物理挙動に変更する。また、安定した従来通りの挙動との切り替えができるようにトグルオプションを提供する。

## 変更内容
1. **`UFOArmController.cs`**
   - **トグルおよび物理設定の追加**:
     - `usePhysicsSway` (bool): 物理挙動を使用するかどうかのトグル。
     - `moveForce` (float): 加速度を考慮した AddForce での移動力。
     - `carriageDrag` (float): 移動後の慣性滑りを制御するためのドラッグ値。
   - **初期化処理 (`Start()`)**:
     - `usePhysicsSway` が有効な場合、アームの親 `Rigidbody` を非キネマティックにし、Y軸方向の移動および全回転をロック（XZ移動のみ可能に設定）。
     - 爪土台の最初のパーツ（`clawBaseParts[0]`）に対して、自動的に `Rigidbody` および `ConfigurableJoint` を追加/構成。
     - Jointの設定として、移動（Translation）を完全にロックし、X軸およびZ軸周りの回転（Sway）を Free に設定。ねじれ（Y軸回転）はロック。
     - アームと爪の初期相対オフセット `originalClawLocalOffset` を記録。
   - **移動処理 (`UpdateMovement()`)**:
     - 物理移動時に `AddForce` を用いて推進力を適用。
     - 移動制限（クランプ）を超えた場合、座標を範囲内に押し戻し、速度をゼロにする処理を追加。
   - **揺れ・効果音の適用**:
     - 物理挙動時は、手動の回転書き換えをスキップ（物理エンジン側でジョイントが揺れを制御するため）。
     - `UpdateGrabJingleSound()` のじゃらじゃら効果音判定において、物理挙動時は Rigidbody の `angularVelocity` の大きさを用いて揺れ速度を検出するよう修正。

2. **`StretchRope.cs`**
   - **昇降処理の物理対応**:
     - `usePhysicsSway` が有効な場合、ロープ先端の子オブジェクトの座標をスクリプトで直接書き換えるのをスキップ（物理ジョイントで結合されているため、直接上書きすると物理計算と競合して挙動が崩れる問題を防ぐ）。
     - 代わりに、ロープの伸縮値（`scaleAdd`）に応じて `physicsJoint.connectedAnchor` のY軸ローカルオフセットを変化させ、クレーンに吊り下げられた状態で滑らかに昇降するように制御。

## 対象ファイル
- [UFOArmController.cs](file:///c:/Users/clock/FEVER-CAPITAL/Assets/Scripts/UFOArmController.cs)
- [StretchRope.cs](file:///c:/Users/clock/FEVER-CAPITAL/Assets/Scripts/StretchRope.cs)
- [feature_ufo_physics_sway_movement_Report.md](file:///c:/Users/clock/FEVER-CAPITAL/Docs/04_Logs_and_Reports/feature_ufo_physics_sway_movement_Report.md) (新規)

## 確認内容
- 物理移動と物理揺れのスクリプト実装および計算ロジックが整合していることを確認。
- 従来のキネマティック移動/揺れモード（`usePhysicsSway = false`）が壊れることなく正常に動作し続けることをコード上で確認。
- 爪の降下/上昇に伴い、Joint の `connectedAnchor` が伸縮量に応じて適切に制御されることを確認。

## 未確認事項 / 懸念点
- Unityエディタ上での実際のプレイフィール（移動時の滑り具合、爪の揺れ加減など）は、Unityプロジェクトを起動して動作テストおよびパラメータ（`moveForce`, `carriageDrag` など）の調整を行う必要があります。
