using System.Collections.Generic;
using UnityEngine;

namespace App.ATM
{
    /// <summary>
    /// 「ファイアウォール突破」タイミングゲームの進行役。
    ///
    /// バーの上をカーソルが左右に往復し、安全地帯の上で止められれば突破。
    /// 階層が進むごとに、安全地帯が狭くなる・カーソルが速くなる・
    /// フェイクの安全地帯が増える・画面が揺れる。
    ///
    /// 表示は ATMHackingUI に任せ、ここでは状態の計算だけを行う。
    /// </summary>
    public class ATMHackingMinigame
    {
        /// <summary>
        /// 安全地帯どうしの中心間の最低距離。安全地帯の幅に対する倍率で決める。
        /// 近すぎると隣り合った 2 つが 1 つの広い安全地帯に見えてしまい、
        /// 真ん中で止めたのに外れる（＝理不尽な失敗）が起きるため、はっきり離す。
        /// </summary>
        private const float ZoneGapRatio = 3.2f;

        /// <summary>中心間距離の下限。安全地帯が細い階層でも見た目が繋がらないようにする。</summary>
        private const float MinimumZoneGap = 0.12f;

        /// <summary>安全地帯を端に寄せすぎないための余白。</summary>
        private const float EdgeMargin = 0.04f;

        /// <summary>フェイクの明滅の速さ(1秒あたりの周期数)。本物は点きっぱなしなので、これが見分け方になる。</summary>
        private const float FakeBlinkSpeed = 1.3f;

        /// <summary>フェイクが点いている時間の割合。</summary>
        private const float FakeBlinkOnRatio = 0.62f;

        private readonly ATMHackingUI _ui;
        private readonly List<float> _fakeCenters = new List<float>();
        private readonly List<float> _placementCandidates = new List<float>();

        private HackLayer _layer;
        private float _cursor;
        private float _direction = 1f;
        private float _safeCenter;
        private float _elapsed;

        public float CursorPosition => _cursor;
        public float SafeCenter => _safeCenter;
        public HackLayer Layer => _layer;

        /// <summary>
        /// 安全地帯の横幅倍率。Inspector から調整する用。
        /// 表示も判定もこの値から計算するので、見た目と当たり判定がずれることはない
        /// (大きくすると当然易しくなる)。
        /// </summary>
        public float SafeZoneWidthScale { get; set; } = 1f;

        /// <summary>実際に使う安全地帯の半幅。表示・判定・フェイクの間隔すべてこれを基準にする。</summary>
        private float EffectiveSafeHalfWidth =>
            Mathf.Clamp(_layer.safeHalfWidth * Mathf.Max(0.05f, SafeZoneWidthScale), 0.005f, 0.45f);

        public ATMHackingMinigame(ATMHackingUI ui)
        {
            _ui = ui;
        }

        /// <summary>1 階層ぶんの配置を決めて開始する。</summary>
        public void BeginLayer(HackLayer layer, int layerIndex, int layerCount, HackTransferJob job)
        {
            _layer = layer;
            _elapsed = 0f;

            // 端に寄りすぎない範囲で安全地帯を置く
            float halfWidth = EffectiveSafeHalfWidth;
            float margin = halfWidth + EdgeMargin;
            _safeCenter = Random.Range(margin, 1f - margin);

            PlaceFakeZones(layer, margin);

            // 開始位置と向きを毎回変えて、覚えゲーにならないようにする
            _cursor = Random.value;
            _direction = Random.value < 0.5f ? -1f : 1f;

            // 開始直後に安全地帯へ入っていると事故で成功してしまうので、離れた位置から始める
            if (Mathf.Abs(_cursor - _safeCenter) < halfWidth * 2f)
            {
                _cursor = _safeCenter > 0.5f ? 0.05f : 0.95f;
            }

            _ui.SetupMinigame(layer, layerIndex, layerCount, job);
            _ui.SetSafeZoneColor(ATMHackingUI.Green);
            _ui.SetCursorColor(Color.white);
            PushToUI();
        }

