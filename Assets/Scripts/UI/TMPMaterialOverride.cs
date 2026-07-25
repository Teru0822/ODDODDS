using TMPro;
using UnityEngine;

/// <summary>
/// TextMeshProUGUI に個別マテリアルを適用するコンポーネント。
/// 同じフォントを使う他のテキストに影響を与えずに、このオブジェクト専用の見た目を設定できる。
/// Edit モード・Play モード両方でリアルタイムにプレビュー可能。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
[DisallowMultipleComponent]
public class TMPMaterialOverride : MonoBehaviour
{
    [Tooltip("このテキストに適用するマテリアル。複製したマテリアルをここにアサインする。")]
    [SerializeField] private Material _material;

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        // Inspector で値を変えるたびに即座に反映（Edit / Play 両対応）
        Apply();
    }

    private void Apply()
    {
        if (_material == null) return;
        var tmp = GetComponent<TMP_Text>();
        if (tmp == null) return;
        tmp.fontMaterial = _material;
    }
}
