# プラン: タイプライター UIボタン演出 + スキル確定エフェクト追加

## Context

スキル選択UIのボタンに一切のアニメーション・音がなく（ホバー時スプライト差し替えのみ）、スキル確定時も即座にUIが消えるだけで達成感がない。
タイプライターという「物理的なキーを押し込む」道具のコンセプトに沿って、ホバー・押下・確定の3段階に演出を加え、スキル取得の瞬間を気持ちの良い体験にする。

---

## 演出フロー全体（完成後のタイムライン）

```
マウスが乗る    → 効果音 + スプライト変化（既存）
                  + ボタンが OutBack でスッと 1.05 倍に拡大（NEW）

クリック押下    → ボタンが即座にキュッと押し込まれる (DOPunchScale 負値, 0.1s)（NEW）
                  ※ タイプライターキーの物理的な押し込みを再現

確定 (onClick)  → ① 確定音（ベル音）を即再生（NEW）
                  ② 選択ボタンが金色に一瞬フラッシュ（0.05s）（NEW）
                  ③ UI全体がわずかに揺れる（DOShakePosition, 0.12s）（NEW）
                  ④ 0.15s 後に Hide → callback
                     → タイプライターが打鍵開始（既存）

紙ローンチ Phase3 → whoosh音（NEW）
```

---

## 変更ファイル（3ファイル）

| ファイル | 変更内容 |
|---|---|
| `Assets/Scripts/Typewriter/ButtonHover.cs` | `IPointerDownHandler` 追加、ホバー/押下スケールアニメ追加 |
| `Assets/Scripts/Typewriter/RewardSelectionUI.cs` | 押下音コールバック追加、確定エフェクト（音+フラッシュ+シェイク+遅延Hide）|
| `Assets/Scripts/Typewriter/TypewriterPaperOutput.cs` | ローンチwhoosh音追加 |

---

## 1. ButtonHover.cs

### 追加する using
```csharp
using DG.Tweening;
```

### 実装変更

```csharp
// 変更前: IPointerEnterHandler, IPointerExitHandler のみ
// 変更後: IPointerDownHandler を追加

public class ButtonHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    private Vector3 _originalScale;

    private void Awake()
    {
        _originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // --- 既存処理（変更なし）---
        _rewardSelectionUI.OnSkillButtonHover(_rewardIndex);
        _rewardSelectionUI.SetExplainText(data.skillDescription);
        _rewardSelectionUI.ShowPreview(data);
        // --- 追加: ホバースケールアップ ---
        transform.DOKill();
        transform.DOScale(_originalScale * 1.05f, 0.1f).SetEase(Ease.OutBack);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // --- 既存処理（変更なし）---
        _rewardSelectionUI?.OnSkillButtonExit(_rewardIndex);
        // --- 追加: スケールを戻す ---
        transform.DOKill();
        transform.DOScale(_originalScale, 0.08f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // キーを押し込む感覚: 負値の PunchScale で瞬時に縮んでバネで戻る
        transform.DOKill();
        transform.DOPunchScale(Vector3.one * -0.08f, 0.12f, 4, 0.5f);
        // 押下音をRewardSelectionUIに通知
        _rewardSelectionUI?.OnSkillButtonPress(_rewardIndex);
    }
}
```

**ポイント:**
- `DOKill()` を各ハンドラの先頭で呼び、前のアニメーションを即停止してから新しいアニメーションを開始する（素早くマウスを動かしたときのスケール崩れを防止）
- `OnPointerDown` は `Button.onClick`（PointerUp）より先に発火するため、「押し込み」演出が視覚的に確実に見える

---

## 2. RewardSelectionUI.cs

### 追加フィールド（`[Header("サウンド")]` ブロックの直下）

```csharp
[Tooltip("スキルボタン押下瞬間の効果音（ホバー音とは別の、より鋭いキー音）")]
[SerializeField] private AudioClip _skillPressClip;
[SerializeField] private float _skillPressVolume = 1f;

[Tooltip("スキル確定時の効果音（タイプライターのカーリッジリターンベル音）")]
[SerializeField] private AudioClip _skillConfirmClip;
[SerializeField] private float _skillConfirmVolume = 1f;

[Header("確定エフェクト")]
[Tooltip("確定時に選択ボタンが光るフラッシュカラー")]
[SerializeField] private Color _confirmFlashColor = new Color(1f, 0.85f, 0.3f); // 琥珀色
[Tooltip("確定時のUIシェイク強度")]
[SerializeField] private float _confirmShakeStrength = 6f;
[Tooltip("確定時のUIシェイク時間（秒）。この後 Hide が呼ばれる。")]
[SerializeField] private float _confirmShakeDuration = 0.12f;
```

