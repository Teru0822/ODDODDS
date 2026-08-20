using UnityEngine;

/// <summary>
/// UFOキャッチャーの残り時間が10秒以下になった際に、パトランプを光らせて回転（Y軸）させる
/// コントローラー。
///
/// パトランプのLight.enabled/色そのものはUFOChaseLightControllerが一元管理しており
/// （残り時間警告中以外は、コイン獲得フラッシュ・チェイス演出等でUFOChaseLightController自身が
/// パトランプも含めて点灯を制御する）、このスクリプトは chaseLightController.IsWarningBlinkActive
/// （残り時間警告中かどうか）だけを見て、trueの間だけ点灯・回転を担当する。
/// それ以外の時間帯は一切手を出さないため、他の演出との競合は起きない。
/// </summary>
public class PatoLampController : MonoBehaviour
{
    [Tooltip("このパトランプが属する実機のUFOChaseLightController")]
    [SerializeField] private UFOChaseLightController chaseLightController;

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

        if (chaseLightController == null)
        {
            Debug.LogWarning($"[{gameObject.name}] Chase Light Controller が未設定です。インスペクターで実機の UFOChaseLightController を設定してください。", this);
        }
    }

    private void Update()
    {
        bool shouldBeActive = chaseLightController != null && chaseLightController.IsWarningBlinkActive;

        if (shouldBeActive && !_isActive)
        {
            // 警告点滅が始まった瞬間だけ、こちらで点灯させる
            _isActive = true;
            SetLightsEnabled(true);
        }
        else if (!shouldBeActive && _isActive)
        {
            // 警告点滅が終わった後の消灯・別演出への切り替えは、常にUFOChaseLightController側の
            // 各モードの遷移処理が責任を持って行う（Deferred/CoinCatchFlash等が、警告点滅の直後に
            // パトランプも含めて自分で点灯させたい場合があるため、ここで消灯を上書きしない。
            // Off等、本当に消灯すべき場合はUFOChaseLightController側が明示的に消灯する）
            _isActive = false;
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
