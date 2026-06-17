# 作業ログ: UFOキャッチャーアームのハイブリッド物理揺れの導入 (PD制御トルク追従)

**対応日**: 2026-06-17
**担当**: Antigravity
**ブランチ**: `feature/ufo_physics_sway_hybrid`

---

## 目的
クレーンの移動自体はキネマティックな等速移動を維持しつつ、アームの揺れに対してのみ物理演算を導入する。その際、数学的に算出された「理想の揺れ角度（ターゲット角度）」に対し、PD（比例・微分）制御に基づいたトルク（回転力）を加えることで理想の揺れに追従させ、コインや壁との衝突時には物理的に跳ね返りや押し戻しが発生する「ハイブリッド揺れ挙動」を実現する。

## 変更内容
1. **`UFOArmController.cs`**
   - **インスペクター項目の追加**:
     - `usePhysicsSway` (bool): 物理揺れを有効にするフラグ。
     - `clawPhysicsSpring` (float): 理想の揺れ角度へ引き戻すバネの力（P項ゲイン）。デフォルト 150。
     - `clawPhysicsDamping` (float): 揺れを抑えるブレーキの力（D項ゲイン）。デフォルト 12。
   - **初期化処理 (`Start()`)**:
     - 物理揺れ有効時、親キャリッジ `_armRigidbody` をキネマティック設定 (`isKinematic = true`) にし、爪の基底オブジェクト（`clawBaseParts[0]`）に対して `Rigidbody` と `ConfigurableJoint` を動的に構成。
     - ジョイントの移動（Translation）を完全にロックし、X軸およびZ軸周りの回転を Free に設定。
   - **揺れ制御 (`UpdateSwayPhysics()`)**:
     - 従来の数学的揺れ角度 `clawSwayRot` を毎フレーム算出して「目標とするワールド回転」に変換。
     - 現在の爪のワールド回転との角度差（`angleDiff`）と回転軸（`axis`）を取得し、角速度 `angularVelocity` を考慮した以下のPD制御式からトルク（`torque`）を計算：
       `torque = axis * angleDiff(rad) * clawPhysicsSpring - angularVelocity * clawPhysicsDamping`
     - このトルクを `_clawRigidbody.AddTorque(torque, ForceMode.Acceleration)` にて物理的に適用。
     - 物理揺れ有効時は、手動での爪土台の直接的な回転上書きをスキップ。
   - **効果音の連動**:
     - 物理揺れ有効時、揺れの計測速度として爪 `Rigidbody` の `angularVelocity.magnitude`（物理角速度）を使用するよう修正。

2. **`StretchRope.cs`**
   - **伸縮処理の物理対応**:
     - 物理揺れ有効時、アーム子オブジェクトの座標の直接書き換えをスキップ。
     - 代わりに伸縮量（`scaleAdd`）に応じて `physicsJoint.connectedAnchor` のY軸ローカル座標を上下させ、物理的にアームが吊り下げられた状態で昇降するように対応。

## 対象ファイル
- [UFOArmController.cs](file:///c:/Users/clock/FEVER-CAPITAL/Assets/Scripts/UFOArmController.cs)
- [StretchRope.cs](file:///c:/Users/clock/FEVER-CAPITAL/Assets/Scripts/StretchRope.cs)
- [feature_ufo_physics_sway_hybrid_Report.md](file:///c:/Users/clock/FEVER-CAPITAL/Docs/04_Logs_and_Reports/feature_ufo_physics_sway_hybrid_Report.md) (新規)

## 確認内容
- 従来のキネマティックな一定速度移動（キャリッジ）が崩れていないこと。
- アームの揺れに対してのみ、バネダンパによる数学ターゲット追従のトルクが加わり、自然に物理揺れが発生すること。
- 従来のキネマティック揺れモード（`usePhysicsSway = false`）が完全に元通り動作すること。

## 未確認事項 / 懸念点
- 実際のゲームプレイにおける跳ね返りや追従の感触は、インスペクター上で `clawPhysicsSpring` と `clawPhysicsDamping` を調整しながら最適なフィールを合わせる必要があります。
