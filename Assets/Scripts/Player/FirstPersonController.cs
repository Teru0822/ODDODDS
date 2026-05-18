using UnityEngine;
using UnityEngine.InputSystem;

namespace App.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5.0f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float jumpHeight = 1.2f;

        [Header("Look Settings")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float lookSensitivity = 1.0f;
        [Tooltip("上を向ける最大角度")]
        [SerializeField] private float maxLookUpAngle = 80.0f;
        [Tooltip("下を向ける最大角度（首の断面が見えないよう制限）")]
        [SerializeField] private float maxLookDownAngle = 45.0f;

        [Header("Avatar Settings")]
        [SerializeField] private Transform avatarRoot;
        [Tooltip("カメラから隠す頭部メッシュの名前の一部")]
        [SerializeField] private string[] headMeshNames = { "Head", "Eye", "Jaw", "Balaclava", "Face", "head" };

        private CharacterController characterController;
        private Vector2 moveInput;
        private Vector2 lookInput;
        private Vector3 velocity;
        private float cameraPitch = 0.0f;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            // マウスカーソルをロックして非表示にする
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            SetupAvatarHeadCulling();
        }

        private void SetupAvatarHeadCulling()
        {
            if (playerCamera == null || avatarRoot == null) return;

            int headLayer = LayerMask.NameToLayer("PlayerHead");
            if (headLayer == -1)
            {
                Debug.LogWarning("PlayerHead レイヤーが存在しません。TagManager に追加してください。");
                return;
            }

            // カメラのCullingMaskからPlayerHeadレイヤーを除外
            playerCamera.cullingMask &= ~(1 << headLayer);

            // アバター内の頭部関連メッシュを探してレイヤーを変更
            Transform[] allChildren = avatarRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allChildren)
            {
                foreach (string keyword in headMeshNames)
                {
                    if (t.name.Contains(keyword, System.StringComparison.OrdinalIgnoreCase))
                    {
                        t.gameObject.layer = headLayer;
                        break;
                    }
                }
            }
        }

        private void Update()
        {
            HandleLook();
            HandleMovement();
        }

        private void HandleMovement()
        {
            // 接地判定
            bool isGrounded = characterController.isGrounded;
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f; // 接地時は少し下方向に押し付けておく
            }

            // カメラの向き（プレイヤーの向き）に基づいた移動方向の計算
            Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
            characterController.Move(moveDirection * (moveSpeed * Time.deltaTime));

            // 重力の適用
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }

        private void HandleLook()
        {
            if (playerCamera == null) return;

            // 左右の視点移動はプレイヤー全体のY軸回転で行う
            float yaw = lookInput.x * lookSensitivity;
            transform.Rotate(Vector3.up * yaw);

            // 上下の視点移動はカメラ単体のX軸回転で行う（Pitch）
            float pitchDelta = lookInput.y * lookSensitivity;
            cameraPitch -= pitchDelta; // 上向きがマイナス、下向きがプラスになるよう調整
            
            // 上向き（マイナス）は -maxLookUpAngle まで、下向き（プラス）は maxLookDownAngle までに制限
            cameraPitch = Mathf.Clamp(cameraPitch, -maxLookUpAngle, maxLookDownAngle);

            // カメラのローカル回転を更新
            playerCamera.transform.localEulerAngles = new Vector3(cameraPitch, 0f, 0f);
        }

        // --- Input System Messages ---
        // PlayerInputコンポーネントの SendMessages または BroadcastMessages によって呼ばれる

        private void OnMove(InputValue value)
        {
            moveInput = value.Get<Vector2>();
        }

        private void OnLook(InputValue value)
        {
            lookInput = value.Get<Vector2>();
        }

        private void OnJump(InputValue value)
        {
            if (value.isPressed && characterController.isGrounded)
            {
                // ジャンプの初速計算: v = sqrt(h * -2 * g)
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
    }
}
