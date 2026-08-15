using UnityEngine;

/// <summary>
/// 練習UFOキャッチャー（Practice_Cranegame）用のパトランプコントローラー。
/// 実機のPatoLampControllerはUFOCameraController.Instance（実機の残り時間）を直接参照するため、
/// 同じスクリプトを練習機側にそのまま付けると実機の残り時間で光ってしまう。
/// こちらはTutorialCraneController側の残り時間を見て、練習機の残り時間が10秒以下になったときだけ
/// 光る・回転するようにした練習機専用版。
///
/// 音は鳴らさない（パトランプは複数個所に配置されるため、ここで鳴らすと台数分重複してしまう。
/// 実機がUFOCameraControllerに集約しているのと同じ理由で、音はTutorialCraneController側に集約する）。
/// </summary>
public class TutorialPatoLampController : MonoBehaviour
{
    [Tooltip("このパトランプが属する練習UFOキャッチャーのTutorialCraneController")]
    [SerializeField] private TutorialCraneController tutorialCrane;

    [Tooltip("回転速度 (度/秒)")]
    [SerializeField] private float rotationSpeed = 360f;

    [Tooltip("残り時間が何秒以下になったらパトランプを作動させるか")]
    [SerializeField] private float activateThresholdSeconds = 10f;

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

        if (tutorialCrane != null && tutorialCrane.IsPlayingTutorial)
        {
            float remaining = tutorialCrane.RemainingTime;
            if (remaining > 0f && remaining <= activateThresholdSeconds)
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
