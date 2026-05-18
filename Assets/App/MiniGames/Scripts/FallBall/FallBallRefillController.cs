using System.Collections;
using UnityEngine;

namespace MiniGames.FallBall
{
    /// <summary>
    /// 鉄球落としのボール補充アニメーションを管理するクラス。
    /// 
    /// 2つのモードに対応:
    /// 1. アニメーションクリップモード: Blender の「Scene」等のクリップを直接再生
    /// 2. シェイプキーモード: スクリプトからシェイプキーを制御
    /// 
    /// AnimationClip が設定されていればそちらを優先し、なければシェイプキーモードを使用します。
    /// </summary>
    public class FallBallRefillController : MonoBehaviour
    {
        [Header("アニメーションクリップモード")]
        [Tooltip("補充アニメーションのクリップ（Blender の Scene 等）。Projectウィンドウからドラッグしてください")]
        [SerializeField] private AnimationClip refillClip;
        [Tooltip("アニメーションを再生する対象のGameObject（fallball改のルートなど）")]
        [SerializeField] private GameObject animationTarget;

        [Header("シェイプキー対象（アニメーションクリップ未使用時）")]
        [Tooltip("昇降棒（円柱）の SkinnedMeshRenderer")]
        [SerializeField] private SkinnedMeshRenderer rodRenderer;
        
        [Tooltip("補充アーム（立方体.001）の SkinnedMeshRenderer")]
        [SerializeField] private SkinnedMeshRenderer armRenderer;

        [Header("ボール設定")]
        [Tooltip("鉄球のテンプレート（球.002）")]
        [SerializeField] private GameObject ballTemplate;
        [Header("Model References")]
        [Tooltip("ボールが生成される場所（アームの先端など）")]
        [SerializeField] private Transform ballSpawnParent;
        
        [Tooltip("無効化対象のコライダーを直接指定してください")]
        [SerializeField] private Collider[] armColliders;

        [Header("Components (Legacy Mode)")]
        [Tooltip("昇降棒が伸びる時間（秒）")]
        [SerializeField] private float extendDuration = 1.0f;
        
        [Tooltip("アームが開く時間（秒）")]
        [SerializeField] private float openDuration = 0.5f;
        
        [Tooltip("アームを閉じつつ昇降棒を縮める時間（秒）")]
        [SerializeField] private float retractDuration = 1.0f;
        
        [Header("Frame Settings")]
        [Tooltip("コライダーを無効化する開始フレーム")]
        [SerializeField] private int disableStartFrame = 45;
        [Tooltip("コライダーを有効化に戻す終了フレーム")]
        [SerializeField] private int disableEndFrame = 60;

        [Tooltip("アームが開いてからボールを離すまでの待機時間（秒）")]
        [SerializeField] private float dropDelay = 0.2f;

        /// <summary>
        /// 補充シーケンスが実行中かどうか。
        /// </summary>
        public bool IsRefilling { get; private set; }

        private void SetArmCollidersEnabled(bool enabled)
        {
            if (armColliders == null || armColliders.Length == 0)
            {
                Debug.LogWarning("FallBallRefill: 無効化対象の armColliders が設定されていません！");
                return;
            }

            foreach (var col in armColliders)
            {
                if (col != null)
                {
                    col.enabled = enabled;
                }
            }
            Debug.Log($"FallBallRefill: 指定された {armColliders.Length} 個のコライダーを {(enabled ? "有効" : "無効")} にしました");
        }

        // シェイプキーのインデックス
        private int rodBlendShapeIndex = -1;
        private int armBlendShapeIndex = -1;
        
        // アニメーションクリップ再生用
        private Animation legacyAnimation;

        private void Start()
        {
            Debug.Log($"FallBallRefill Start: refillClip={refillClip != null}, animationTarget={animationTarget != null}, " +
                      $"rodRenderer={rodRenderer != null}, armRenderer={armRenderer != null}, " +
                      $"ballTemplate={ballTemplate != null}, ballSpawnParent={ballSpawnParent != null}");

            // シェイプキーモード用: インデックスを自動取得
            if (rodRenderer != null)
            {
                LogBlendShapeNames(rodRenderer, "昇降棒");
                rodBlendShapeIndex = FindBlendShapeIndex(rodRenderer, "キー 1");
                Debug.Log($"FallBallRefill: 昇降棒シェイプキー index={rodBlendShapeIndex}");
                if (rodBlendShapeIndex < 0) rodBlendShapeIndex = 0;
            }
            if (armRenderer != null)
            {
                LogBlendShapeNames(armRenderer, "アーム");
                armBlendShapeIndex = FindBlendShapeIndex(armRenderer, "キー 1");
                Debug.Log($"FallBallRefill: アームシェイプキー index={armBlendShapeIndex}");
                if (armBlendShapeIndex < 0) armBlendShapeIndex = 0;
            }

            // アニメーションクリップモード用: legacy Animation コンポーネントを準備
            if (refillClip != null)
            {
                GameObject target = animationTarget != null ? animationTarget : gameObject;
                legacyAnimation = target.GetComponent<Animation>();
                if (legacyAnimation == null)
                {
                    legacyAnimation = target.AddComponent<Animation>();
                }
                refillClip.legacy = true;
                legacyAnimation.AddClip(refillClip, refillClip.name);
                legacyAnimation.playAutomatically = false;
                Debug.Log($"FallBallRefill: アニメーションクリップ「{refillClip.name}」をセットアップ完了（長さ: {refillClip.length}秒）");
            }
            else
            {
                Debug.Log("FallBallRefill: アニメーションクリップ未設定。シェイプキーモードを使用します。");
            }
        }

