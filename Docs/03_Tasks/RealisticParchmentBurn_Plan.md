# リアルな羊皮紙の燃焼エフェクト（Realistic Parchment Burn）詳細実装プラン

## 1. 背景と目的
現在のプロジェクト内に存在する「Cartoon FX」などのディゾルブ用アセットは、アニメ調の滑らかなノイズを使用しているため、ゲームのダークでリアルな雰囲気に合致しないという課題がある。
本プランでは、Unity 6の標準機能である**Shader Graph**を活用し、ザラついた質感、熱による焦げ跡、高輝度の炎の境界線を備えた**「リアルな羊皮紙の燃焼エフェクト」**を3D空間上で構築する手順を非常に詳細に定義する。

## 2. 構成要素の全体像
本エフェクトは、視覚と動的な光の演出を組み合わせた以下の3要素で構成される。
1. **専用の URP Lit Shader Graph**（燃焼・焦げ・消滅の論理と描画を司る）
2. **演出制御用 C# スクリプト**（時間経過による燃え具合の進行、発光の揺らめき、パーティクルの発生を統合制御する）
3. **補助演出（VFX）**（舞い散る火の粉 `Particle System` と、炎の揺らめきを周囲の環境に反射させる `Point Light`）

---

## 3. 実装詳細手順

### フェーズ 1: Shader Graph の完全構築
`Assets/Shaders/RealisticBurn.shadergraph` として新規に URP Lit Shader Graph を作成する。

**Graph Settings**:
- `Surface Type`: **Opaque**
  > ⚠️ `Transparent` にしてはいけない。燃焼エフェクトはグラデーション透明ではなく「ハードエッジで消える」動作なので、Transparentにすると他の透明オブジェクトとの描画順序が破綻しやすい。
- `Alpha Clipping`: **ON**、閾値 = **0.5（固定）**
  > `Alpha Clip Threshold` ポートには何も接続しない。固定値 0.5 で十分。
- `Render Face`: Both (紙の裏側も見せる場合)

