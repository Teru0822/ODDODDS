using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 各ゲーム（UFO/FALLBALL/PINBALL）の強化パネルに1つアタッチして使用します。
/// 3ページ分のページオブジェクトを管理し、矢印ボタンでページ切り替えを行います。
///
/// 【このオブジェクトのヒエラルキー構成例（UFOUpgradePanel）】
/// UFOUpgradePanel
///  ├─ Page1（GameObject）  ← 強化内容1ページ目
///  ├─ Page2（GameObject）  ← 強化内容2ページ目
///  ├─ Page3（GameObject）  ← 強化内容3ページ目
///  ├─ PrevButton（Button） ← ← 前へボタン
///  ├─ NextButton（Button） ← → 次へボタン
///  └─ PageLabel（TMP_Text）← 「1 / 3」表示
/// </summary>
public class UpgradePanelController : MonoBehaviour
{
    [Header("ページ")]
    [Tooltip("ページ順に格納してください（Page1, Page2, Page3）")]
    public GameObject[] pages;

    [Header("ページナビゲーション")]
    public Button prevButton;
    public Button nextButton;

    [Tooltip("「1 / 3」形式で表示するテキスト（TextMeshPro）")]
    public TMP_Text pageLabel;

    // 現在のページインデックス（0始まり）
    private int _currentPage = 0;

    void OnEnable()
    {
        // パネルが表示されるたびに1ページ目に戻す
        _currentPage = 0;
        RefreshView();
    }

    void Start()
    {
        prevButton?.onClick.AddListener(OnPrevPage);
        nextButton?.onClick.AddListener(OnNextPage);
    }

    /// <summary>「←」ボタン</summary>
    public void OnPrevPage()
    {
        if (_currentPage > 0)
        {
            _currentPage--;
            RefreshView();
        }
    }

    /// <summary>「→」ボタン</summary>
    public void OnNextPage()
    {
        if (_currentPage < pages.Length - 1)
        {
            _currentPage++;
            RefreshView();
        }
    }

    /// <summary>表示を現在ページに同期する</summary>
    private void RefreshView()
    {
        for (int i = 0; i < pages.Length; i++)
            pages[i]?.SetActive(i == _currentPage);

        if (pageLabel != null)
            pageLabel.text = $"{_currentPage + 1} / {pages.Length}";

        if (prevButton != null) prevButton.interactable = _currentPage > 0;
        if (nextButton != null) nextButton.interactable = _currentPage < pages.Length - 1;
    }
}
