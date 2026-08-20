using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// price_coin プレハブのUI要素を制御・保持するクラス。
/// </summary>
public class PriceCoinUI : MonoBehaviour
{
    [Header("UI References (事前アタッチ)")]
    [Tooltip("コインの画像UI")]
    [SerializeField] private Image _coinImage;

    [Tooltip("コインのテキストUI (TMP)")]
    [SerializeField] private TMP_Text _coinText;

    /// <summary>
    /// 画像UIオブジェクトへの参照
    /// </summary>
    public Image CoinImage => _coinImage;

    /// <summary>
    /// テキストUIオブジェクトへの参照
    /// </summary>
    public TMP_Text CoinText => _coinText;

    /// <summary>
    /// テキストの内容を設定します
    /// </summary>
    public void SetText(string text)
    {
        if (_coinText != null)
        {
            _coinText.text = text;
        }
    }

    /// <summary>
    /// コイン画像を設定します
    /// </summary>
    public void SetImage(Sprite sprite)
    {
        if (_coinImage != null && sprite != null)
        {
            _coinImage.sprite = sprite;
        }
    }
}
