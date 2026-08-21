# ローディング画面の Prefab 化と柔軟な API 設計

**ブランチ:** `feature/loading-screen-prefab`  
**担当:** Claude + ユーザー（Unity Editor 作業）

---

## Context

現在のローディング画面は `3D_Title_Sample.unity` に置かれた `TransitionCanvas` に強く依存しており、
シーン遷移の際に「タイトルシーンのカメラを DontDestroyOnLoad で持ち回る」という複雑な仕組みを使っている。
その結果、ゲームシーン→タイトルシーン戻りでライトがおかしくなるなど不具合が頻発し、
タイトル以外のシーンや新しいタイミングでロード画面を挟もうとすると実装コストが非常に高い。

**目的:** ローディング画面を Prefab + 専用マネージャーに切り出し、
どのシーン・どのタイミングからでも同じ見た目のロード画面を手軽に呼び出せるようにする。
タイトルシーンへの依存を完全に排除し、表示時間・終了条件を呼び出し元ごとに自由に指定できる API を提供する。

---

## 現状の問題点

| 問題 | 原因 |
|------|------|
| タイトルシーン依存 | TransitionCanvas が 3D_Title_Sample.unity に固定 |
| カメラ保護の複雑さ | シーン遷移のたびに Camera.main を DDOL 化→Y=-500 にワープ→遷移後に破棄 |
| ライト管理の脆弱性 | SetParent タイミングと DOScale が競合、ワールド座標が崩壊しやすい |
| 環境光キャッシュが初回依存 | タイトルを経由しないと `AmbientMode.Flat` フォールバック |
| 別シーンから呼べない | 新規シーンで呼ぼうとすると TransitionCanvas が存在せずクラッシュ |

---

## 新しい設計方針

### URP Overlay Camera + 専用レイヤーで完全隔離 + 自動セットアップ

本プロジェクトは **URP（Universal Render Pipeline）** を使用しているため、
カメラ合成は URP の **Camera Stacking（Base + Overlay）** 方式で行う。

```
LoadingScreenRoot [DontDestroyOnLoad]
├── LoadingCamera
│     Render Type = Overlay（URP）
│     Culling Mask = LoadingScreen レイヤーのみ
│     ClearFlags = Depth
│     位置 Y = -500
├── TransitionCanvas  (ScreenSpaceCamera → LoadingCamera, SortOrder=999)
│   ├── RootBlocker  (透明 Image, RaycastTarget=true)  ← クリック貫通防止
│   ├── FadeGroup  (CanvasGroup + 全画面黒 Image)
│   ├── LoadingGroup  (CanvasGroup)
│   │   ├── Background  (RawImage)
│   │   └── LoadingText  (TextMeshProUGUI "Loading...")
│   └── LogoObject  [Layer: LoadingScreen]
└── LogoLight  [Layer: LoadingScreen, SpotLight または PointLight]
```

#### URP Camera Stacking の仕組み

- ゲームシーンの Base Camera のスタックに LoadingCamera（Overlay）を動的に追加・削除する
- `SceneManager.sceneLoaded` フックで新しいシーンのカメラにも自動追加される

#### Culling Mask の自動除外

手動設定では将来のカメラ追加時に外し忘れが起きるため、スクリプトから自動除外する。
`Awake()` と `SceneManager.sceneLoaded` のタイミングで全カメラの Culling Mask から `LoadingScreen` レイヤーを除外する。

#### クリック貫通防止

- Canvas Sort Order = 999（ゲーム UI より常に前面）
- `RootBlocker`（透明 Image, RaycastTarget=true）でフェード中を含めて入力をブロック

#### LogoLight の制約

- DirectionalLight は全シーンに影響するため **使用禁止**
- SpotLight または PointLight を使う（Y=-500 付近の狭い範囲のみ照らす）

#### Lazy Initialization（シーン配置不要）

`Instance` プロパティが呼ばれた際に `Resources.Load` から自動 Instantiate する。
各シーンへの手動配置不要。`Assets/Resources/LoadingScreen.prefab` に置くだけ。

---

## 公開 API

