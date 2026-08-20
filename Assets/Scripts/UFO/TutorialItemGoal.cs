using System.Collections;
using UnityEngine;

/// <summary>
/// チュートリアル用の落とし口（練習Devilキャッチャー用）。
/// 実機の DevilItemGoal とは完全に分離しており、所持金・アイテム所持数・実機のランプ（タグ検索で
/// シーン全体のInsertableItemを操作する仕組み）など、実機の永続データ/共有オブジェクトには
/// 一切触れない。入ってきたアイテムは、種別ごとの効果音・ランプ演出のあと見た目上消えるだけ。
/// </summary>
public class TutorialItemGoal : MonoBehaviour
{
    [Header("ランプ演出（チュートリアル専用のLightのみを対象にする。実機のランプには一切触れない）")]
    [Tooltip("アイテム獲得時に点灯させるLight（任意・複数可）")]
    [SerializeField] private Light[] flashLights;

    [Tooltip("通常アイテム（コイン等）の点灯色")]
    [SerializeField] private Color coinFlashColor = Color.white;

    [Tooltip("時計獲得時の点灯色")]
    [SerializeField] private Color watchFlashColor = new Color(1f, 0.9f, 0.3f);

    [Tooltip("ブラックダイヤ獲得時の点灯色")]
    [SerializeField] private Color blackDiamondFlashColor = new Color(0.8f, 0f, 1f);

    [Tooltip("ランプの点灯を維持する時間（秒）")]
    [SerializeField, Min(0.05f)] private float flashDuration = 1.0f;

    [Header("時計効果（実機のDevilItemGoalと同じ挙動）")]
    [Tooltip("このTutorialItemGoalが属する練習DevilキャッチャーのTutorialCraneController")]
    [SerializeField] private TutorialCraneController tutorialCrane;

    [Tooltip("時計を落とし口に入れたときに残り時間を何秒延長するか")]
    [SerializeField] private float watchTimeExtension = 20f;

    /// <summary>アイテムが落とし口に入った瞬間に発火する（チュートリアルステップの進行検知等に使う）</summary>
    public event System.Action<UFOItemType> OnItemDropped;

    /// <summary>時計・ブラックダイヤ等の獲得ランプ演出中かどうか（練習機用DevilChaseLightControllerが参照する）</summary>
    public bool IsFlashing { get; private set; }

    /// <summary>IsFlashing中に使っている色（練習機用DevilChaseLightControllerが、チェイスライト側の
    /// 物理ランプをこの色で光らせるために参照する）</summary>
    public Color CurrentFlashColor { get; private set; }

    /// <summary>
    /// IsFlashingを強制的にfalseへ戻す（新しい練習セッション開始時に呼ぶ）。演出中にGameObjectが
    /// 破棄される等でFlashLightsRoutineが中断されると、IsFlashingがtrueのまま固まる可能性があるため、
    /// セッション開始時に必ず呼んで持ち越されないようにする
    /// </summary>
    public void ResetStuckFlashState()
    {
        IsFlashing = false;
    }

    private Coroutine _flashCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        HandleItemDrop(other);
    }

    /// <summary>
    /// アイテムが入ったときの実際の処理。複数の穴（小・中・大）がある場合は、
    /// それぞれの穴の判定用CubeにTutorialItemGoalTriggerを付けて、こちらに転送してもらう
    /// （実機のDevilItemGoal.HandleItemDropと同じ構造）。
    /// </summary>
    public void HandleItemDrop(Collider other)
    {
        // 複数パーツ構成のモデルにも対応するため、親方向も辿って UFOItem を探す
        UFOItem item = other.GetComponentInParent<UFOItem>();
        if (item == null) return;

        // 複数の穴の判定用トリガーが重なっていると、同じアイテムに対して同じフレーム内で
        // HandleItemDropが複数回呼ばれることがある（Destroy()は次フレームまで実際には効かないため）。
        // 既に処理済みなら二重の獲得音・演出を防ぐためここで弾く
        if (item.IsProcessedForGoal) return;
        item.IsProcessedForGoal = true;

        // チュートリアルをプレイしていない間（Yes/No待ちの間に物理的に転がり込んだ等）に
        // 何か入っても、効果音・ランプ演出・時間延長などは一切発生させない（未プレイ判定）。
        // ただし入ったアイテム自体は消しておかないと機体内に残り続けるため、破棄だけは行う。
        // tutorialCrane未設定の場合は判定できないため、従来通り処理する（安全側に倒す）
        if (tutorialCrane != null && !tutorialCrane.IsPlayingTutorial)
        {
            Destroy(item.gameObject);
            return;
        }

        switch (item.itemType)
        {
            case UFOItemType.Watch:
                DevilSEManager.Instance?.PlayPracticeWatchGet();
                FlashLights(watchFlashColor, isGetEffect: true);
                tutorialCrane?.AddPlayTime(watchTimeExtension);
                break;
            case UFOItemType.BlackDiamond:
                DevilSEManager.Instance?.PlayPracticeBlackDiamondGet();
                FlashLights(blackDiamondFlashColor, isGetEffect: true);
                break;
            default:
                DevilSEManager.Instance?.PlayPracticeCoinGet();
                FlashLights(coinFlashColor, isGetEffect: false);
                // 練習機のチェイスライトも、実機の銅貨・銀貨・金貨獲得時と同じ一瞬フラッシュを行う
                DevilChaseLightController.TriggerCoinCatchFlash();
                break;
        }

        OnItemDropped?.Invoke(item.itemType);

        // 左下の3D回転ポップアップに、今取得したアイテムを表示する（実機のDevilItemGoalと同じ）
        if (DevilItemPickupDisplay.Instance != null)
        {
            DevilItemPickupDisplay.Instance.ShowPickedItem(item.gameObject);
        }

        Destroy(item.gameObject);
    }

    /// <summary>
    /// isGetEffect: true の場合、時計/ブラックダイヤ獲得演出として IsFlashing を演出中のみ true にする
    /// （練習機用DevilChaseLightControllerが、この間はチェイス演出を止めて待機する判定に使う）
    /// </summary>
    private void FlashLights(Color color, bool isGetEffect)
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;

            // 前の演出が isGetEffect:true（時計/ブラックダイヤ獲得）の途中だった場合、
            // StopCoroutineはFlashLightsRoutine末尾のIsFlashing=falseまで実行しないため、
            // ここでリセットしないとIsFlashingがtrueのまま固まってしまう
            // （練習機のパトランプが警告点滅に切り替われなくなるバグの原因だった）
            IsFlashing = false;
        }
        _flashCoroutine = StartCoroutine(FlashLightsRoutine(color, isGetEffect));
    }

    private IEnumerator FlashLightsRoutine(Color color, bool isGetEffect)
    {
        if (isGetEffect)
        {
            CurrentFlashColor = color;
            IsFlashing = true;
        }

        if (flashLights != null)
        {
            foreach (var light in flashLights)
            {
                if (light == null) continue;
                light.color = color;
                light.enabled = true;
            }
        }

        yield return new WaitForSeconds(flashDuration);

        if (flashLights != null)
        {
            foreach (var light in flashLights)
            {
                if (light != null) light.enabled = false;
            }
        }

        if (isGetEffect) IsFlashing = false;
    }
}