### 追加メソッド: `OnSkillButtonPress()`
`ButtonHover.OnPointerDown` から呼ばれる。押下音を再生するだけ。

```csharp
/// <summary>ボタン押下瞬間（ButtonHover.OnPointerDown から呼ばれる）</summary>
public void OnSkillButtonPress(int index)
{
    if (_skillPressClip != null && _uiAudioSource != null)
        _uiAudioSource.PlayOneShot(_skillPressClip, _skillPressVolume);
}
```

### `OnOptionClicked(int index)` の変更

```csharp
private void OnOptionClicked(int index)
{
    if (_currentOptions == null || index < 0 || index >= _currentOptions.Count)
    {
        Debug.LogWarning(...);
        return;
    }
    SetExplainText("");
    RoguelikeData picked = _currentOptions[index];

    // ① 確定音（即再生）
    if (_skillConfirmClip != null && _uiAudioSource != null)
        _uiAudioSource.PlayOneShot(_skillConfirmClip, _skillConfirmVolume);

    // ② 選択ボタンのフラッシュ（金色に一瞬光る）
    if (index < _dynButtons.Count)
    {
        var img = _dynButtons[index].GetComponent<Image>();
        if (img != null)
            img.DOColor(_confirmFlashColor, 0.05f).SetLoops(2, LoopType.Yoyo);
    }

    // ③ UI全体シェイク → 完了後に Hide + callback
    var cb = _onSelected;
    _onSelected = null;
    var root = uiRoot != null ? uiRoot.transform : transform;
    root.DOShakePosition(_confirmShakeDuration, _confirmShakeStrength, 12, 90f, false, true)
        .OnComplete(() => { Hide(); cb?.Invoke(picked); });
}
```

**ポイント:**
- `_onSelected = null` を先行してセットし、0.12s の間に二重呼び出しされても安全
- フラッシュ（0.05s × 2）とシェイク（0.12s）は並行して走る
- `DOShakePosition` 完了後に `Hide()` → callback の順で呼ぶことで、エフェクトが確実に見える

---

## 3. TypewriterPaperOutput.cs

### 追加フィールド（`[Header("打鍵完了時のローンチ演出")]` ブロック直下）

```csharp
[Header("ローンチ効果音")]
[SerializeField] private AudioClip _launchWhooshClip;
[SerializeField, Range(0f, 2f)] private float _launchWhooshVolume = 1f;
[SerializeField] private AudioSource _launchAudioSource;
```

### `Awake()` を追加（既存の `Start` がなければ追加）

```csharp
private void Awake()
{
    if (_launchAudioSource == null)
    {
        _launchAudioSource = GetComponent<AudioSource>()
            ?? gameObject.AddComponent<AudioSource>();
        _launchAudioSource.playOnAwake = false;
        _launchAudioSource.spatialBlend = 0f;
    }
}
```

### `LaunchSequence()` Phase 3 直前に挿入

```csharp
// Phase 3 開始直前
if (_launchWhooshClip != null)
    _launchAudioSource.PlayOneShot(_launchWhooshClip, _launchWhooshVolume);

float launchElapsed = 0f;
// ...既存の while ループ
```

---

## 必要な音声アセットと Inspector 設定

| クリップ | Inspector 設定先 | 推奨素材 |
|---|---|---|
| ホバー音（既存） | `RewardSelectionUI._uiKeySoundClips` | 既存アサイン |
| 押下音（NEW） | `RewardSelectionUI._skillPressClip` | ホバー音より鋭いキー音 (.wav)。ホバー音と同じクリップでも可 |
| 確定ベル音（NEW） | `RewardSelectionUI._skillConfirmClip` | freesound.org "typewriter bell" |
| ローンチwhoosh（NEW） | `TypewriterPaperOutput._launchWhooshClip` | **既存** `Assets/Audio/SE/Title/mixkit-fast-rocket-whoosh-1714.wav` |

---

## 検証方法

1. タイトルシーンからスタート、タイプライターをクリックしてUIを開く
2. ボタンにマウスを乗せる → スプライト変化 + 効果音 + **ボタンが 1.05 倍にスッと拡大**することを確認
3. ボタンをクリック（押下） → **ボタンがキュッと小さく**なることを確認（押し込み感）
4. クリック確定 → **ベル音 + ボタンが金色にフラッシュ + UI全体が軽く揺れる**ことを確認
5. 0.15s 後に UI が消え、タイプライターが打鍵開始することを確認
6. 紙が回転 → ため → **発射と同時に whoosh 音**が鳴ることを確認
7. 連続でスキル選択を試み、二重確定が起きないことを確認（`_onSelected = null` の効果）