```csharp
// A. シーン遷移
LoadingScreenManager.Instance.TransitionToScene(
    string sceneName,
    float minimumDuration = 2f,
    float fadeDuration = 1.5f,
    Action onComplete = null);

// B. 条件が満たされるまで表示
LoadingScreenManager.Instance.ShowUntil(
    Func<bool> condition,
    float minimumDuration = 0f,
    float postCompletionDelay = 0f,   // 条件クリア後も追加で表示する秒数
    float fadeDuration = 1.5f,
    Action onComplete = null);

// C. コルーチンが終わるまで表示
LoadingScreenManager.Instance.ShowWhile(
    IEnumerator task,
    float minimumDuration = 1f,
    float postCompletionDelay = 0f,
    float fadeDuration = 1.5f,
    Action onComplete = null);

// D. アクションを実行しながら表示（ShowTurnTransition の置き換え）
LoadingScreenManager.Instance.ShowWhile(
    Action onDuringLoading,
    float minimumDuration = 1f,
    float postCompletionDelay = 0f,
    float fadeDuration = 1.5f,
    Action onComplete = null);

// E. 手動制御
LoadingScreenManager.Instance.Show(float fadeDuration = 1.5f);
LoadingScreenManager.Instance.Hide(float fadeDuration = 1.5f);
```

---

## 変更ファイル一覧

| ファイル | 種別 | 作業者 |
|---------|------|--------|
| `Assets/Scripts/System/LoadingScreenManager.cs` | **新規作成** | Claude |
| `Assets/Resources/LoadingScreen.prefab` | **新規作成** | ユーザー（Unity Editor） |
| `Assets/Scripts/System/SceneTransitionManager.cs` | **大幅リファクタリング** | Claude |
| `Assets/Scenes/3D_Title_Sample.unity` | TransitionCanvas 削除 | ユーザー（Unity Editor） |
| Project Settings → Tags and Layers | 「LoadingScreen」レイヤー追加 | ユーザー（Unity Editor） |

※ 各カメラの Culling Mask 手動変更は **不要**（スクリプトが自動で行う）

---

## 既存の呼び出し元への影響

| ファイル | 変更必要か | 理由 |
|---------|-----------|------|
| `TitlePlayButton.cs` | 不要 | SceneTransitionManager.TransitionToScene の API 維持 |
| `SettingUIManager.cs` | 不要 | 同上 |
| `DebtCollectionManager.cs` | 任意 | ShowTurnTransition API 維持、新 API への移行も可 |
| `TypewriterInteractable.cs` | 任意 | 同上 |
| `TutorialCraneController.cs` | 任意 | 同上 |

---

## Phase 2（将来の改善）

`SceneTransitionManager` に残る `MultiSceneLoader` 待機・`RoguelikeSaveManager.Load` は
ゲーム固有の処理であり、本来は汎用の TransitionManager の責務ではない。
将来的に `GameModeManager`（仮）に切り出すことで完全にゲームロジック非依存にできる。
今回は互換性維持を優先してスコープ外とする。

---

## 作業量の見積もり

| 作業 | 担当 | 見積もり時間 |
|------|------|------------|
| `LoadingScreenManager.cs` 新規実装（URP対応含む） | Claude | 4〜5 時間 |
| `SceneTransitionManager.cs` リファクタリング | Claude | 2〜3 時間 |
| デバッグ・調整 | Claude | 1〜2 時間 |
| `LoadingScreen.prefab` 作成・設定 | ユーザー | 1〜2 時間 |
| `3D_Title_Sample.unity` 修正 | ユーザー | 30 分 |
| Project Settings レイヤー追加 | ユーザー | 10 分 |
| 動作確認・微調整 | 両方 | 1〜2 時間 |
| **合計** | | **10〜15 時間** |

---

## 検証方法

1. タイトル → ゲームシーン遷移でロード画面が正しく表示・消去される
2. ゲームシーン → タイトル遷移でロード画面が正しく表示・消去される
3. ShowTurnTransition 呼び出し箇所（DebtCollection, Typewriter, Tutorial）で表示される
4. `ShowUntil(condition)` で条件クリア待機 + postCompletionDelay 継続が機能する
5. ロード画面表示中にゲームオブジェクトが一切映り込まない
6. ロード画面表示中（フェード中含む）にゲーム内ボタンが反応しない
7. 新しいシーンをロードした後、新カメラが LogoObject を映さない
8. 連続遷移しても TransitionCanvas が重複せず一つだけ表示される
9. ゲーム→タイトル→ゲームを繰り返しても LogoLight がゲーム側に影響しない
