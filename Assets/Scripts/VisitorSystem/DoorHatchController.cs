using DG.Tweening;
using UnityEngine;

/// <summary>
/// ドアの取引口ハッチを開閉するコンポーネント。
/// door_hatch オブジェクトにアタッチして使う。
/// IntroTourDirector の onShotEnter/onShotExit からも呼べる。
/// </summary>
public class DoorHatchController : MonoBehaviour
{
    [Tooltip("閉じた状態のローカル Z 座標")]
    [SerializeField] private float _closedZ = -0.0004f;

    [Tooltip("開いた状態のローカル Z 座標")]
    [SerializeField] private float _openZ = 0.0016f;

    [Tooltip("開閉にかかる時間（秒）")]
    [SerializeField] private float _duration = 1.0f;

    public void Open()
    {
        transform.DOLocalMoveZ(_openZ, _duration);
    }

    public void Close()
    {
        transform.DOLocalMoveZ(_closedZ, _duration);
    }
}
