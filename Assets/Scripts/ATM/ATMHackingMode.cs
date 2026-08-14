using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace App.ATM
{
    /// <summary>
    /// ATM の裏メニュー「ハッキングモード」。
    ///
    /// ATM 起動中、モデル上の atm_design4 が赤く点滅する。クリックすると効果音とともに
    /// パネルがローカル Z 軸方向へ沈み、ATM 画面が一瞬切れてハッキング画面が起動する。
    /// 架空銀行間の送金 3 件から 1 つ選び、「ファイアウォール突破」ミニゲームを
    /// 最後まで突破できれば送金額を横取りできる。
    ///
    /// ATMController から実行時に自動生成されるため、シーンやプレハブへの配置は不要。
    /// </summary>
    [DisallowMultipleComponent]
    public class ATMHackingMode : MonoBehaviour
    {
        [Header("トリガー (atm_design4)")]
        [Tooltip("点滅させてクリック対象にするオブジェクト。未指定なら子階層から名前で自動検索する")]
        [SerializeField] private Transform triggerObject;

        [Tooltip("自動検索するときのオブジェクト名")]
        [SerializeField] private string triggerObjectName = "atm_design4";

        [Tooltip("点滅の色")]
        [SerializeField] private Color blinkColor = new Color(1f, 0.08f, 0.08f, 1f);

        [Tooltip("点滅の速さ(1秒あたりの往復回数)")]
        [SerializeField] private float blinkSpeed = 2.2f;

        [Tooltip("Emission の強さ。暗い場所で光って見える程度に調整する")]
        [SerializeField] private float blinkIntensity = 3.5f;

        [Tooltip("ATM を起動している間だけ点滅させる")]
        [SerializeField] private bool blinkOnlyWhileScreenActive = true;

        [Tooltip("点滅中に出すアウトラインの色")]
        [SerializeField] private Color outlineColor = new Color(1f, 0.1f, 0.1f, 1f);

        [Tooltip("アウトラインの太さ(ピクセル)。0 にするとアウトラインを出さない")]
        [Range(0f, 12f)]
        [SerializeField] private float outlineWidth = 4f;

        [Header("クリック時の沈み込み")]
        [Tooltip("沈む方向(オブジェクト自身のローカル軸)。既定 (0,0,-1)=ローカルZ軸の負方向")]
        [SerializeField] private Vector3 pressDirection = Vector3.back;

        [Tooltip("沈む量 (メートル)")]
        [SerializeField] private float pressDistance = 0.006f;

        [Tooltip("沈むのにかかる時間 (秒)")]
        [SerializeField] private float pressDuration = 0.09f;

        [Tooltip("戻るのにかかる時間 (秒)")]
        [SerializeField] private float releaseDuration = 0.16f;

        [Header("送金 (架空の銀行)")]
        [Tooltip("横取り対象の送金。空なら既定の3件(小/中/大)を使う")]
        [SerializeField] private List<HackTransferJob> transfers = new List<HackTransferJob>();

        [Tooltip("難易度ごとの設定。空なら既定値(Easy=2段/Normal=3段/Hard=4段)を使う")]
        [SerializeField] private List<HackDifficultySettings> difficulties = new List<HackDifficultySettings>();

        [Header("報酬")]
        [Tooltip("ON: 1回の ATM 起動につき成功は1度まで。ATM を閉じて入り直せばまた狙える")]
        [SerializeField] private bool oneSuccessPerVisit = true;

        [Header("ハッキング画面の大きさ・位置 (実行中の変更も即反映)")]
        [Tooltip("横幅・縦幅(キャンバス座標)。0 のままなら ATM 画面に表示中の文字の範囲へ自動で合わせる。" +
                 "起動時に実測値をログへ出すので、それを基準に数値を入れると合わせやすい")]
        [SerializeField] private Vector2 screenSize = Vector2.zero;

        [Tooltip("全体倍率。横幅・縦幅をまとめて微調整したいとき用")]
        [Range(0.2f, 3f)]
        [SerializeField] private float screenScale = 1f;

        [Tooltip("位置(キャンバス座標)。X=右が正 / Y=上が正 / Z=画面から手前が正。" +
                 "Z は ATM 画面の文字やガラスと重なってチラつくときに少しだけ手前へ出す")]
        [SerializeField] private Vector3 screenOffset = Vector3.zero;

        [Tooltip("大きさを自動計測するときの余白。1.0 で文字の範囲ぴったり、1.2 なら 2 割大きく")]
        [Range(0.5f, 2f)]
        [SerializeField] private float screenPadding = 1.05f;

        [Header("プログレスバーの位置・横幅 (実行中の変更も即反映)")]
        [Tooltip("送金一覧の各行にある「送金中」バーの位置。既定位置からのずれ")]
        [SerializeField] private Vector2 transferBarOffset = Vector2.zero;

        [Tooltip("送金一覧のバーの横幅倍率。1 で既定の幅")]
        [Range(0.1f, 2f)]
        [SerializeField] private float transferBarWidth = 1f;

        [Tooltip("送金一覧のバーの太さ倍率。1 で既定の太さ")]
        [Range(0.2f, 8f)]
        [SerializeField] private float transferBarThickness = 1f;

        [Tooltip("送金中バーの進む速さ(1秒あたりの割合)。0.03 なら約33秒で1周。" +
                 "行ごとにこの範囲でばらつかせる")]
        [SerializeField] private Vector2 transferProgressSpeedRange = new Vector2(0.02f, 0.045f);

        [Tooltip("ミニゲームのバーの位置。既定位置からのずれ")]
        [SerializeField] private Vector2 minigameBarOffset = Vector2.zero;

        [Tooltip("ミニゲームのバーの横幅倍率。1 で既定の幅。狭くするとゲームは難しくなる")]
        [Range(0.1f, 2f)]
        [SerializeField] private float minigameBarWidth = 1f;

        [Tooltip("ミニゲームのバーの太さ倍率。1 で既定の太さ")]
        [Range(0.2f, 4f)]
        [SerializeField] private float minigameBarThickness = 1f;

        [Tooltip("安全地帯(緑)の太さ倍率。1 でバーに収まる既定の太さ。見た目だけが変わる")]
        [Range(0.2f, 3f)]
        [SerializeField] private float safeZoneThickness = 1f;

        [Tooltip("安全地帯(緑)の横幅倍率。1 で難易度設定どおりの幅。" +
                 "判定の幅も同じだけ変わる(見た目と当たり判定は常に一致)ため、大きくすると易しくなる")]
        [Range(0.3f, 2.5f)]
        [SerializeField] private float safeZoneWidth = 1f;

        [Header("効果音 (未設定なら ATM 本体の音を流用)")]
        [Tooltip("トリガーをクリックした時の音")]
        [SerializeField] private AudioClip triggerSound;

        [Tooltip("ハッキング画面が起動する時の音")]
        [SerializeField] private AudioClip bootSound;

        [Tooltip("階層を突破した時の音")]
        [SerializeField] private AudioClip breachSound;

        [Tooltip("失敗した時の音")]
        [SerializeField] private AudioClip deniedSound;

        [Tooltip("送金を横取りできた時の音")]
        [SerializeField] private AudioClip authorizedSound;

        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorID = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        private ATMController _controller;
        private ATMHackingUI _ui;
        private ATMHackingMinigame _minigame;

        private Renderer _triggerRenderer;
        private List<Renderer> _outlineRenderers;
        private bool _outlineShown;
        private MaterialPropertyBlock _propertyBlock;
        private Color _originalBaseColor;
        private Color _originalEmissionColor;
        private int _baseColorProperty = -1;
        private bool _hasEmission;
        private bool _blockApplied;

        private Vector3 _triggerOriginalLocalPosition;
        private Coroutine _pressCoroutine;
        private Coroutine _flowCoroutine;

        private HackPhase _phase = HackPhase.Hidden;
        private int _selectedIndex;
        private bool _selectionConfirmed;
        private bool _stopRequested;
        private bool _cancelRequested;
        private bool _clearedAllLayers;
        private bool _successThisVisit;

        // キーボードは自前でも読むため、ATMController 経由の入力と二重にならないよう記録しておく
        private int _lastInputFrame = -1;
        private KeyRole _lastInputRole = KeyRole.Other;

        /// <summary>ハッキング画面を出している間 true。ATMController 側の入力はこちらへ回る。</summary>
        public bool IsActive => _flowCoroutine != null;

        /// <summary>トリガーが反応できる状態か。</summary>
        private bool CanTrigger => _controller != null && _controller.IsScreenActive && !IsActive
                                   && !(oneSuccessPerVisit && _successThisVisit);

        private void Awake()
        {
            _controller = GetComponent<ATMController>();
            if (_controller == null)
            {
                Debug.LogError("[ATMHackingMode] ATMController と同じオブジェクトに付けてください。", this);
                enabled = false;
                return;
            }

            if (transfers == null || transfers.Count == 0) transfers = HackDefaults.CreateTransfers();
            if (difficulties == null || difficulties.Count == 0) difficulties = HackDefaults.CreateDifficulties();

            foreach (HackTransferJob job in transfers)
            {
                if (job == null) continue;
                job.accountNumber = HackDefaults.CreateAccountNumber();
                job.progress = Random.value;
                job.progressSpeed = Random.Range(
                    Mathf.Min(transferProgressSpeedRange.x, transferProgressSpeedRange.y),
                    Mathf.Max(transferProgressSpeedRange.x, transferProgressSpeedRange.y));
            }

            ResolveTrigger();
        }

        private void OnDestroy()
        {
            RestoreTriggerMaterial();
            SetOutline(false);
        }

        private void Update()
        {
            if (_controller == null) return;

            UpdateBlink();

            // Inspector の値を毎フレーム流し込む。実行中に動かしてそのまま位置合わせできる
            ApplyScreenSettings();

            if (IsActive) HandleDirectInput();
            else HandleTriggerClick();
        }

        // --- トリガー (atm_design4) ---

        private void ResolveTrigger()
        {
            if (triggerObject == null) triggerObject = FindChildByName(transform, triggerObjectName);
            if (triggerObject == null)
            {
                Debug.LogWarning($"[ATMHackingMode] '{triggerObjectName}' が見つかりませんでした。" +
                                 "Inspector の Trigger Object に手動で割り当ててください。", this);
                return;
            }

            _triggerOriginalLocalPosition = triggerObject.localPosition;

            _triggerRenderer = triggerObject.GetComponent<Renderer>();
            if (_triggerRenderer == null) _triggerRenderer = triggerObject.GetComponentInChildren<Renderer>();

            // アウトラインは既存の仕組み（OutlineRendererFeature が読む登録リスト）に相乗りする
            if (_triggerRenderer != null)
            {
                _outlineRenderers = OutlineHighlightUtil.CreateOutlineCopies(new[] { _triggerRenderer });
            }

            EnsureCollider();
            PrepareBlinkMaterial();
        }

        /// <summary>
        /// マウスのレイが当たるようコライダーを補う。
        /// MeshCollider は元メッシュの Read/Write が無効だと実行時に割り当てられないため、
        /// メッシュの bounds から BoxCollider を作る。板状のパネルならこれで十分。
        /// </summary>
        private void EnsureCollider()
        {
            if (triggerObject.GetComponent<Collider>() != null) return;

            Bounds? localBounds = null;

            var filter = triggerObject.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                localBounds = filter.sharedMesh.bounds;
            }
            else if (_triggerRenderer != null)
            {
                // メッシュを直接持たない場合は、ワールドの AABB を自分のローカル空間へ落とし込む
                Bounds world = _triggerRenderer.bounds;
                Vector3 scale = triggerObject.lossyScale;
                var size = new Vector3(
                    Mathf.Approximately(scale.x, 0f) ? 0f : world.size.x / Mathf.Abs(scale.x),
                    Mathf.Approximately(scale.y, 0f) ? 0f : world.size.y / Mathf.Abs(scale.y),
                    Mathf.Approximately(scale.z, 0f) ? 0f : world.size.z / Mathf.Abs(scale.z));
                localBounds = new Bounds(triggerObject.InverseTransformPoint(world.center), size);
            }

            if (localBounds == null)
            {
                Debug.LogWarning($"[ATMHackingMode] '{triggerObject.name}' の大きさが分からずコライダーを作れませんでした。" +
                                 "クリックできるよう手動で Collider を付けてください。", this);
                return;
            }

            var box = triggerObject.gameObject.AddComponent<BoxCollider>();
            box.center = localBounds.Value.center;
            box.size = localBounds.Value.size;
        }

        /// <summary>
        /// 点滅に使う色の元値を控える。
        /// 描画の上書きは MaterialPropertyBlock で行う。マテリアルを複製すると
        /// ATMController 側の Emission 制御（共有マテリアルを直接書き換える方式）と衝突するため。
        /// </summary>
        private void PrepareBlinkMaterial()
        {
            if (_triggerRenderer == null) return;

            Material material = _triggerRenderer.sharedMaterial;
            if (material == null) return;

            _propertyBlock = new MaterialPropertyBlock();

            // URP Lit は _BaseColor、Standard 系は _Color と名前が違う
            if (material.HasProperty(BaseColorID)) _baseColorProperty = BaseColorID;
            else if (material.HasProperty(ColorID)) _baseColorProperty = ColorID;

            if (_baseColorProperty != -1) _originalBaseColor = material.GetColor(_baseColorProperty);

            _hasEmission = material.HasProperty(EmissionColorID);
            if (_hasEmission) _originalEmissionColor = material.GetColor(EmissionColorID);
        }

        private void RestoreTriggerMaterial()
        {
            if (_triggerRenderer != null && _blockApplied) _triggerRenderer.SetPropertyBlock(null);
            _blockApplied = false;
        }

        /// <summary>赤いアウトラインの表示切り替え。状態が変わったときだけ登録し直す。</summary>
        private void SetOutline(bool show)
        {
            if (_outlineRenderers == null || _outlineRenderers.Count == 0) return;
            if (_outlineShown == show) return;

            _outlineShown = show;
            OutlineHighlightUtil.SetActive(_outlineRenderers, show, outlineColor, outlineWidth, null);
        }

        private void UpdateBlink()
        {
            if (_propertyBlock == null || _triggerRenderer == null) return;

            bool shouldBlink = CanTrigger || (!blinkOnlyWhileScreenActive && !IsActive
                                              && !(oneSuccessPerVisit && _successThisVisit));

            // 点滅中は赤いアウトラインも出して、押せる場所だとはっきり分かるようにする
            SetOutline(shouldBlink && outlineWidth > 0f);

            if (!shouldBlink)
            {
                // 上書きを外して、ATM 本体側の見た目（Emission 制御など）へ戻す
                RestoreTriggerMaterial();
                return;
            }

            float pulse = Mathf.Clamp01(Mathf.Sin(Time.time * blinkSpeed * Mathf.PI * 2f) * 0.5f + 0.5f);

            _triggerRenderer.GetPropertyBlock(_propertyBlock);
            if (_baseColorProperty != -1)
            {
                _propertyBlock.SetColor(_baseColorProperty, Color.Lerp(_originalBaseColor, blinkColor, pulse * 0.65f));
            }
            if (_hasEmission)
            {
                _propertyBlock.SetColor(EmissionColorID,
                    Color.Lerp(_originalEmissionColor, blinkColor * blinkIntensity, pulse));
            }
            _triggerRenderer.SetPropertyBlock(_propertyBlock);
            _blockApplied = true;
        }

        private void HandleTriggerClick()
        {
            if (!CanTrigger || triggerObject == null) return;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            Camera camera = _controller.ScreenCamera;
            if (camera == null) return;

            Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 5f)) return;
            if (hit.transform != triggerObject && !hit.transform.IsChildOf(triggerObject)) return;

            PlaySe(triggerSound != null ? triggerSound : _controller.KeyClickSound);
            PressTrigger();
            StartHacking();
        }

        /// <summary>クリックされたパネルを自身のローカル Z 軸方向へ沈めて戻す。</summary>
        private void PressTrigger()
        {
            if (_pressCoroutine != null)
            {
                StopCoroutine(_pressCoroutine);
                triggerObject.localPosition = _triggerOriginalLocalPosition;
            }
            _pressCoroutine = StartCoroutine(PressRoutine());
        }

        private IEnumerator PressRoutine()
        {
            // pressDirection はパネル自身のローカル軸。localPosition(=親空間)へ足すため localRotation で変換する
            Vector3 offset = triggerObject.localRotation * (pressDirection.normalized * pressDistance);
            Vector3 pressed = _triggerOriginalLocalPosition + offset;

            yield return MoveTrigger(_triggerOriginalLocalPosition, pressed, pressDuration);
            yield return MoveTrigger(pressed, _triggerOriginalLocalPosition, releaseDuration);

            triggerObject.localPosition = _triggerOriginalLocalPosition;
            _pressCoroutine = null;
        }

        private IEnumerator MoveTrigger(Vector3 from, Vector3 to, float duration)
        {
            if (duration <= 0f)
            {
                triggerObject.localPosition = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                triggerObject.localPosition = Vector3.Lerp(from, to, Mathf.Sin(t * Mathf.PI * 0.5f));
                yield return null;
            }
            triggerObject.localPosition = to;
        }

        // --- 入力 ---

        /// <summary>ATMController から回ってくるキー入力。3Dキーパッドのクリックがここへ来る。</summary>
        public void HandleKey(KeyRole role)
        {
            if (!IsActive) return;

            // キーボードは自前でも読んでいるので、同じフレームの同じキーは 1 回だけ処理する
            if (_lastInputFrame == Time.frameCount && _lastInputRole == role) return;
            _lastInputFrame = Time.frameCount;
            _lastInputRole = role;

            switch (_phase)
            {
                case HackPhase.TransferList:
                    if (role == KeyRole.Up) MoveSelection(-1);
                    else if (role == KeyRole.Down) MoveSelection(1);
                    else if (role == KeyRole.Confirm) _selectionConfirmed = true;
                    else if (role == KeyRole.Cancel) _cancelRequested = true;
                    else if (role >= KeyRole.Num1 && role <= KeyRole.Num3) SelectDirect((int)role - 1);
                    break;

                case HackPhase.Minigame:
                    if (role == KeyRole.Confirm) _stopRequested = true;
                    else if (role == KeyRole.Cancel) _cancelRequested = true;
                    break;
            }
        }

        /// <summary>
        /// キーボードとマウスを自分で読む。
        /// ATMController 経由の入力に頼りきると、ATM 側の状態次第で届かないことがあるため、
        /// ハッキング中の操作はここだけでも完結できるようにしてある
        /// (同じフレームの同じキーは HandleKey 側と二重処理しない)。
        /// </summary>
        private void HandleDirectInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) _cancelRequested = true;

                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame
                    || keyboard.spaceKey.wasPressedThisFrame)
                {
                    HandleKey(KeyRole.Confirm);
                }

                if (keyboard.backspaceKey.wasPressedThisFrame) HandleKey(KeyRole.Cancel);
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) HandleKey(KeyRole.Up);
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) HandleKey(KeyRole.Down);

                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) HandleKey(KeyRole.Num1);
                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) HandleKey(KeyRole.Num2);
                if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) HandleKey(KeyRole.Num3);
            }

            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            // ミニゲームは左クリックでも止められる
            if (_phase == HackPhase.Minigame)
            {
                HandleKey(KeyRole.Confirm);
                return;
            }

            // 一覧では行をクリックしてそのまま開始できる
            if (_phase == HackPhase.TransferList && _ui != null)
            {
                int row = _ui.GetRowAtScreenPoint(Mouse.current.position.ReadValue(), _controller.ScreenCamera);
                if (row >= 0)
                {
                    _selectedIndex = row;
                    _ui.SetSelection(row);
                    _selectionConfirmed = true;
                }
            }
        }

        private void MoveSelection(int delta)
        {
            if (transfers.Count == 0) return;
            _selectedIndex = Mathf.Clamp(_selectedIndex + delta, 0, transfers.Count - 1);
            _ui.SetSelection(_selectedIndex);
        }

        private void SelectDirect(int index)
        {
            if (index < 0 || index >= transfers.Count) return;
            _selectedIndex = index;
            _ui.SetSelection(_selectedIndex);
            _selectionConfirmed = true;
        }

        // --- 進行 ---

        private void StartHacking()
        {
            if (IsActive) return;
            EnsureUI();
            if (_ui == null) return;

            _flowCoroutine = StartCoroutine(RunHackingFlow());
        }

        /// <summary>ATM を閉じる時など、外から強制終了させる。</summary>
        public void Abort()
        {
            if (_flowCoroutine != null)
            {
                StopCoroutine(_flowCoroutine);
                _flowCoroutine = null;
            }

            _phase = HackPhase.Hidden;
            _minigame?.ResetShake();
            _ui?.SetPhase(HackPhase.Hidden);
            if (_controller != null) _controller.SetNormalScreenVisible(true);

            // 「1回の起動につき1度だけ」の判定は ATM を閉じたときに戻す
            _successThisVisit = false;
        }

        private void EnsureUI()
        {
            if (_ui != null) return;

            Transform canvas = _controller.ScreenCanvas;
            if (canvas == null)
            {
                Debug.LogError("[ATMHackingMode] ATM の画面キャンバスが見つかりません。", this);
                return;
            }

            Vector2 size;
            Vector2 center = Vector2.zero;

            if (screenSize.sqrMagnitude > 1f)
            {
                size = screenSize;
            }
            else if (_controller.TryGetScreenTextArea(out Vector2 measured, out Vector2 measuredCenter))
            {
                // ATM 画面に出ている文字の範囲＝実際の画面の大きさとみなす。
                // キャンバスの sizeDelta(800x600) は実際の表示範囲と桁が違うので使わない
                size = measured * screenPadding;
                center = measuredCenter;
            }
            else
            {
                size = new Vector2(240f, 160f);
                Debug.LogWarning("[ATMHackingMode] ATM 画面の文字範囲を測れませんでした。" +
                                 "大きさが合わない場合は Screen Size / Screen Scale で調整してください。", this);
            }

            Debug.Log($"[ATMHackingMode] ハッキング画面: 大きさ {size} / 中心 {center}。" +
                      "Screen Size にこの値を入れて調整できます");

            _ui = new ATMHackingUI(canvas, size, center);
            _minigame = new ATMHackingMinigame(_ui);
            ApplyScreenSettings();
        }

        /// <summary>
        /// Inspector で指定した大きさ・位置を UI へ反映する。
        /// 毎フレーム呼んでいるので、実行中に数値を動かせばその場で画面が動く。
        /// </summary>
        private void ApplyScreenSettings()
        {
            if (_ui == null) return;

            // Screen Size 未指定なら、組み上げたときの大きさ(=自動計測値)のまま使う
            Vector2 target = screenSize.sqrMagnitude > 1f ? screenSize : _ui.BuiltSize;

            _ui.SetTransform(target, screenScale, screenOffset);
            _ui.SetProgressBarLayout(transferBarOffset, transferBarWidth, transferBarThickness,
                                     minigameBarOffset, minigameBarWidth, minigameBarThickness);
            _ui.SetSafeZoneThickness(safeZoneThickness);

            // 横幅は判定にも効くのでミニゲーム側に渡す。表示はそこから作られる
            if (_minigame != null) _minigame.SafeZoneWidthScale = safeZoneWidth;
        }

        private IEnumerator RunHackingFlow()
        {
            _cancelRequested = false;
            _selectedIndex = 0;
            _controller.SetNormalScreenVisible(false);

            yield return BootSequence();

            while (!_cancelRequested)
            {
                yield return SelectionPhase();
                if (_cancelRequested) break;

                HackTransferJob job = transfers[Mathf.Clamp(_selectedIndex, 0, transfers.Count - 1)];
                Debug.Log($"[ATMHackingMode] 送金 {_selectedIndex + 1} を選択: " +
                          $"{job.fromBank} → {job.toBank} {DevilCurrency.Format(job.amount)} ({job.difficulty})");
                yield return RunLayers(job);
                if (_cancelRequested) break;

                if (_clearedAllLayers)
                {
                    yield return AuthorizedSequence(job);
                    break;
                }

                yield return DeniedSequence();
            }

            yield return ExitSequence();
        }

        private IEnumerator BootSequence()
        {
            _phase = HackPhase.Boot;
            _ui.SetPhase(HackPhase.Boot);
            _ui.SetBootProgress(0f);
            _ui.SetTitle("HACKING MODE", ATMHackingUI.Red);
            _ui.SetStatus("");
            _ui.SetFooter("");

            // ATM の画面が一瞬切れる
            _ui.SetFlash(Color.black, 1f);
            yield return new WaitForSeconds(0.14f);
            _ui.SetFlash(Color.white, 1f);
            yield return new WaitForSeconds(0.04f);
            _ui.SetFlash(Color.black, 1f);
            yield return new WaitForSeconds(0.24f);

            PlaySe(bootSound != null ? bootSound : _controller.StartupSound);

            // 画面全体を赤く明滅させながら起動する
            const float duration = 1.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                _ui.SetBootProgress(t);
                _ui.ScrollStripes(elapsed * 0.4f);
                _ui.SetStatus(BootMessage(t));

                // 明滅は最初ほど強く、最後には収まる
                float blink = Mathf.Sin(elapsed * 38f) > 0f ? 1f : 0f;
                _ui.SetFlash(ATMHackingUI.Red, blink * 0.5f * (1f - t));
                yield return null;
            }

            _ui.SetBootProgress(1f);
            _ui.SetFlash(Color.clear, 0f);
        }

        private static string BootMessage(float t)
        {
            if (t < 0.3f) return "BYPASSING SECURITY LAYER...";
            if (t < 0.6f) return "INJECTING PAYLOAD...";
            if (t < 0.85f) return "SCANNING INTERBANK TRAFFIC...";
            return "ROOT ACCESS GRANTED";
        }

        private IEnumerator SelectionPhase()
        {
            _phase = HackPhase.TransferList;
            _selectionConfirmed = false;

            _ui.SetPhase(HackPhase.TransferList);
            _ui.SetTitle("HACKING MODE", ATMHackingUI.Red);
            _ui.SetStatus("INTERCEPTED TRANSFERS - SELECT TARGET");
            _ui.SetFooter("[UP/DOWN] SELECT    [ENTER] BREACH    [CANCEL] ABORT");
            _ui.BindTransfers(transfers, difficulties);
            _ui.SetSelection(_selectedIndex);

            while (!_selectionConfirmed && !_cancelRequested)
            {
                _ui.UpdateTransferProgress(transfers, Time.deltaTime);
                _ui.ScrollStripes(Time.time * 0.15f);
                yield return null;
            }
        }

        private IEnumerator RunLayers(HackTransferJob job)
        {
            _clearedAllLayers = false;
            HackDifficultySettings settings = FindSettings(job.difficulty);
            int layerCount = Mathf.Max(1, settings.LayerCount);

            _phase = HackPhase.Minigame;
            _ui.SetPhase(HackPhase.Minigame);
            _ui.SetTitle("FIREWALL BREACH", ATMHackingUI.Red);

            for (int i = 0; i < layerCount; i++)
            {
                HackLayer layer = settings.BuildLayer(i);
                _minigame.BeginLayer(layer, i, layerCount, job);

                _ui.SetStatus("STOP THE CURSOR INSIDE THE SAFE ZONE");
                _ui.SetFooter(layer.fakeZoneCount > 0
                    ? "[ENTER]/[SPACE] STOP    WARNING: DECOY ZONES ARE BLINKING"
                    : "[ENTER]/[SPACE] STOP    [CANCEL] ABORT");

                _stopRequested = false;
                while (!_stopRequested && !_cancelRequested)
                {
                    _minigame.Tick(Time.deltaTime);
                    _ui.ScrollStripes(Time.time * 0.3f);
                    yield return null;
                }

                if (_cancelRequested)
                {
                    _minigame.ResetShake();
                    yield break;
                }

                bool cleared = _minigame.IsCursorSafe();
                Debug.Log($"[ATMHackingMode] 階層 {i + 1}/{layerCount} '{layer.label}': {(cleared ? "突破" : "失敗")}");
                _minigame.ShowJudgement(cleared);
                PlaySe(cleared
                    ? (breachSound != null ? breachSound : _controller.ConfirmSound)
                    : (deniedSound != null ? deniedSound : _controller.CancelSound));

                _ui.SetStatus(cleared ? "LAYER BREACHED" : "TRACE DETECTED");
                yield return FlashScreen(cleared ? ATMHackingUI.Green : ATMHackingUI.Red, 0.45f);
                _minigame.ResetShake();

                if (!cleared) yield break;
            }

            _clearedAllLayers = true;
        }

        private IEnumerator AuthorizedSequence(HackTransferJob job)
        {
            _phase = HackPhase.Result;
            _ui.SetPhase(HackPhase.Result);
            _ui.SetTitle("TRANSFER AUTHORIZED", ATMHackingUI.Green);
            _ui.SetStatus($"{DevilCurrency.Format(job.amount)}  REROUTED FROM {job.fromBank}");
            _ui.SetFooter("");

            // 入金側(CreditCash)が ATM の成功音を鳴らすので、専用音を指定した時だけ重ねる
            PlaySe(authorizedSound);
            Award(job.amount);
            _successThisVisit = true;

            // 成功表示を点滅させる
            for (int i = 0; i < 5; i++)
            {
                _ui.SetFlash(ATMHackingUI.Green, 0.35f);
                yield return new WaitForSeconds(0.06f);
                _ui.SetFlash(Color.clear, 0f);
                yield return new WaitForSeconds(0.12f);
            }

            yield return new WaitForSeconds(1.4f);
        }

        private IEnumerator DeniedSequence()
        {
            _phase = HackPhase.Result;
            _ui.SetPhase(HackPhase.Result);
            _ui.SetTitle("ACCESS DENIED", ATMHackingUI.Red);
            _ui.SetStatus("CONNECTION TERMINATED BY HOST");
            _ui.SetFooter("");

            yield return FlashScreen(ATMHackingUI.Red, 0.7f);
            yield return new WaitForSeconds(0.6f);
        }

        private IEnumerator ExitSequence()
        {
            _ui.SetFlash(Color.black, 1f);
            yield return new WaitForSeconds(0.12f);

            _phase = HackPhase.Hidden;
            _minigame.ResetShake();
            _ui.SetPhase(HackPhase.Hidden);
            _ui.SetFlash(Color.clear, 0f);

            _controller.SetNormalScreenVisible(true);
            _flowCoroutine = null;
        }

        /// <summary>画面全体を指定色で数回点滅させる。</summary>
        private IEnumerator FlashScreen(Color color, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float blink = Mathf.Sin(elapsed * 42f) > 0f ? 1f : 0f;
                _ui.SetFlash(color, blink * 0.45f * (1f - elapsed / duration));
                yield return null;
            }
            _ui.SetFlash(Color.clear, 0f);
        }

        private HackDifficultySettings FindSettings(HackDifficulty difficulty)
        {
            for (int i = 0; i < difficulties.Count; i++)
            {
                if (difficulties[i] != null && difficulties[i].difficulty == difficulty) return difficulties[i];
            }

            Debug.LogWarning($"[ATMHackingMode] 難易度 {difficulty} の設定がないため既定値を使います。", this);
            return new HackDifficultySettings();
        }

        /// <summary>
        /// 横取りした送金額を現金として入金する。
        /// 入金処理はコイン売却と共通（ATMController.CreditCash）で、
        /// 紙幣の払い出し演出とカウントアップもそのまま流用される。
        /// </summary>
        private void Award(float amount)
        {
            if (amount <= 0f) return;
            _controller.CreditCash(amount);
        }

        private void PlaySe(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            AudioSource source = _controller.Speaker;
            if (source != null) source.PlayOneShot(clip, volume);
        }

        private static Transform FindChildByName(Transform root, string targetName)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == targetName) return child;

                Transform found = FindChildByName(child, targetName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
