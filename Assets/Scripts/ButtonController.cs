using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ボタンメッシュにアタッチ。
/// New Input System 対応：クリックで視覚的に押し込み、UFOArmController の下降サイクルを起動する。
/// </summary>
public class ButtonController : MonoBehaviour
{
    public enum ButtonType { StartDescent, ToggleClaw, FeverTime }

    [Header("連携")]
    [Tooltip("UFOArmController が付いているオブジェクト")]
    public UFOArmController armController;

    [Header("ボタンの種類")]
    [Tooltip("このボタンを押したときの動作を選びます")]
    public ButtonType buttonType = ButtonType.StartDescent;

    [Header("ボタン演出")]
    [Tooltip("ボタンが押し込まれるローカルY方向の量")]
    public float pressDepth = 0.05f;

    [Tooltip("押し込み／戻りのスピード")]
    public float pressSpeed = 20f;

    [Header("効果音")]
    [Tooltip("ボタンを押したときの効果音")]
    [SerializeField] private AudioClip clickSound;

    [Tooltip("効果音の音量調整 (1.0より大きい値で音量増幅可能)")]
    [Range(0f, 10f)]
    [SerializeField] private float soundVolume = 1.0f;

    [Header("チュートリアル用制御元（任意）")]
    [Tooltip("設定すると、UFOCameraControllerの静的な状態の代わりにこちらを参照します（ICraneControlSourceを実装したコンポーネント）。" +
             "未設定なら今まで通りUFOCameraControllerを見ます（実機用）。")]
    [SerializeField] private MonoBehaviour controlSourceOverride;
    private ICraneControlSource _controlSource;

    private Vector3  _originalLocalPos;
    private bool     _isPressed = false;
    private Collider _collider;

    void Start()
    {
        _originalLocalPos = transform.localPosition;
        _collider         = GetComponent<Collider>();
        _controlSource    = controlSourceOverride as ICraneControlSource;
    }

    private bool IsPlaying => _controlSource != null ? _controlSource.IsPlayingCrane : UFOCameraController.IsPlayingUfo;
    private bool IsActive => _controlSource != null ? _controlSource.IsButtonTypeActive(buttonType) : UFOCameraController.IsControlActive;
    private Camera ActiveCam => _controlSource != null
        ? _controlSource.GetActiveCamera()
        : (UFOCameraController.Instance != null ? UFOCameraController.Instance.GetActiveCamera() : Camera.main);
    private void NotifyInput()
    {
        if (_controlSource != null) _controlSource.NotifyControlInputUsed();
        else UFOCameraController.Instance?.NotifyControlInputUsed();
    }

    void Update()
    {
        // UFOプレイ中かつアクティブなプレイセッション中のみ操作を許可する
        if (!IsPlaying || !IsActive)
        {
            _isPressed = false;
            float inactiveTargetY = _originalLocalPos.y;
            Vector3 inactivePos = transform.localPosition;
            inactivePos.y = Mathf.Lerp(inactivePos.y, inactiveTargetY, Time.deltaTime * pressSpeed);
            transform.localPosition = inactivePos;
            return;
        }

        var mouse = Mouse.current;
        if (mouse != null)
        {
            // クリック開始：マウスがこのオブジェクト上にあるとき
            if (mouse.leftButton.wasPressedThisFrame && IsMouseOverThis(mouse.position.ReadValue()))
            {
                TriggerPress();
            }

            // クリック終了
            if (mouse.leftButton.wasReleasedThisFrame)
                TriggerRelease();
        }

        // 押し込み演出
        float   targetY = _isPressed ? _originalLocalPos.y - pressDepth : _originalLocalPos.y;
        Vector3 pos     = transform.localPosition;
        pos.y                 = Mathf.Lerp(pos.y, targetY, Time.deltaTime * pressSpeed);
        transform.localPosition = pos;
    }

    /// <summary>
    /// このボタンを押した状態にする（押し込み演出・効果音・対応する動作の実行）。
    /// マウスクリックからも、UFOKeyboardController（キーボードショートカット）からも、
    /// 同じ見た目・音・挙動になるようこのメソッドに一本化している。
    /// </summary>
    public void TriggerPress()
    {
        Debug.Log($"[TutorialDebug] TriggerPress: buttonType={buttonType}, IsActive={IsActive}");
        // UFOプレイ中かつアクティブなプレイセッション中のみ操作を許可する
        if (!IsPlaying || !IsActive) return;

        _isPressed = true;
        NotifyInput();
        // 押下と同じフレームで即座に通知する（連打防止のロックを1フレームも遅れず掛けられるように）
        _controlSource?.NotifyButtonPressed(buttonType);

        // クリック効果音の再生
        PlaySound(clickSound);

        if (armController != null)
        {
            if (buttonType == ButtonType.StartDescent)
            {
                armController.StartDescentCycle();
            }
            else if (buttonType == ButtonType.ToggleClaw)
            {
                armController.ToggleClaw();
            }
            else if (buttonType == ButtonType.FeverTime)
            {
                UFOItemGoal goal = FindAnyObjectByType<UFOItemGoal>();
                if (goal != null)
                {
                    goal.StartFeverTime();
                }
                else
                {
                    Debug.LogError("[ButtonController] UFOItemGoal がシーン内に見つかりません。");
                }
            }
        }
    }

    /// <summary>押し込み演出を解除する（見た目を元の高さへ戻す）。動作の実行は伴わない。</summary>
    public void TriggerRelease()
    {
        _isPressed = false;
    }

    /// <summary>マウス座標がこのオブジェクトのCollider上にあるか判定</summary>
    bool IsMouseOverThis(Vector2 screenPos)
    {
        Camera activeCam = ActiveCam;
        if (_collider == null || activeCam == null) return false;
        Ray ray = activeCam.ScreenPointToRay(screenPos);
        return _collider.Raycast(ray, out _, 1000f);
    }

    private void PlaySound(AudioClip clip)
    {
        UFOSEManager.Instance?.PlayOneShot(clip, soundVolume);
    }
}
