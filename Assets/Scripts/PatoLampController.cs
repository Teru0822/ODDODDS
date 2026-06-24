using UnityEngine;

/// <summary>
/// UFOキャッチャーの残り時間が10秒以下になった際に、
/// パトランプを光らせて回転（Y軸）させるコントローラー。
/// </summary>
public class PatoLampController : MonoBehaviour
{
    [Tooltip("回転速度 (度/秒)")]
    [SerializeField] private float rotationSpeed = 360f;

    private Transform _patoinTransform;
    private Light[] _lights;
    private bool _isActive = false;

    private void Start()
    {
        // 回転する内側パーツ (patoin) を取得
        _patoinTransform = transform.Find("patoin");

        // patoin 配下、または自身配下のすべての Light コンポーネントを取得
        if (_patoinTransform != null)
        {
            _lights = _patoinTransform.GetComponentsInChildren<Light>(true);
        }
        else
        {
            _lights = GetComponentsInChildren<Light>(true);
        }

        // 初期状態ではパトランプを消灯（光らないように）しておく
        SetLightsEnabled(false);
    }

    private void Update()
    {
        bool shouldBeActive = false;

        // セッションが有効かつ残り時間が10秒以下の時のみアクティブにする
        if (UFOCameraController.Instance != null && UFOCameraController.IsPlaySessionActive)
        {
            float remaining = UFOCameraController.Instance.RemainingTime;
            if (remaining > 0f && remaining <= 10f)
            {
                shouldBeActive = true;
            }
        }

        // 状態が切り替わった場合のみ、ライトのオンオフを制御
        if (shouldBeActive != _isActive)
        {
            _isActive = shouldBeActive;
            SetLightsEnabled(_isActive);
        }

        // アクティブな間、Y軸で回転させ続ける
        if (_isActive && _patoinTransform != null)
        {
            _patoinTransform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        }
    }

    /// <summary>
    /// ライトコンポーネントの有効・無効を切り替えます。
    /// </summary>
    private void SetLightsEnabled(bool enabled)
    {
        if (_lights != null)
        {
            foreach (var light in _lights)
            {
                if (light != null)
                {
                    light.enabled = enabled;
                }
            }
        }
    }
}
