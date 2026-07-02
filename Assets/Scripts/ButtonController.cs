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
    [Tooltip("再生用のAudioSource。未設定の場合は自動でGetComponentします")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("ボタンを押したときの効果音")]
    [SerializeField] private AudioClip clickSound;

    [Tooltip("効果音の音量調整 (1.0より大きい値で音量増幅可能)")]
    [Range(0f, 10f)]
    [SerializeField] private float soundVolume = 1.0f;

    private Vector3  _originalLocalPos;
    private bool     _isPressed = false;
    private Collider _collider;

    void Start()
    {
        _originalLocalPos = transform.localPosition;
        _collider         = GetComponent<Collider>();
        
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    void Update()
    {
        // UFOプレイ中かつアクティブなプレイセッション中のみ操作を許可する
        if (!UFOCameraController.IsPlayingUfo || !UFOCameraController.IsControlActive)
        {
            _isPressed = false;
            float inactiveTargetY = _originalLocalPos.y;
            Vector3 inactivePos = transform.localPosition;
            inactivePos.y = Mathf.Lerp(inactivePos.y, inactiveTargetY, Time.deltaTime * pressSpeed);
            transform.localPosition = inactivePos;
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null) return;

        // クリック開始：マウスがこのオブジェクト上にあるとき
        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (IsMouseOverThis(mouse.position.ReadValue()))
            {
                _isPressed = true;
                
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
        }

        // クリック終了
        if (mouse.leftButton.wasReleasedThisFrame)
            _isPressed = false;

        // 押し込み演出
        float   targetY = _isPressed ? _originalLocalPos.y - pressDepth : _originalLocalPos.y;
        Vector3 pos     = transform.localPosition;
        pos.y                 = Mathf.Lerp(pos.y, targetY, Time.deltaTime * pressSpeed);
        transform.localPosition = pos;
    }

    /// <summary>マウス座標がこのオブジェクトのCollider上にあるか判定</summary>
    bool IsMouseOverThis(Vector2 screenPos)
    {
        Camera activeCam = UFOCameraController.Instance != null ? UFOCameraController.Instance.GetActiveCamera() : Camera.main;
        if (_collider == null || activeCam == null) return false;
        Ray ray = activeCam.ScreenPointToRay(screenPos);
        return _collider.Raycast(ray, out _, 1000f);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f; // 2D音響にして距離減衰を無視する
                }
            }
            audioSource.PlayOneShot(clip, soundVolume);
        }
    }
}
