# 作業ログ: UFOキャッチャーアームの揺れの物理化 (AddForce直接適用 & 接続パーツ自動親子化)

**対応日**: 2026-06-17
**担当**: Antigravity
**ブランチ**: `feature/ufo_physics_sway_addforce`

---

## 目的
レバーの円形操作改善（四角クランプ解消、スナップイージング）は維持した上で、アーム（爪）の揺れ計算をすべて `AddForce` による物理演算に置き換える。その際、前回の物理化の課題であった「爪アセンブリの空中分解（潰れてしまう現象）」を防ぐため、実行時に爪の全サブパーツおよびロープ連動オブジェクトを自動的に物理爪ベースの配下に親子化する処理を組み込む。

## 変更内容
1. **`UFOArmController.cs`**
   - **インスペクター設定項目の追加**:
     - `usePhysicsSway` (bool): 物理揺れを有効にするフラグ。
     - `clawPhysicsForceMultiplier` (float): アーム移動速度に対して爪の Rigidbody に加える逆方向の力の倍率。デフォルト 20。
   - **初期化処理 (`Start()`)**:
     - 物理揺れ有効時、親キャリッジ `_armRigidbody` をキネマティック設定 (`isKinematic = true`) にし、アーム自体は従来の等速移動を維持。
     - 爪土台の最初のパーツ（`clawBaseParts[0]`）に対して `Rigidbody`（非キネマティック）および `ConfigurableJoint` を動的に構成。
      - **物理ターゲットの自動検出と接続パーツ自動親子化処理**:
        - 爪ベース（`clawBaseParts[0]`）の親階層から名前が `"cube"` であるオブジェクトを自動的に検索し、それを物理ボディ（揺らす対象の `physicsTarget`）とします。
        - 物理ターゲットの配下に、他のすべての爪ベースパーツ（`clawBaseParts[0...]`）およびロープ連動オブジェクト（`StretchRope.attachedObjects`）を実行時に自動的に再配置（`SetParent`）します。これにより、物理ターゲット（`Cube`）を主軸とした振り子運動を可能にし、かつ爪モデルのパーツ同士が空中分解して崩壊する現象を防止します。
   - **揺れ物理処理 (`UpdateSwayPhysics()`)**:
     - 物理揺れ有効時、キャリッジ速度（`currentVel`）に反比例する慣性力（`-currentVel * clawPhysicsForceMultiplier`）を計算し、爪の Rigidbody に対し `AddForce` を用いて適用（Y方向の力は 0f に制限）。
     - 有効時は手動での爪土台回転上書きをスキップ。
   - **効果音の連動**:
     - 物理揺れ有効時、揺れの計測速度として爪 `Rigidbody` の `angularVelocity.magnitude`（物理角速度）を使用するよう修正。

2. **`StretchRope.cs`**
   - **伸縮処理の物理対応**:
     - 物理揺れ有効時は、アーム子オブジェクトの座標の直接書き換えをスキップ。
     - 代わりに伸縮量（`scaleAdd`）に応じて `physicsJoint.connectedAnchor` のY軸ローカル座標を上下させ、物理的にアームが吊り下げられた状態で昇降するように対応。

## 対象ファイル
- [UFOArmController.cs](file:///c:/Users/clock/FEVER-CAPITAL/Assets/Scripts/UFOArmController.cs)
- [StretchRope.cs](file:///c:/Users/clock/FEVER-CAPITAL/Assets/Scripts/StretchRope.cs)
- [feature_ufo_physics_sway_addforce_Report.md](file:///c:/Users/clock/FEVER-CAPITAL/Docs/04_Logs_and_Reports/feature_ufo_physics_sway_addforce_Report.md) (新規)

## 確認内容
- クレーンの移動が従来の一定速度のままであること。
- アームの揺れに対してのみ `AddForce` とジョイントによる物理挙動が適用され、移動中や停止時に滑らかな物理揺れが発生すること。
- 物理化を適用しても、パーツ自動親子化によって爪モデル全体が分離せず一体のまま綺麗に揺れ動くことを確認。
- 従来のキネマティックな揺れモード（`usePhysicsSway = false`）が完全に元通り動作すること。

## 未確認事項 / 懸念点
- 実際のゲームプレイにおける物理揺れの強さや減衰具合は、インスペクターから `clawPhysicsForceMultiplier` や Rigidbody の Angular Damping を調整して最適な感触に合わせる必要があります。
