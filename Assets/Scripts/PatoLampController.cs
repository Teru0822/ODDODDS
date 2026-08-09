using UnityEngine;

/// <summary>
/// UFOキャッチャーの残り時間が10秒以下になった際に、
/// パトランプを光らせて回転（Y軸）させるコントローラー。
/// </summary>
public class PatoLampController : MonoBehaviour
{
    [Tooltip("回転速度 (度/秒)")]
    [SerializeField] private float rotationSpeed = 360f;

    [Tooltip("状態確認用のログを出力する。常時オンにすると負荷になるので、調査時だけ有効にしてください")]
    [SerializeField] private bool _showDebugLogs = false;

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

        // 【デバッグ用】どの条件で止まっているか特定するための一時ログ（0.5秒間隔で出力）
        // 既定でオフ。常時出力すると Debug.Log のコストでフレームレートが落ちる
        if (_showDebugLogs && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[PatoLampController] DEBUG: Instance={(UFOCameraController.Instance != null)}, " +
                      $"IsPlaySessionActive={UFOCameraController.IsPlaySessionActive}, " +
                      $"Remaining={(UFOCameraController.Instance != null ? UFOCameraController.Instance.RemainingTime : -999f):F1}, " +
                      $"IsFlashing={UFOItemGoal.IsFlashing}, " +
                      $"lightsCount={(_lights != null ? _lights.Length : -1)}");
        }

        // セッションが有効かつ残り時間が10秒以下で、かつアイテム獲得演出中でない時のみアクティブにする
        if (UFOCameraController.Instance != null && UFOCameraController.IsPlaySessionActive)
        {
            float remaining = UFOCameraController.Instance.RemainingTime;
            if (remaining > 0f && remaining <= 10f && !UFOItemGoal.IsFlashing)
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
