# 3D 紙の燃焼エフェクト実装プラン（改訂版）

## 概要

`paper_cube.prefab`（Unity 標準 Cube の 3D メッシュ）を炎で燃やす演出を追加する。
参考動画: [The Burning Paper Effect in Unity VFX Graph](https://www.youtube.com/watch?v=fgJf-gNq-1k)

### 使用技術（すべて無料・商用利用可）
| 技術 | 説明 |
|---|---|
| **Shader Graph** | Unity 内蔵。紙メッシュのアルファクリップ溶解＋炎の発光を担当 |
| **既存炎 Prefab** | `Assets/Vefects/Free Fire VFX URP/Particles/VFX_Fire_01_Small_Smoke.prefab`（Asset Store 無料配布、Standard EULA） |
| **C# スクリプト** | `Assets/Scripts/PaperBurn/RealisticPaperBurn.cs`（作成済み） |

### なぜ Quad でなく Cube か
紙は `paper_cube.prefab`（3D Cube メッシュ）。カメラが斜めから見ても側面・上下面が見えるため、Shader Graph の座標は UV.y ではなく **Object Space Y** を使う必要がある。

---

## 構成図

```
paper_cube (GameObject)
├── MeshRenderer  ← PaperBurnMaterial（PaperBurn.shadergraph から作成）
├── RealisticPaperBurn.cs  ← 演出統合コントローラー（作成済み）
├── [子] FireEffect  ← VFX_Fire_01_Small_Smoke のインスタンス（スクリプトが自動生成）
└── [子] FireLight   ← Point Light（オレンジ、手動で追加）
```

---

## Step 1: Shader Graph の作成（Unity Editor での作業）

### 何をするか
「紙が下から上へ炎で消えていく」シェーダーを Shader Graph で作る。
Shader Graph は「箱（ノード）を線で繋いでシェーダーを作るビジュアルエディタ」。

### 手順

**1-1. ファイルを作成する**
1. Project ウィンドウで `Assets/Shaders/` フォルダを右クリック
2. `Create → Shader Graph → URP → Lit Shader Graph` を選択
3. 名前を `PaperBurn` に変更（Enter で確定）

**1-2. Graph Settings を設定する**
1. `PaperBurn.shadergraph` をダブルクリックして開く
2. 右上の **Graph Settings** タブをクリック
3. `Surface Type` を `Opaque` に設定（デフォルト）
4. `Alpha Clipping` にチェックを入れる ← **重要。これがないと紙が消えない**
5. `Render Face` を `Both` に変更（裏面も見せる）

**1-3. プロパティ（Blackboard）を追加する**
左側の **Blackboard** パネルで「+」ボタンをクリックしてプロパティを追加する。
**右クリック → Rename で名前を変更し、右側 Reference 欄を以下の通り設定すること**（C# から SetFloat で値を渡すのに必要）。

| 追加する順 | 型 | 表示名（Name） | Reference（必須） | 初期値 |
|---|---|---|---|---|
| 1 | Texture2D | MainTexture | `_MainTex` | なし |
| 2 | Float | BurnProgress | `_BurnProgress` | 0 |
| 3 | Color (HDR) | FireColor | `_FireColor` | オレンジ R=3 G=1.2 B=0.1（HDR輝度を上げる） |
| 4 | Float | FireWidth | `_FireWidth` | 0.08 |
| 5 | Color | CharColor | `_CharColor` | 黒 #111111 |
| 6 | Float | CharWidth | `_CharWidth` | 0.12 |
| 7 | Float | NoiseScale | `_NoiseScale` | 12 |
| 8 | Float | NoiseSpeed | `_NoiseSpeed` | 0.04 |

> **Reference の設定方法**: Blackboard のプロパティを右クリック → Edit Reference → 上記の Reference 名を入力する

**1-4. ノードを作成して繋ぐ**

Shader Graph のキャンバス上で **スペースキー** または **右クリック → Add Node** でノードを追加できる。

以下の順番でノードを追加・接続する：

---

**ブロック A: 燃焼マスクの計算**

```
[Position]ノード
  → Object Space を選択
  → 出力の Y 成分を取り出す（Split ノード → G 出力）
  → [Remap]ノードで -0.5〜0.5 を 0〜1 に変換
  → これが NormalizedY（紙の下=0、上=1）

[UV]ノード（UV0）
  + [Time]ノード × NoiseSpeed プロパティ
  → [Add]ノードで足す
  → これが AnimatedUV（炎がゆらゆらするためのUV）

[Voronoi]ノード
  → UV 入力: AnimatedUV を接続
  → Cell Density 入力: NoiseScale プロパティを接続

[Simple Noise]ノード
  → UV 入力: AnimatedUV を接続
  → Scale 入力: NoiseScale × 1.5（Multiply ノード）を接続

[Add]ノード
  → A: Voronoi の Output
  → B: Simple Noise の Out
  → [Clamp]ノード (0, 1) で収める
  → これが CombinedNoise

[Add]ノード
  → A: CombinedNoise
  → B: NormalizedY × 0.3（Multiply ノード）
  → これが FinalMask
```

---

**ブロック B: Alpha（紙を消す）**

```
[Step]ノード
  → Edge: BurnProgress プロパティ
  → In: FinalMask
  → 出力を [Fragment] の Alpha ポートへ接続
```
> Step ノードは「In > Edge なら 1（残る）、それ以外は 0（消える）」を返す。Alpha Clip 閾値 0.5 により、0 の部分が透明（消滅）になる。

---

**ブロック C: Emission（炎の発光）**

```
// 炎帯マスク = 「まだ燃えていない領域」から「BurnProgress + FireWidth より先」を引いた細い帯
[Step]ノード A
  → Edge: BurnProgress プロパティ
  → In: FinalMask
  → 出力: StepA

[Step]ノード B
  → Edge: [Add]ノード（BurnProgress + FireWidth）
  → In: FinalMask
  → 出力: StepB

[Subtract]ノード
  → A: StepA, B: StepB
  → これが FireMask（値 0 か 1）

[Multiply]ノード
  → A: FireMask, B: FireColor プロパティ（HDR Color）
  → 出力を [Fragment] の Emission ポートへ接続
```

---

**ブロック D: Base Color（紙の色と焦げ）**

```
// 焦げマスク = FireWidth の外側〜CharWidth の範囲
[Step]ノード C
  → Edge: BurnProgress + FireWidth（Add ノード）
  → In: FinalMask

[Step]ノード D
  → Edge: BurnProgress + FireWidth + CharWidth（2つの Add ノード）
  → In: FinalMask

[Subtract]ノード
  → A: StepC, B: StepD
  → これが CharMask

[Sample Texture 2D]ノード
  → Texture: MainTexture プロパティ
  → 出力の RGBA を取り出す

[Lerp]ノード
  → A: SampleTexture2D の RGB 出力（紙のテクスチャ色）
  → B: CharColor プロパティ（黒）
  → T: CharMask
  → 出力を [Fragment] の Base Color ポートへ接続
```

**1-5. 保存**
Ctrl+S で Shader Graph を保存する。

---

## Step 2: マテリアルを作成してメッシュに適用

1. Project ウィンドウで `Assets/Shaders/PaperBurn.shadergraph` を右クリック
2. `Create → Material` を選択 → 名前を `PaperBurnMaterial` に変更
3. `Assets/models/Furniture/paper_cube.prefab` をダブルクリックして Prefab Mode で開く
4. 階層の一番上（paper_cube 本体）を選択 → Inspector の MeshRenderer の Material を `PaperBurnMaterial` に変更
5. **Prefab を保存**（Ctrl+S）

---

## Step 3: paper_cube.prefab にコンポーネントを追加

Prefab Mode のまま作業する。

**3-1. RealisticPaperBurn スクリプトを追加**
1. paper_cube の Inspector → `Add Component` → `RealisticPaperBurn` を検索して追加
2. 以下をインスペクターで設定：
   - `Paper Renderer`: 同オブジェクトの MeshRenderer をドラッグ
   - `Fire Prefab`: `Assets/Vefects/Free Fire VFX URP/Particles/VFX_Fire_01_Small_Smoke.prefab` をドラッグ
   - `Burn Duration`: 3（好みで変更可）
   - `Burn Axis`: Y=1（デフォルト。紙が縦向きなら X=1 に変更）

**3-2. FireLight を追加（任意）**
1. paper_cube の子オブジェクトを追加 → 右クリック → `Light → Point Light`
2. 名前を `FireLight` に変更
3. Color: オレンジ (255, 120, 30)、Range: 1.5、初期 intensity: 1
4. RealisticPaperBurn の `Fire Light` フィールドにこの Light をドラッグ

**3-3. Prefab を保存**（Ctrl+S）

---

## Step 4: 動作テスト

1. `paper_cube.prefab` を Scene にドラッグして配置
2. そのオブジェクトを選択 → Inspector で `RealisticPaperBurn` コンポーネントを右クリック
3. **「Test Burn」** を選択 → 再生しなくても Editor 上でプレビュー可能
4. 燃え方がおかしければ `PaperBurnMaterial` を選択して各パラメータをスライダーで調整

---

## 調整のポイント（インスペクターから変更可）

| 項目 | 場所 | 説明 |
|---|---|---|
| 燃焼速度 | `RealisticPaperBurn → Burn Duration` | 小さいほど速く燃える |
| 燃焼方向 | `RealisticPaperBurn → Burn Axis` | 紙の向きに合わせて X/Y/Z を設定 |
| 炎の色 | `PaperBurnMaterial → FireColor` | HDR値を上げると Bloom が強くなる |
| 炎の帯の太さ | `PaperBurnMaterial → FireWidth` | 大きいほど炎ラインが太い |
| 焦げの範囲 | `PaperBurnMaterial → CharWidth` | 大きいほど黒焦げが広がる |
| ノイズの細かさ | `PaperBurnMaterial → NoiseScale` | 大きいほどギザギザが細かい |

---

## 参考リソース

| リソース | URL |
|---|---|
| 参考動画 | https://www.youtube.com/watch?v=fgJf-gNq-1k |
| Unity Shader Graph 公式ドキュメント | Package Manager → Shader Graph → View Documentation |
| VFX Graph サンプル（追加で導入可） | Package Manager → Visual Effect Graph → Samples タブ |
| 燃焼音 (CC0) | https://freesound.org（"paper burning" で検索） |