#### 1-1. プロパティ (Blackboard) の定義
| プロパティ名（表示名） | **Reference name** | 型 | 推奨初期値 | 用途 |
| :--- | :--- | :--- | :--- | :--- |
| `MainTexture` | `_MainTexture` | Texture2D | (羊皮紙画像) | 燃える前のベースとなる紙のテクスチャ |
| `BurnProgress` | **`_BurnProgress`** | Float | 0.0 | 燃焼の進行度 (0.0: 未燃焼 ～ 1.0: 灰) |
| `FireColor` | `_FireColor` | Color (HDR) | 輝度+3のオレンジ | 境界線の炎の色。HDRで強く発光させる |
| `FireWidth` | `_FireWidth` | Float | 0.03 | 炎の帯の太さ |
| `CharColor` | `_CharColor` | Color | 黒 (#111111) | 焦げ跡の色 |
| `CharWidth` | `_CharWidth` | Float | 0.1 | 焦げ跡の帯の太さ |
| `NoiseScale` | `_NoiseScale` | Float | 15.0 | ノイズの細かさ |
| `NoiseScrollSpeed` | `_NoiseScrollSpeed` | Float | 0.05 | ノイズUVのスクロール速度（炎の揺らぎ） |
| `BurnDirection` | `_BurnDirection` | Float | 0.5 | 燃焼の方向性 (0=なし ～ 1=下から上に強く燃える) |

> ⚠️ **Reference name の設定が必須**: Blackboard 上でプロパティを右クリックし `Reference` 欄を上記の通り明示設定すること。設定しないと C# スクリプトからの `SetFloat("_BurnProgress", ...)` が反映されない。

#### 1-2. 高品質なノイズのプロシージャル生成
単純なテクスチャではなく、自然界の炎の揺らぎを表現する複雑なノイズを計算で作る。また、`Time` ノードを組み合わせることで炎の境界線が常に揺らぎ続ける動的な表現にする。

1. **アニメーション用 UV の生成**:
   - `UV` ノードを作成し、`Time` ノードに `NoiseScrollSpeed` を乗算した値を加算する。これをノイズ入力のUVとして使う。これにより `BurnProgress` が変化しない間も炎の境界線が揺らぎ続ける。
2. `Voronoi` ノードを作成 (Cell Density に `NoiseScale` を接続、UVには上記のアニメーションUVを使用)。
   > ⚠️ Voronoi はシェーダー計算の中でも重いノード。複数インスタンスが同時に燃える場合はパフォーマンスを要確認。低スペック端末ターゲットの場合は `Gradient Noise` のみへの変更を検討すること。
3. `Simple Noise` ノードを作成 (Scale に `NoiseScale * 1.5` 程度を設定、UVには同様のアニメーションUVを使用)。
4. 上記2つの出力を **`Add`** ノードで加算し、`Clamp(0, 1)` で収める。
   > ⚠️ `Multiply`（乗算）ではなく `Add` を使うこと。Voronoi (0〜1) × Simple Noise (0〜1) の乗算は値が全体的に小さくなりすぎ（平均 0.25 程度）、炎の模様が細かくなりすぎる。加算の方が2つのノイズ特性がバランスよく混ざる。

5. **燃焼方向性の付加**: UV.y（紙の縦方向）に `BurnDirection` を乗算した値をノイズ値に加算する。これにより「下の辺ほど先に燃える」自然な方向性が生まれる。
   ```
   // ノードグラフのイメージ
   FinalNoise = Clamp(VoronoiNoise + SimpleNoise, 0, 1)
              + UV.y × BurnDirection
   ```
   この最終ノイズ値を **$N$** とする。

#### 1-3. マスク生成と出力のロジック
ノイズ値 **$N$** と 進行度 **$B$ (`BurnProgress`)** を比較して3つの層に分解する。

**① Alpha（透明度・消滅）**
- 計算式: `Step(Edge: B, In: N)`
- 結果: ノイズ値が進行度より高い（まだ燃えていない）部分が 1、低い部分が 0 となる。
- 接続先: 出力をマスターノードの `Alpha` **のみ**に接続する。
  > ⚠️ `Alpha Clip Threshold` ポートには接続しないこと。`Alpha Clip Threshold` は固定値であり、動的マスクを接続する用途ではない。

**② Emission（炎の帯）**
- 計算式: `SmoothStep(Edge1: B, Edge2: B + FireWidth, In: N)` から `SmoothStep(Edge1: B + FireWidth*0.1, Edge2: B + FireWidth, In: N)` を引く、あるいはシンプルに:
  - `Step(Edge: B, In: N) - Step(Edge: B + FireWidth, In: N)` でバイナリマスクを作り、`Multiply` で `FireColor` (HDR) を掛ける。
  > 炎の輪郭をより柔らかくしたい場合は `SmoothStep` を使うことで自然なグロー感が出る。
- 接続先: マスターノードの `Emission` に接続。

**③ Base Color（紙と焦げの合成）**
- 焦げマスクの計算式: `Step(Edge: B + FireWidth, In: N) - Step(Edge: B + FireWidth + CharWidth, In: N)`
- 紙の色の取得: `Sample Texture 2D` ノードで `MainTexture` の色を取得する。
- 合成処理: `Lerp` ノードを作成し、`A` に `MainTexture` の色、`B` に `CharColor`（黒）を接続し、`T` に上記の「焦げマスク」を接続する。
  > より自然な焦げ（境界がくっきり、奥に向かって徐々に薄れる）にしたい場合は焦げマスクを SmoothStep で計算するとよい。
- 接続先: マスターノードの `Base Color` に接続。

---

### フェーズ 2: 補助VFXオブジェクトのセットアップ
3Dの Quad オブジェクトに上記のマテリアルを適用し、さらに以下のコンポーネントを子オブジェクトとして追加する。

#### 2-1. 火の粉パーティクル (Particle System)
燃え尽きる境界線から上に舞い上がる火の粉（Embers）を表現する。
- **Duration**: 5.0 (Looping: OFF)
- **Start Lifetime**: 1.5 ～ 2.5 (Random Between Two Constants)
- **Start Speed**: 0.5 ～ 1.0
- **Start Size**: 0.02 ～ 0.05
- **Start Color**: HDRのオレンジから暗い赤へのランダム
- **Emission**: Rate over Time = 50
- **Shape**: Box (スケールをQuadの紙の大きさに合わせる)
  > ⚠️ 理想的には「燃焼境界線」に追従してEmitterを動かすべきだが、実装コストが高い。Box（紙全体）から出す近似でも視覚的に許容できる。境界線追従が必要な場合は C# スクリプトで `emberParticles.transform.localPosition` を `BurnProgress` に合わせて毎フレーム更新する実装を追加すること。
- **Velocity over Lifetime**: Y軸方向に微小なプラス値（熱で上に昇る気流）
- **Noise**: Strength = 0.5, Frequency = 1.0 (火の粉特有のジグザグな動き)
- **Renderer**: MaterialにはデフォルトのParticleマテリアル等を使用。

#### 2-2. 炎の環境光 (Point Light)
燃えている間、周囲のオブジェクト（机など）をオレンジ色に照らす。
- **Color**: 濃いオレンジ色
- **Range**: 2.0 ～ 3.0
- **Intensity**: デフォルト 2.0 程度（※後述のスクリプトで揺らがせる）

---

### フェーズ 3: 統合制御用 C# スクリプトの実装
以下のスクリプトを作成し、親のQuadオブジェクトにアタッチする。
これにより「紙が燃える」「火の粉が舞う」「光がチカチカ揺れる」を完全に同期させる。

```csharp
using UnityEngine;
using UnityEngine.Events;

public class RealisticPaperBurn : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Shaderを適用したメッシュレンダラー")]
    public Renderer paperRenderer;
    [Tooltip("火の粉のパーティクルシステム")]
    public ParticleSystem emberParticles;
    [Tooltip("周囲を照らす炎の光源")]
    public Light fireLight;

    [Header("Burn Settings")]
    [Tooltip("燃え尽きるまでの秒数")]
    public float burnDuration = 3.0f;
    [Tooltip("Shader内のBurnProgressプロパティ名（Blackboard の Reference name と一致させること）")]
    public string progressPropertyName = "_BurnProgress";

    [Header("Light Flicker Settings")]
    public float minLightIntensity = 1.0f;
    public float maxLightIntensity = 3.0f;
    public float flickerSpeed = 15.0f;

    [Header("Events")]
    public UnityEvent onBurnComplete;

    private Material _instancedMaterial;
    private float _currentProgress = 0f;
    private bool _isBurning = false;
    private float _flickerOffset; // 複数インスタンスで揺らぎがずれるようにする

    void Awake()
    {
        // インスタンスごとに異なるノイズのオフセットを設定
        // （複数の紙が同時に燃えても光の揺らぎが同期しない）
        _flickerOffset = Random.Range(0f, 100f);
    }

    void Start()
    {
        if (paperRenderer != null)
        {
            // Renderer.material は呼び出し時点でマテリアルインスタンスを自動生成する
            _instancedMaterial = paperRenderer.material;
            _instancedMaterial.SetFloat(progressPropertyName, 0f);
        }
        
        if (fireLight != null)
        {
            fireLight.enabled = false;
        }

        if (emberParticles != null)
        {
            emberParticles.Stop();
        }
    }

    void OnDestroy()
    {
        // Start() で生成したマテリアルインスタンスを明示的に破棄しないとメモリリークになる
        if (_instancedMaterial != null)
            Destroy(_instancedMaterial);
    }

    void Update()
    {
        if (!_isBurning) return;

        // 1. マテリアルの進行度を更新
        _currentProgress += (Time.deltaTime / burnDuration);
        float clampedProgress = Mathf.Clamp01(_currentProgress);
        
        if (_instancedMaterial != null)
        {
            _instancedMaterial.SetFloat(progressPropertyName, clampedProgress);
        }

        // 2. 光の揺らめき（Flicker）処理
        if (fireLight != null && clampedProgress < 1.0f)
        {
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, _flickerOffset);
            fireLight.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, noise);
        }

        // 3. 燃焼完了判定
        if (clampedProgress >= 1.0f)
        {
            _isBurning = false;
            
            if (fireLight != null) fireLight.enabled = false;
            if (emberParticles != null) emberParticles.Stop(); // 新規発生を止める（残っている火の粉は消えるまで残す）

            onBurnComplete?.Invoke();
        }
    }

    /// <summary>
    /// 外部から呼び出して燃焼を開始するメソッド
    /// </summary>
    public void StartBurning()
    {
        if (_isBurning) return;
        
        _isBurning = true;
        _currentProgress = 0f;

        if (fireLight != null) fireLight.enabled = true;
        if (emberParticles != null) emberParticles.Play();
    }

    [ContextMenu("Test Burn")]
    public void TestBurn()
    {
        StartBurning();
    }
}
```

---

## 4. クオリティ向上: 追加表現の実装方法

基本実装（フェーズ1〜3）完了後に取り組む発展的な表現。  
現プランのみで完成度 **60〜70点**、以下を順に追加することで **80〜90点** を目指せる。

---

### 4-1. 炎の3D感

**根本的な問題**: Quad は平面なのでカメラが斜めになると薄さが露骨に見え、Emission の発光もフラットに見える。

**解決策（難易度順）:**

**① 最も現実的: VFX Graph で炎リムをパーティクル化**  
Emission の炎の帯をシェーダーで描くのをやめ、VFX Graph のパーティクルとして燃焼境界線に沿って小さな炎パーティクルを発生させる。パーティクルは常にカメラに向くビルボードなので角度問題が解消する。Unity 6 では VFX Graph が標準搭載されているため追加インポート不要。

**② 中コスト: Quad を複数枚 X 字配置**  
同じシェーダーを貼った Quad を 2〜3 枚、少しずつ角度をずらして配置（X字・#字）。正面・斜めどちらからも見え、古典的だが効果的。

**③ 根本解決: 3D メッシュに直接貼る**  
紙が実際に薄い 3D メッシュであれば、そのまま UV にシェーダーを適用すると側面も自然に燃える。タイプライターの紙がすでに 3D オブジェクトの場合は最初からこちらが正解。

> **このプロジェクトでの推奨**: カメラアングルがほぼ固定（タイプライター俯瞰）なら Bloom + Emission 調整だけで十分。カメラが自由移動するなら③が必須。

---

### 4-2. 燃え方の不規則な「ちぎれ」

**現在の問題**: 単一スケールのノイズは「均一な砂嵐ディゾルブ」に見える。本物の紙は大きなチャンクがちぎれながら、端がボロボロになって細かく燃える。

**解決策: 3スケール・ノイズの重ね合わせ（Shader Graph の変更のみ・追加アセット不要）**

本物の紙の燃え方を3つのスケールに分解する：

| レイヤー | ノイズ種類 | NoiseScale 目安 | 意味 | 重み |
|---|---|---|---|---|
| 粗ノイズ (MacroCrack) | Voronoi | 3.0 (低密度) | 大きな塊がちぎれる | 40% |
| 中ノイズ (現状) | Voronoi | 15.0 | ギザギザの中間エッジ | 40% |
| 細ノイズ (Fraying) | Simple Noise | 22.5 | 繊維のほつれ・細かい焼け | 20% |

```
FinalNoise = MacroCrack × 0.4
           + MediumNoise × 0.4
           + FineNoise   × 0.2
           + UV.y × BurnDirection
```

粗ノイズの「大きな塊」が進行度に引っかかった瞬間にドンと大きなちぎれが生まれ、その端を細ノイズがほつれさせる。Shader Graph の変更のみで実現できる。

**代替案: プリベイクされた Burn Map テクスチャ**  
実際の燃えた紙をスキャンした or 手書きした白黒テクスチャを `NoiseTexture` プロパティとして追加し、プロシージャルノイズの代わりに使う。textures.com などで "paper burn" を検索すると素材が見つかる。アーティストが「どこからちぎれるか」を完全にコントロールできる。

---

### 4-3. 煙の表現

煙は視覚的な「質量感・重厚感」を加える。炎の華やかさに対して煙は重さを与えるため、体感クオリティへの寄与が大きい。

**実装: Particle System を1つ追加（約1時間）**

`RealisticPaperBurn.cs` に煙用フィールドを追加：
```csharp
[Header("煙")]
public ParticleSystem smokeParticles;
// StartBurning() 内で smokeParticles?.Play();
// 完了時に     smokeParticles?.Stop();
```

Particle System の設定値：

| 項目 | 値 |
|---|---|
| Start Color | ダークグレー (0.2, 0.2, 0.2, 0.8) → アルファ 0 でフェードアウト |
| Start Size | 0.05 〜 0.08（小さく出て膨らむ） |
| Size over Lifetime | 1.0 → 4.0（上昇するほど大きく広がる） |
| Start Speed | 0.05 〜 0.2（ゆっくり） |
| Start Lifetime | 2.0 〜 4.0 |
| Gravity Modifier | -0.05（わずかに上昇） |
| Noise / Strength | 0.3（煙らしいゆらぎ） |
| Noise / Frequency | 0.5 |
| Emission Rate | 8 〜 15 |
| Renderer Material | Particles/Standard Unlit、Rendering Mode: Fade |

---

### 4-4. 燃焼音

音響はしばしば視覚品質と同等か、それ以上にクオリティの印象を左右する。パチパチ・ジリジリという紙の燃焼音を追加するだけで完成度の体感が大きく変わる。

`RealisticPaperBurn.cs` に追加：
```csharp
[Header("燃焼音")]
public AudioClip burnSoundClip;
public float burnSoundVolume = 1f;

private AudioSource _audioSource;

// Awake() に追加
_audioSource = gameObject.AddComponent<AudioSource>();
_audioSource.loop = true;
_audioSource.playOnAwake = false;
_audioSource.spatialBlend = 1f; // 3D 音響（紙の位置から聞こえる）

// StartBurning() に追加
if (burnSoundClip != null) { _audioSource.clip = burnSoundClip; _audioSource.Play(); }

// 完了時に追加
_audioSource.Stop();
```

フリー素材例: freesound.org で "paper burning" "fire crackling" を検索。

---

## 5. クオリティロードマップ

| 施策 | 完成度目安 | 追加工数 | 優先度 |
|---|---|---|---|
| 基本実装（フェーズ1〜3） | 60〜70点 | — | — |
| + URP **Bloom** ポストプロセス有効化 | +10点 | 10分（設定変更のみ） | **最優先** |
| + 煙 Particle System 追加（4-3） | +5点 | 1時間 | 高 |
| + 燃焼音追加（4-4） | +5点 | 30分 | 高 |
| → **ここまでで 80点** | | **+約2時間** | |
| + 3スケール・ノイズでちぎれ感（4-2） | +5点 | 2〜3時間 | 中 |
| + 火の粉 Emitter を境界線に追従させる | +3点 | 1〜2時間 | 中 |
| → **ここまでで 85点** | | **+約4時間** | |
| + VFX Graph で炎リムを置き換え（4-1） | +3点 | 3〜4時間 | 低 |
| + Distortion（陽炎/Heat Haze）効果 | +2点 | 4〜6時間 | 低 |
| → **ここまでで 90点** | | **+約9時間** | |

> **Bloom が最優先な理由**: HDR の `FireColor` は Bloom なしだと「白っぽい明るいオレンジ」にしか見えず、炎の発光感が全く出ない。URP の Volume コンポーネントに Bloom を追加するだけで 10 分、かつ効果は最大。

---

## 6. 今後のタスクリスト (Next Steps)
- [ ] **Task 1**: `Assets/Shaders` に `RealisticBurn.shadergraph` を作成し、フェーズ1のノード構成を実装する。Blackboard の各プロパティに Reference name を明示設定すること。
- [ ] **Task 2**: 動作確認用のシーンを用意し、Quadオブジェクトを作成。新規マテリアルを適用し、Inspectorから各パラメータ（ノイズサイズ、色、`BurnDirection`、`NoiseScrollSpeed`）を調整する。
- [ ] **Task 3**: フェーズ2に基づく Particle System と Point Light をQuadの子要素として追加し、見た目を調整する。
- [ ] **Task 4**: `RealisticPaperBurn.cs` を作成してアタッチし、コンテキストメニューの「Test Burn」から、全ての演出が同期して動作することを確認する。
- [ ] **Task 5**: 完成したオブジェクト群を `Prefab` 化し、実際のUIまたはゲーム進行スクリプトから `StartBurning()` を呼び出せるように組み込む。
  > ⚠️ **統合時に別途要検討**: どのゲームオブジェクトが燃えるのか（タイプライターの紙？選択UI？）、`StartBurning()` を呼ぶタイミング（スキル選択後？ターン遷移開始時？）を統合フェーズで改めて設計すること。
