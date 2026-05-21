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
        [Tooltip("マウス感度の基本値 (deg/pixel)。0.05〜0.3 推奨")]
        [SerializeField] private float lookSensitivity = 0.1f;
        [Tooltip("左右(Yaw)の感度倍率")]
        [SerializeField] private float horizontalSensitivity = 1.0f;
        [Tooltip("上下(Pitch)の感度倍率")]
        [SerializeField] private float verticalSensitivity = 1.0f;
        [Tooltip("Y軸を反転 (マウスを下に動かすと上を向く)")]
        [SerializeField] private bool invertY = false;
        [Tooltip("マウス入力の補間スムージング (0 で即時、0.05〜0.15 でなめらか)")]
        [Range(0f, 0.3f)]
        [SerializeField] private float lookSmoothing = 0f;
        [Tooltip("上を向ける最大角度")]
        [SerializeField] private float maxLookUpAngle = 80.0f;
        [Tooltip("下を向ける最大角度（首の断面が見えないよう制限）")]
        [SerializeField] private float maxLookDownAngle = 45.0f;

        [Header("Avatar Lock (Man_03 等の人メッシュ用)")]
        [Tooltip("avatarRoot の横方向ローカル位置 (X/Z) を 0 に固定し、Yaw 回転時のピボット公転を防ぐ")]
        [SerializeField] private bool lockAvatarLateralPosition = true;

        [Header("Input Gate (Display 制限)")]
        [Tooltip("特定 Display にカーソルがある時だけ WASD / Look 入力を受け付ける")]
        [SerializeField] private bool restrictInputToDisplay = true;

        [Tooltip("入力を受け付ける Display 番号 (0-indexed: 3 = Display 4)")]
        [Range(0, 7)]
        [SerializeField] private int activeInputDisplay = 3;

        [Tooltip("カーソルがロック中はディスプレイ判定をスキップして入力を許可する")]
        [SerializeField] private bool allowWhenCursorLocked = true;

        [Header("Avatar Settings")]
        [SerializeField] private Transform avatarRoot;
        [Tooltip("カメラから隠す頭部メッシュの名前の一部")]
        [SerializeField] private string[] headMeshNames = { "Head", "Eye", "Jaw", "Balaclava", "Face", "head" };

        [Header("Head Bob Settings")]
        [SerializeField] private bool enableHeadBob = true;
        [SerializeField] private float bobSpeed = 14.0f;
        [SerializeField] private float bobAmountX = 0.04f; // 左右の揺れの大きさ
        [SerializeField] private float bobAmountY = 0.06f; // 上下の揺れの大きさ
        [SerializeField] private float bobLerpSpeed = 10.0f; // 揺れ状態への補間速度

        [Header("Animator Settings")]
        [SerializeField] private Animator avatarAnimator;
        [SerializeField] private string animatorSpeedParam = "Speed";

        private CharacterController characterController;
        private Vector2 moveInput;
        private Vector2 lookInput;
        private Vector2 smoothedLook;
        private Vector3 velocity;
        private float cameraPitch = 0.0f;
        private Vector3 defaultCameraLocalPosition;
        private float bobTimer = 0.0f;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            // マウスカーソルをロックして非表示にする
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (playerCamera != null)
            {
                defaultCameraLocalPosition = playerCamera.transform.localPosition;
            }

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
            // Display 判定で入力を遮断する場合、入力値を毎フレーム 0 にする
            // (移動の慣性は途切れるが、重力・接地・ヘッドボブの減衰は通常通り動く)
            if (!IsInputAllowed())
            {
                moveInput = Vector2.zero;
                lookInput = Vector2.zero;
            }
            HandleLook();
            HandleMovement();
            HandleHeadBob();
        }

        /// <summary>現在カーソルが乗っている Display が許可 Display と一致するか</summary>
        private bool IsInputAllowed()
        {
            if (!restrictInputToDisplay) return true;
            // カーソルロック中はマウス位置の信頼性が低いので allow へフォールバック
            if (allowWhenCursorLocked && Cursor.lockState == CursorLockMode.Locked) return true;
            // マルチ Display 未有効時 (Editor の通常 Game ビュー含む) は単一 Display = 0 とみなす
            if (Display.displays.Length <= 1) return activeInputDisplay == 0;
            if (Mouse.current == null) return true;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector3 rel = Display.RelativeMouseAt(mousePos);
            // rel が (0,0,0) (= マウスがどの Display にも乗っていない) の場合は入力遮断
            if (rel == Vector3.zero) return false;
            int currentDisplay = (int)rel.z;
            return currentDisplay == activeInputDisplay;
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
            
            // 水平移動と垂直移動（重力）を1つのベクトルにまとめる
            Vector3 finalMove = moveDirection * moveSpeed + velocity;
            
            // Moveの呼び出しを1回に統合（これによりvelocityの取得や接地判定が安定します）
            characterController.Move(finalMove * Time.deltaTime);

            // Animatorの速度パラメータ更新
            if (avatarAnimator != null)
            {
                float speed = new Vector2(characterController.velocity.x, characterController.velocity.z).magnitude;
                avatarAnimator.SetFloat(animatorSpeedParam, speed);
            }
        }

        private void HandleHeadBob()
        {
            if (!enableHeadBob || playerCamera == null) return;

            // 移動入力があるか判定
            float inputMagnitude = moveInput.magnitude;

            // 接地判定(isGrounded)が地形によってずっとfalseになる場合があるため、条件から外して入力のみで揺らすように変更
            if (inputMagnitude > 0.1f)
            {
                // 移動入力の大きさに応じて揺れのスピードを変化させる
                bobTimer += Time.deltaTime * bobSpeed * inputMagnitude;

                // サイン・コサイン波で左右・上下の揺れを計算 (左右は上下の半分の周期にして無限ループ8の字の揺れにする)
                float bobX = Mathf.Cos(bobTimer * 0.5f) * bobAmountX;
                float bobY = Mathf.Sin(bobTimer) * bobAmountY;

                Vector3 targetPos = defaultCameraLocalPosition + new Vector3(bobX, bobY, 0f);
                playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, targetPos, Time.deltaTime * bobLerpSpeed);
            }
            else
            {
                // 静止時や空中ではゆっくり初期位置に戻す
                bobTimer = 0.0f;
                playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, defaultCameraLocalPosition, Time.deltaTime * bobLerpSpeed);
            }
        }

        private void HandleLook()
        {
            if (playerCamera == null) return;

            // スムージング（任意）
            Vector2 input = lookInput;
            if (lookSmoothing > 0f)
            {
                float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(1e-4f, lookSmoothing));
                smoothedLook = Vector2.Lerp(smoothedLook, input, t);
                input = smoothedLook;
            }
            else
            {
                smoothedLook = input;
            }

            // 左右の視点移動はプレイヤー全体のY軸回転で行う
            float yaw = input.x * lookSensitivity * horizontalSensitivity;
            transform.Rotate(Vector3.up * yaw);

            // 上下の視点移動はカメラ単体のX軸回転で行う（Pitch）
            float pitchDelta = input.y * lookSensitivity * verticalSensitivity;
            if (invertY) pitchDelta = -pitchDelta;
            cameraPitch -= pitchDelta; // 上向きがマイナス、下向きがプラスになるよう調整

            // 上向き（マイナス）は -maxLookUpAngle まで、下向き（プラス）は maxLookDownAngle までに制限
            cameraPitch = Mathf.Clamp(cameraPitch, -maxLookUpAngle, maxLookDownAngle);

            // カメラのローカル回転を更新
            playerCamera.transform.localEulerAngles = new Vector3(cameraPitch, 0f, 0f);
        }

        private void LateUpdate()
        {
            // Yaw 回転で人メッシュが Player 原点を軸に公転するのを防ぐ。
            // avatarRoot のローカル位置から横方向成分 (X/Z) を毎フレーム強制 0 にする。
            if (lockAvatarLateralPosition && avatarRoot != null && avatarRoot.parent == transform)
            {
                Vector3 lp = avatarRoot.localPosition;
                if (lp.x != 0f || lp.z != 0f)
                {
                    lp.x = 0f;
                    lp.z = 0f;
                    avatarRoot.localPosition = lp;
                }
            }
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
