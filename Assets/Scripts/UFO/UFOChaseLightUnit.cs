using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 10種類の機体（bottom_lump_left, PatoLamp_bottom_left 等）のprefabそれぞれに1回だけアタッチする、
/// 自己申告型のライトユニット。「自分がどの位置か」をInspectorで選ぶだけでよく、
/// 同じprefabが何セット（何個）シーンに置かれていても、UFOChaseLightController側が実行時に
/// 自動的に全インスタンスを見つけてグループ化するため、インスタンスごとの手動配線は不要。
/// </summary>
public class UFOChaseLightUnit : MonoBehaviour
{
    public enum Position
    {
        BottomLumpLeft,
        BottomLumpRight,
        MiddleLumpLeft,
        MiddleLumpRight,
        TopLumpLeft,
        TopLumpRight,
        PatoLampBottomLeft,
        PatoLampBottomRight,
        PatoLampTopLeft,
        PatoLampTopRight,
    }

    [Tooltip("このprefabが10種類のうちどれに該当するかを選ぶ（prefab側で1回設定すれば、全インスタンスに反映される）")]
    [SerializeField] private Position position;

    private Light[] _lights;
    private Renderer[] _renderers;

    public Position Kind => position;

    public IReadOnlyList<Light> Lights
    {
        get
        {
            if (_lights == null) _lights = GetComponentsInChildren<Light>(true);
            return _lights;
        }
    }

    /// <summary>電球のガラス部分等、Emissionでマテリアルも一緒に光らせたい場合に使う</summary>
    public IReadOnlyList<Renderer> Renderers
    {
        get
        {
            if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
            return _renderers;
        }
    }
}
