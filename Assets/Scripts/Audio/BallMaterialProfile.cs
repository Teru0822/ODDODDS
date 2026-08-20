using UnityEngine;

/// <summary>
/// ボール素材プロファイル。Metal / Wood / Glass 等それぞれに 1 つ作る。
/// SurfaceProfile の clip 配列にアクセスする際の index 名前空間として機能する。
///
/// 例: Metal の index = 0 なら、SurfaceProfile.impactClips[0] が
///     「金属球がこの面に当たった時の音」を意味する。
/// </summary>
[CreateAssetMenu(menuName = "OddOdds/Audio/Ball Material Profile", fileName = "BallMat_New")]
public class BallMaterialProfile : ScriptableObject
{
    [Tooltip("SurfaceProfile の配列における index。Metal=0, Wood=1, Glass=2 等。\nボール素材を追加する時はここを採番し、全 SurfaceProfile の配列サイズを揃える")]
    [Min(0)]
    public int index = 0;

    [Tooltip("デバッグ識別用 (Metal / Wood / Glass 等)")]
    public string id;
}