        /// <summary>
        /// 本物から十分離れた位置にフェイクを並べる。
        /// 置ける位置を先に列挙してから選ぶので、重なったり隣接したりすることが原理的に起きない。
        /// 置き切れない場合はフェイクの数が減るだけで、位置が破綻することはない。
        /// </summary>
        private void PlaceFakeZones(HackLayer layer, float margin)
        {
            _fakeCenters.Clear();
            if (layer.fakeZoneCount <= 0) return;

            float minimumGap = Mathf.Max(EffectiveSafeHalfWidth * ZoneGapRatio, MinimumZoneGap);

            // 本物から十分離れた位置だけを候補にする
            _placementCandidates.Clear();
            for (float position = margin; position <= 1f - margin + 0.0001f; position += 0.02f)
            {
                if (Mathf.Abs(position - _safeCenter) >= minimumGap) _placementCandidates.Add(position);
            }

            for (int i = 0; i < layer.fakeZoneCount && _placementCandidates.Count > 0; i++)
            {
                float chosen = _placementCandidates[Random.Range(0, _placementCandidates.Count)];
                _fakeCenters.Add(chosen);

                // 選んだ位置の近くは候補から外し、フェイクどうしも離す
                for (int j = _placementCandidates.Count - 1; j >= 0; j--)
                {
                    if (Mathf.Abs(_placementCandidates[j] - chosen) < minimumGap) _placementCandidates.RemoveAt(j);
                }
            }
        }

        /// <summary>カーソルを進め、表示を更新する。</summary>
        public void Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            _cursor += _direction * _layer.cursorSpeed * deltaTime;

            // 端で折り返す。速い階層では 1 フレームで大きく動くため、はみ出した分を折り返して戻す
            if (_cursor > 1f)
            {
                _cursor = 2f - _cursor;
                _direction = -1f;
            }
            else if (_cursor < 0f)
            {
                _cursor = -_cursor;
                _direction = 1f;
            }
            _cursor = Mathf.Clamp01(_cursor);

            PushToUI();
            _ui.SetShake(CalculateShake());
        }

        private void PushToUI()
        {
            // 本物は点きっぱなし。フェイクははっきり消える明滅にして、見分けられる余地を残す
            float cycle = Mathf.Repeat(_elapsed * FakeBlinkSpeed, 1f);
            float alpha = cycle < FakeBlinkOnRatio ? 1f : 0f;
            _ui.UpdateMinigame(_cursor, _safeCenter, _fakeCenters, alpha, EffectiveSafeHalfWidth);
        }

        private Vector2 CalculateShake()
        {
            if (_layer.shakeAmount <= 0f) return Vector2.zero;

            return new Vector2(
                (Mathf.PerlinNoise(_elapsed * 14f, 0f) - 0.5f) * 2f * _layer.shakeAmount,
                (Mathf.PerlinNoise(0f, _elapsed * 14f) - 0.5f) * 2f * _layer.shakeAmount);
        }

        /// <summary>
        /// 今の位置が安全地帯に入っているか。
        /// 判定に使う幅は表示と同じ EffectiveSafeHalfWidth なので、見えている緑の範囲＝成功範囲。
        /// </summary>
        public bool IsCursorSafe()
        {
            return Mathf.Abs(_cursor - _safeCenter) <= EffectiveSafeHalfWidth;
        }

        /// <summary>判定結果を色で見せる。成功なら白、失敗なら赤に染める。</summary>
        public void ShowJudgement(bool success)
        {
            _ui.SetSafeZoneColor(success ? Color.white : ATMHackingUI.DarkRed);
            _ui.SetCursorColor(success ? ATMHackingUI.Green : ATMHackingUI.Red);
        }

        /// <summary>揺れを戻す。階層終了時に必ず呼ぶ。</summary>
        public void ResetShake()
        {
            _ui.SetShake(Vector2.zero);
        }
    }
}
