using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ListItem プレハブのUI要素を制御・保持するクラス。
/// </summary>
public class ListItemUI : MonoBehaviour
{
    [Header("UI References (事前アタッチ)")]
    [Tooltip("ListItemのメイン画像UI")]
    [SerializeField] private Image _itemImage;

    [Tooltip("Row_1 のテキストUI (TMP)")]
    [SerializeField] private TMP_Text _row1Text;

    [Tooltip("Row_2 のテキストUI (TMP)")]
    [SerializeField] private TMP_Text _row2Text;

    [Tooltip("Row_3 のテキストUI (TMP)")]
    [SerializeField] private TMP_Text _row3Text;

    /// <summary>
    /// メイン画像UIオブジェクトへの参照
    /// </summary>
    public Image ItemImage => _itemImage;

    /// <summary>
    /// Row_1 のテキストUIオブジェクトへの参照
    /// </summary>
    public TMP_Text Row1Text => _row1Text;

    /// <summary>
    /// Row_2 のテキストUIオブジェクトへの参照
    /// </summary>
    public TMP_Text Row2Text => _row2Text;

    /// <summary>
    /// Row_3 のテキストUIオブジェクトへの参照
    /// </summary>
    public TMP_Text Row3Text => _row3Text;

    /// <summary>
    /// 3つのテキストを一括設定します
    /// </summary>
    public void SetTexts(string row1, string row2, string row3)
    {
        if (_row1Text != null) _row1Text.text = row1;
        if (_row2Text != null) _row2Text.text = row2;
        if (_row3Text != null) _row3Text.text = row3;
    }

    /// <summary>
    /// メイン画像を設定します
    /// </summary>
    public void SetImage(Sprite sprite)
    {
        if (_itemImage != null && sprite != null)
        {
            _itemImage.sprite = sprite;
        }
    }
}
