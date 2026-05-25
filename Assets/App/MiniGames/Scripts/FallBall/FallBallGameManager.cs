using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniGames.FallBall
{
    /// <summary>
    /// 鉄球落とし（FALLBALL）ミニゲームの進行管理と判定を行うクラス。
    /// IMiniGame インターフェースを実装。
    /// </summary>
    public class FallBallGameManager : MonoBehaviour, IMiniGame
    {
        public event Action<bool, float> OnGameCompleted;

        [Header("Settings")]
        [Tooltip("成功時の獲得倍率")]
        [SerializeField] private float successMultiplier = 2.0f;
        
        [Header("References")]
        [Tooltip("棒の操作コントローラー")]
        [SerializeField] private BarController barController;
        [Tooltip("落下させる鉄球のGameObject（プレハブまたはシーン内の元オブジェクト）")]
        [SerializeField] private GameObject ballObject;
        [Tooltip("ボール補充アニメーションのコントローラー")]
        [SerializeField] private FallBallRefillController refillController;

        [Header("Debug & Test")]
        [Tooltip("テスト用に、ボールが落ちてもゲームを終了せず操作を続けられるようにする")]
        [SerializeField] private bool allowContinuousPlay = false; // 動的UI連動のためデフォルトfalseに

        [Header("Play Limits")]
        [Tooltip("1プレイでの制限時間（秒）")]
        [SerializeField] private float maxPlayTime = 60f;
        [Tooltip("1プレイで補充できる最大球数")]
        [SerializeField] private int maxPlayCount = 5;
        
        private Rigidbody ballRigidbody;
        private float currentBet; // 全額対応のためfloatに変更
        private bool isFinished = false;
        private bool isPlaying = false; // プレイ中かどうかのフラグ

        // 状態公開用プロパティ
        public bool IsPlaying => isPlaying;
        public float PlayTimer { get; private set; }
        public int UsedBallsCount { get; private set; }
        public int MaxPlayCount => maxPlayCount;
        public float CurrentBet => currentBet;

        private GameObject ballTemplate;
        private Vector3 initialBallPosition;
        private Quaternion initialBallRotation;

        private void Start()
        {
            Debug.Log($"FallBallGameManager Start: ballObject={ballObject != null}, refillController={refillController != null}");
            
            if (ballObject != null)
            {
                // シーン内のオブジェクトが直接指定されている場合、Destroyされないようにテンプレートとして保持
                // (プレハブでない場合は scene.name が入る)
                if (ballObject.gameObject.scene.name != null)
                {
                    ballTemplate = ballObject;
                    initialBallPosition = ballObject.transform.position;
                    initialBallRotation = ballObject.transform.rotation;
                    
                    // シーン内の実体そのものが消えないよう、補充時はこれのクローンを作る
                    // 最初の1個目を出す前に非表示にしておく
                    ballObject.SetActive(false);
                }
                else
                {
                    ballTemplate = ballObject;
                    initialBallPosition = transform.position;
                    initialBallRotation = Quaternion.identity;
                }
                
                if (refillController != null)
                {
                    StartCoroutine(InitialRefill());
                }
                else
                {
                    SpawnNewBall();
                }
            }
            else
            {
                Debug.LogWarning("FallBallGameManager: ballObject が設定されていません！");
            }
        }

        private System.Collections.IEnumerator InitialRefill()
        {
            // 起動直後だとアニメーションが正しく開始されない場合があるため、少し待つ
            yield return new WaitForSeconds(0.5f);
            Debug.Log("FallBallGameManager: 起動時の自動補充（アニメーション付き）を実行します。");
            SpawnNewBall();
        }

        private void Update()
        {
            if (isPlaying)
            {
                // タイマー減少
                PlayTimer -= Time.deltaTime;
                if (PlayTimer <= 0)
                {
                    PlayTimer = 0;
                    GameOver(false); // 時間切れで失敗
                    return;
                }
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                // プレイ中のみスペースキー（補充）を許可する
                if (!isPlaying && !allowContinuousPlay) return;

                Debug.Log($"FallBallGameManager: Spaceキー検知! refillController={refillController != null}, IsRefilling={refillController?.IsRefilling}");
                
                // 補充アニメーション中は追加スポーンを無効化
                if (refillController != null && refillController.IsRefilling) return;
                
                SpawnNewBall();
            }
        }

        private void SpawnNewBall()
        {
            if (isPlaying)
            {
                if (UsedBallsCount >= maxPlayCount)
                {
                    Debug.Log("FallBallGameManager: 制限球数に達しました。");
                    GameOver(false); // 弾切れ失敗
                    return;
                }
                UsedBallsCount++;
            }

            // RefillController が設定されている場合はアニメーション付きで補充
            // (RefillController は自身の ballTemplate を持つので GameManager の ballTemplate は不要)
            if (refillController != null)
            {
                StartCoroutine(refillController.PlayRefillSequence());
                Debug.Log($"FallBall: 補充アニメーションを開始しました (球数: {UsedBallsCount}/{maxPlayCount})");
                return;
            }
            
            if (ballTemplate == null) return;
            
            // RefillController が未設定の場合は従来のシンプルなスポーン
            GameObject newBall = Instantiate(ballTemplate, initialBallPosition, initialBallRotation);
            newBall.SetActive(true);
            
            Rigidbody rb = newBall.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
            }
            Debug.Log($"FallBall: 新しいボールを出しました！ (球数: {UsedBallsCount}/{maxPlayCount})");
        }

        public void Initialize(float betAmount)
        {
            currentBet = betAmount;
            isFinished = false;
            isPlaying = false;
            PlayTimer = maxPlayTime;
            UsedBallsCount = 0;
            
            if (ballObject != null)
            {
                ballRigidbody = ballObject.GetComponent<Rigidbody>();
            }
            
            // 操作を一旦無効化して物理挙動も止めておく
            if (barController != null) barController.SetActive(false);
            if (ballRigidbody != null)
            {
                ballRigidbody.isKinematic = true;
                ballRigidbody.linearVelocity = Vector3.zero;
            }
            
            Debug.Log($"FallBall Initialized. Bet: {betAmount}");
        }

        public void StartGame()
        {
            // ゲーム開始：操作と物理挙動を有効化
            isPlaying = true;
            if (barController != null) barController.SetActive(true);
            if (ballRigidbody != null) ballRigidbody.isKinematic = false;
            
            Debug.Log("FallBall Started!");
            
            // 最初の球をスポーンする
            SpawnNewBall();
        }

        public void GameOver(bool isSuccess)
        {
            if (isFinished) return;
            isFinished = true;
            isPlaying = false;

            if (barController != null) 
            {
                barController.SetActive(false);
            }

            if (isSuccess)
            {
                Debug.Log($"FallBall: Goal Reached! Success. Won: {currentBet * successMultiplier}");
                if (MoneyManager.Instance != null && currentBet > 0)
                {
                    MoneyManager.Instance.AddMoney(currentBet, successMultiplier);
                }
            }
            else
            {
                Debug.Log("FallBall: Game Over! Failed.");
                // お金はすでにプレイ開始時に徴収されているので加算なし（没収）
            }

            OnGameCompleted?.Invoke(isSuccess, isSuccess ? successMultiplier : 0);
        }

        /// <summary>
        /// ボールが「筒（ゴール）」に触れたときに呼ばれる。
        /// ゴールとなるTriggerコライダーを持つオブジェクトのスクリプトから呼び出す想定。
        /// </summary>
        public void OnGoalReached()
        {
            Debug.Log("FallBall: ゴールに到達！成功です！");
            if (isFinished && !allowContinuousPlay) return;
            
            GameOver(true);
        }

        /// <summary>
        /// ボールが場外に落ちた（失敗）ときに呼ばれる。
        /// </summary>
        public void OnOutZoneReached()
        {
            // 自動再生成を有効にするため、ここでは単にログを出して再生成ルーチンを呼ぶ
            Debug.Log("FallBall: ボールが場外に落ちました");
            OnBallExit();
        }

        /// <summary>
        /// ボールがシーンから消えた（アウトまたはゴール）際に、次のボールを出すための通知。
        /// </summary>
        public void OnBallExit()
        {
            Debug.Log($"FallBallGameManager: OnBallExit呼ばれました. isFinished={isFinished}, allowContinuousPlay={allowContinuousPlay}");
            
            if (isFinished && !allowContinuousPlay) 
            {
                Debug.Log("FallBallGameManager: ゲーム終了済みのため再補充をスキップします。");
                return;
            }
            
            Debug.Log("FallBall: ボール退出検知。1秒後に再出現させます。");
            StartCoroutine(WaitAndSpawnBall());
        }

        private System.Collections.IEnumerator WaitAndSpawnBall()
        {
            yield return new WaitForSeconds(1.0f);
            SpawnNewBall();
        }
    }
}
