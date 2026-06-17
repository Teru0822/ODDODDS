# 作業ログ: UFOキャッチャーのアーム揺れの物理挙動化 (Joint & claw-only AddForce)

**対応日**: 2026-06-17
**担当**: Antigravity
**ブランチ**: `feature/ufo_physics_sway_movement`

---

## 目的
UFOキャッチャーのクレーン移動自体は従来のキネマティック移動（緩急のない一定速度・即時停止）のままとし、アーム（爪）の揺れに対してのみ Unity の物理演算（Rigidbody, ConfigurableJoint, AddForce）を導入し、リアルな揺れを実現する。安定した従来通りの手動揺れ計算との切り替えができるようトグルオプションを提供する。

## 変更内容
1. **`UFOArmController.cs`**
   - **トグルおよび物理設定の追加**:
     - `usePhysicsSway` (bool): 物理揺れを使用するかどうかのトグル。
     - `clawPhysicsForceMultiplier` (float): アームの移動速度に応じて爪の Rigidbody に適用する、慣性を再現するための力の倍率。
   - **初期化処理 (`Start()`)**:
     - `usePhysicsSway` が有効な場合、アームの親 `_armRigidbody` は **キネマティック (isKinematic = true)** のままとし、物理移動（AddForce）は行わない。
     - 爪土台の最初のパーツ（`clawBaseParts[0]`）に対し、動的に `Rigidbody` および `ConfigurableJoint` を追加して設定。
     - Joint の設定として、移動（Translation）を完全にロックし、X軸およびZ軸周りの回転（Sway）を Free に設定。ねじれ（Y軸回転）はロック。
     - アームと爪の初期相対オフセット `originalClawLocalOffset` を記録。
   - **移動処理 (`UpdateMovement()`)**:
     - 従来のキネマティック移動（一定速度、レバーを離すと即時停止）をそのまま維持。
   - **揺れ・効果音の適用**:
     - 物理揺れ（`usePhysicsSway`）有効時、`UpdateSwayPhysics` 内でキャリッジ（親）の移動速度（`currentVel`）に反比例する慣性力（`-currentVel * clawPhysicsForceMultiplier`）を計算し、爪の Rigidbody に `AddForce` を使って適用。
     - これにより、クレーンが移動した際に爪が慣性で遅れて傾き、クレーンが停止した際に振り子のように物理的に揺れ戻す挙動を実現。
     - `UpdateGrabJingleSound()` のじゃらじゃら効果音判定において、物理挙動時は Rigidbody の `angularVelocity`（角速度）の大きさを用いて揺れ速度を検出するよう修正。

2. **`StretchRope.cs`**
   - **昇降処理の物理対応**:
     - `usePhysicsSway` が有効な場合、ロープ先端の子オブジェクトの座標をスクリプトで直接書き換えるのをスキップし、物理エンジンと競合しないようにしました。
     - 代わりに、ロープの伸縮スケールに合わせて `physicsJoint.connectedAnchor` のY軸ローカルオフセットを変化させ、吊り下げられた状態でスムーズに昇降するように制御。

## 対象ファイル
- [UFOArmController.cs](file:///c:/Users/clock/FEVER-CAPITAL/Assets/Scripts/UFOArmController.cs)
- [StretchRope.cs](file:///c:/Users/clock/FEVER-CAPITAL/Assets/Scripts/StretchRope.cs)
- [feature_ufo_physics_sway_movement_Report.md](file:///c:/Users/clock/FEVER-CAPITAL/Docs/04_Logs_and_Reports/feature_ufo_physics_sway_movement_Report.md)

## 確認内容
- クレーンの移動が従来の一定速度のままであること。
- アームの揺れに対してのみ `AddForce` と `ConfigurableJoint` による物理挙動が適用され、停止時に物理的な揺れが発生すること。
- 従来のキネマティックな揺れモード（`usePhysicsSway = false`）が壊れることなく正常に動作し続けることをコード上で確認。
- 爪の降下/上昇に伴い、Joint の `connectedAnchor` が伸縮量に応じて適切に制御されることを確認。

## 未確認事項 / 懸念点
- Unityエディタ上での実際の揺れの強さや減衰具合は、インスペクターから `clawPhysicsForceMultiplier`（力の強さ）や Rigidbody の `angularDamping`（角ドラッグ）を微調整してプレイフィールを合わせる必要があります。