        private int FindBlendShapeIndex(SkinnedMeshRenderer renderer, string shapeName)
        {
            if (renderer == null || renderer.sharedMesh == null) return -1;
            int count = renderer.sharedMesh.blendShapeCount;
            for (int i = 0; i < count; i++)
            {
                if (renderer.sharedMesh.GetBlendShapeName(i) == shapeName) return i;
            }
            for (int i = 0; i < count; i++)
            {
                if (renderer.sharedMesh.GetBlendShapeName(i).Contains(shapeName)) return i;
            }
            return -1;
        }

        /// <summary>
        /// SkinnedMeshRenderer のシェイプキー名を全てログ出力します（デバッグ用）。
        /// </summary>
        private void LogBlendShapeNames(SkinnedMeshRenderer renderer, string label)
        {
            if (renderer == null || renderer.sharedMesh == null)
            {
                Debug.Log($"FallBallRefill: {label} - sharedMesh が null です");
                return;
            }
            int count = renderer.sharedMesh.blendShapeCount;
            if (count == 0)
            {
                Debug.Log($"FallBallRefill: {label} - シェイプキーがありません（count=0）");
                return;
            }
            for (int i = 0; i < count; i++)
            {
                string name = renderer.sharedMesh.GetBlendShapeName(i);
                Debug.Log($"FallBallRefill: {label} シェイプキー[{i}] = \"{name}\"");
            }
        }

        /// <summary>
        /// ボール補充シーケンスを実行するコルーチン。
        /// </summary>
        public IEnumerator PlayRefillSequence()
        {
            if (IsRefilling) yield break;
            IsRefilling = true;

            Debug.Log($"FallBallRefill: PlayRefillSequence 開始 (clipMode={refillClip != null && legacyAnimation != null})");

            if (refillClip != null && legacyAnimation != null)
            {
                yield return StartCoroutine(PlayClipRefill());
            }
            else
            {
                yield return StartCoroutine(PlayShapeKeyRefill());
            }

            IsRefilling = false;
            Debug.Log("FallBallRefill: PlayRefillSequence 完了");
        }

        /// <summary>
        /// AnimationClip を使った補充アニメーション。
        /// </summary>
        private IEnumerator PlayClipRefill()
        {
            Debug.Log($"FallBallRefill: 補充アニメーション「{refillClip.name}」を開始します");
            
            // 1. 最初からボールを生成（親子関係なし、物理有効）
            SpawnBallInArm();

            // 2. アニメーション再生
            legacyAnimation.Play(refillClip.name);
            
            // 3. フレーム指定によるコライダー制御
            float fps = refillClip.frameRate;
            float startTime = (float)disableStartFrame / fps;
            float endTime = (float)disableEndFrame / fps;

            // 無効化タイミングまで待機
            yield return new WaitForSeconds(startTime);
            SetArmCollidersEnabled(false);

            // 有効化タイミングまで待機
            yield return new WaitForSeconds(Mathf.Max(0, endTime - startTime));
            SetArmCollidersEnabled(true);

            // アニメーション完了を待つ（すでに時間が経過している分を引く）
            float remaining = refillClip.length - endTime;
            if (remaining > 0) yield return new WaitForSeconds(remaining);
            
            Debug.Log("FallBallRefill: 補充シーケンス完了");
        }

        /// <summary>
        /// シェイプキーを使った補充アニメーション。
        /// </summary>
        private IEnumerator PlayShapeKeyRefill()
        {
            Debug.Log("FallBallRefill: シェイプキーによる補充を開始します");
            
            SpawnBallInArm();

            yield return StartCoroutine(AnimateBlendShape(rodRenderer, rodBlendShapeIndex, 0f, 100f, extendDuration));
            yield return StartCoroutine(AnimateBlendShape(armRenderer, armBlendShapeIndex, 0f, 100f, openDuration));

            yield return new WaitForSeconds(dropDelay);
            
            StartCoroutine(AnimateBlendShape(armRenderer, armBlendShapeIndex, 100f, 0f, retractDuration));
            yield return StartCoroutine(AnimateBlendShape(rodRenderer, rodBlendShapeIndex, 100f, 0f, retractDuration));
        }

        private GameObject SpawnBallInArm()
        {
            if (ballTemplate == null || ballSpawnParent == null) return null;

            // 親を指定せず、現在のスポーン位置に生成
            GameObject newBall = Instantiate(ballTemplate, ballSpawnParent.position, ballSpawnParent.rotation);
            newBall.name = "RefilledBall_" + Time.frameCount;
            
            // 重要：アウト判定(OutZone)で検知されるようにタグを設定
            try
            {
                newBall.tag = "Ball";
            }
            catch (UnityEngine.UnityException ex)
            {
                Debug.LogError($"FallBallRefillController: 'Ball' タグの設定に失敗しました。Unityのエディタ設定で 'Ball' タグが定義されているか確認してください。エラー: {ex.Message}");
            }
            
            newBall.transform.localScale = ballTemplate.transform.localScale;
            newBall.SetActive(true);
            
            Rigidbody rb = newBall.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false; 
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.sleepThreshold = 0f;
                
                Debug.Log($"FallBallRefill: ボールを独立生成: {newBall.name}");
            }

            return newBall;
        }

        private void ReleaseBall(GameObject ball)
        {
            // 最初からはなしているので、ここでは何もしない
        }

        private IEnumerator AnimateBlendShape(SkinnedMeshRenderer renderer, int index, float from, float to, float duration)
        {
            if (renderer == null || index < 0) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                renderer.SetBlendShapeWeight(index, Mathf.Lerp(from, to, smoothT));
                yield return null;
            }
            renderer.SetBlendShapeWeight(index, to);
        }
    }
}
