using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// ローグライク強化UIの全体管理スクリプト。
/// Canvas直下のオブジェクトにアタッチして使用します。
///
/// 【UIヒエラルキー構成】
/// Canvas
///  └─ UpgradeRoot（このスクリプトをアタッチ）
///      ├─ UpgradeButton（Button）       ← 左下の「強化要素」ボタン
///      ├─ CloseButton（Button）         ← 「閉じる」ボタン（常時表示、パネル表示中のみ有効）
///      ├─ GameSelectPanel               ← ゲーム選択パネル
///      │    ├─ UFOButton（Button）      → OnGameSelected(0)
///      │    ├─ FallballButton（Button） → OnGameSelected(1)
///      │    └─ PinballButton（Button）  → OnGameSelected(2)
///      ├─ UFOUpgradePanel               ← UpgradePanelController をアタッチ
///      │    ├─ Page1
///      │    ├─ Page2
///      │    ├─ Page3
///      │    ├─ PrevButton
///      │    ├─ NextButton
///      │    └─ PageLabel (TMP_Text)
///      ├─ FallballUpgradePanel          ← UpgradePanelController をアタッチ
///      └─ PinballUpgradePanel           ← UpgradePanelController をアタッチ
/// </summary>
public class UpgradeUIManager : MonoBehaviour
{
    [Header("メインボタン")]
    [Tooltip("左下に置く「強化要素」ボタン")]
    public Button upgradeButton;

    [Header("閉じるボタン")]
    [Tooltip("常時Canvasに表示しておく「閉じる」ボタン。パネルが開いているときのみ有効にします。")]
    public Button closeButton;

    [Header("ゲーム選択パネル")]
    [Tooltip("UFO / FALLBALL / PINBALL を選ぶパネル")]
    public GameObject gameSelectPanel;

    [Header("各ゲームの強化パネル")]
    [Tooltip("ミニゲームの強化パネルリスト。追加するたびここに1件追加するだけでOK！")]
    public List<GameUpgradeEntry> gameUpgradeEntries = new List<GameUpgradeEntry>();

    // 現在開いている強化パネル
    private GameObject _currentOpenPanel = null;
    private bool _isAnyPanelOpen = false;

    void Start()
    {
        // 最初は全部非表示
        gameSelectPanel?.SetActive(false);
        foreach (var entry in gameUpgradeEntries)
            entry.upgradePanel?.SetActive(false);

        // CloseボタンはUI上に残すが最初は非活性
        SetCloseButtonVisible(false);

        // CloseButtonをヒエラルキーの最後尾に移動 → 常に最前面に描画される
        closeButton?.transform.SetAsLastSibling();

        // イベント登録
        upgradeButton?.onClick.AddListener(OnUpgradeButtonClicked);
        closeButton?.onClick.AddListener(CloseAll);
    }

    // ─────────────────────────────────────────
    // 公開メソッド（Unityインスペクターから呼べる）
    // ─────────────────────────────────────────

    /// <summary>「強化要素」ボタンが押された</summary>
    public void OnUpgradeButtonClicked()
    {
        CloseCurrentUpgradePanel();
        gameSelectPanel?.SetActive(true);
        _isAnyPanelOpen = true;

        // UPGRADEボタンを隠し、CLOSEボタンを表示
        upgradeButton?.gameObject.SetActive(false);
        SetCloseButtonVisible(true);
    }

    /// <summary>
    /// ゲーム選択パネルのボタンから呼ぶ。
    /// インスペクターで OnClick() → OnGameSelected(0) のように登録してください。
    ///   0 = UFO CATCHER
    ///   1 = FALLBALL
    ///   2 = PINBALL
    /// </summary>
    public void OnGameSelected(int index)
    {
        if (index < 0 || index >= gameUpgradeEntries.Count) return;

        // ゲーム選択パネルを閉じる
        gameSelectPanel?.SetActive(false);

        // 前の強化パネルを閉じて新しいものを開く
        CloseCurrentUpgradePanel();
        _currentOpenPanel = gameUpgradeEntries[index].upgradePanel;
        _currentOpenPanel?.SetActive(true);

        _isAnyPanelOpen = true;
        SetCloseButtonVisible(true);
    }

    /// <summary>
    /// 全パネルを閉じる。
    /// ✅ CloseButton の OnClick() にこのメソッドを登録してください。
    /// </summary>
    public void CloseAll()
    {
        CloseCurrentUpgradePanel();
        gameSelectPanel?.SetActive(false);
        _isAnyPanelOpen = false;

        // UPGRADEボタンを戻し、CLOSEボタンを隠す
        upgradeButton?.gameObject.SetActive(true);
        SetCloseButtonVisible(false);
    }

    // ─────────────────────────────────────────
    // 内部ヘルパー
    // ─────────────────────────────────────────

    private void CloseCurrentUpgradePanel()
    {
        _currentOpenPanel?.SetActive(false);
        _currentOpenPanel = null;
    }

    /// <summary>
    /// CloseボタンのGameObjectは常に表示したまま、
    /// interactable（操作可否）と透明度でオン/オフを切り替えます。
    /// </summary>
    private void SetCloseButtonVisible(bool visible)
    {
        if (closeButton == null) return;
        closeButton.interactable = visible;

        // CanvasGroupがあれば透明度でフェード、なければそのまま
        var cg = closeButton.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = visible ? 1f : 0f;
        }
    }
}

/// <summary>
/// ゲーム1種類分のエントリー。
/// 新しいミニゲームを追加するときは、このリストに1件追加するだけでOKです！
/// </summary>
[System.Serializable]
public class GameUpgradeEntry
{
    [Tooltip("ゲームの表示名（デバッグ用）")]
    public string gameName;

    [Tooltip("このゲームの強化パネル（UpgradePanelController をアタッチ済みのもの）")]
    public GameObject upgradePanel;
}
