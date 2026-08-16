using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.ATM
{
    /// <summary>ハッキング画面の表示段階。</summary>
    public enum HackPhase
    {
        Hidden,
        Boot,
        TransferList,
        Minigame,
        Result
    }

    /// <summary>
    /// ATM 画面に重ねるハッキング用 UI。ATM の WorldSpace キャンバス配下に実行時生成する。
    ///
    /// 画面は「黒背景 + 上下の赤黒ストライプ + 赤い悪魔 + 赤文字」で構成し、
    /// 段階(HackPhase)に応じて中身だけ差し替える。
    ///
    /// 大きさについて:
    /// レイアウトは 800x600 を基準に組み、実際の画面サイズとの比 (_uiScale) を
    /// 文字サイズや余白にも掛ける。これにより、渡された screenSize がいくつでも
    /// 見た目の比率が変わらずそのまま縮む。
    ///
    /// 入力はキーパッド／キーボードで行うため、UI 側は一切レイキャストを受けない
    /// (受けてしまうと ATM 本体の 3D ボタンのクリック判定が塞がれる)。
    /// </summary>
    public class ATMHackingUI
    {
        /// <summary>レイアウトを組んだときの基準サイズ。</summary>
        private static readonly Vector2 DesignSize = new Vector2(800f, 600f);

        // 配色。ATM 既存画面の緑に対して、ハッキング中は赤で統一する
        public static readonly Color Red = new Color(1f, 0.16f, 0.16f, 1f);
        public static readonly Color DarkRed = new Color(0.45f, 0.03f, 0.03f, 1f);
        public static readonly Color Black = new Color(0.02f, 0.02f, 0.02f, 1f);
        public static readonly Color Green = new Color(0.2f, 1f, 0.4f, 1f);
        private static readonly Color DimRed = new Color(0.75f, 0.25f, 0.25f, 1f);

        /// <summary>状態行の既定色。YAML で色を指定しなかった時のフォールバックに使う。</summary>
        public static Color DimRedPublic => DimRed;
        private static readonly Color SpaceButtonNormal = new Color(0.55f, 0.04f, 0.04f, 0.92f);
        private static readonly Color SpaceButtonPressed = new Color(1f, 0.35f, 0.35f, 1f);

        private const int TransferRowCount = 3;
        private const int FakeZonePoolSize = 4;

        private readonly Vector2 _screenSize;
        private readonly float _uiScale;
        private readonly Sprite _white;

        private readonly GameObject _root;
        private readonly RectTransform _rootRect;
        private readonly Vector2 _rootBasePosition;

        // 位置は「計測した中心 + Inspector の調整 + 揺れ」の合成で決める
        private Vector3 _userOffset;
        private Vector2 _shakeOffset;
        private float _appliedScale = 1f;

        // プログレスバーの既定位置・既定幅・既定の太さ。Inspector の調整はここを基準に掛け合わせる
        private readonly Vector2[] _transferBarBasePositions = new Vector2[TransferRowCount];
        private readonly float[] _transferBarBaseWidths = new float[TransferRowCount];
        private readonly float[] _transferBarBaseHeights = new float[TransferRowCount];
        private Vector2 _minigameBarBasePosition;
        private float _minigameBarBaseWidth;
        private float _minigameBarBaseHeight;
        private float _cursorOverhang;

        // ミニゲームのバー幅を変えたときに安全地帯の大きさを追従させるため覚えておく
        private float _currentSafeHalfWidth = 0.1f;

        // 安全地帯(緑)の太さ倍率。1 でバーに収まる既定の太さ
        private float _safeZoneThickness = 1f;

        private readonly Image _background;
        private readonly RawImage _scanlines;
        private readonly RawImage _stripeTop;
        private readonly RawImage _stripeBottom;
        private readonly Image _devil;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _status;
        private readonly TextMeshProUGUI _footer;
        private readonly Image _flash;

        private readonly GameObject _listGroup;
        private readonly TransferRow[] _rows = new TransferRow[TransferRowCount];

        private readonly GameObject _gameGroup;
        private readonly Image _barTrack;
        private readonly Image _safeZone;
        private readonly Image[] _fakeZones = new Image[FakeZonePoolSize];
        private readonly Image _cursor;
        private Image _spaceButton;
        private TextMeshProUGUI _spaceButtonLabel;
        private TextMeshProUGUI _timerText;

        private float _barWidth;

        // YAML から % 指定で大きさを変えられるよう、組み上げ時の文字サイズを控えておく
        private readonly float _baseTitleFontSize;
        private readonly float _baseStatusFontSize;

        // 見出しの配置。一覧やミニゲームでは上部、起動演出と結果表示では画面中央に大きく出す
        private readonly Vector2 _titleTopPosition;
        private readonly Vector2 _statusTopPosition;
        private readonly Vector2 _titleCenterPosition;
        private readonly Vector2 _statusCenterPosition;

        /// <summary>中央寄せ時に見出しを何倍にするか。YAML の size はこの上に掛かる。</summary>
        private const float CenteredTitleScale = 1.6f;
        private const float CenteredStatusScale = 1.35f;

        private bool _headlineCentered;
        private Vector2 _titleUserOffset;
        private Vector2 _statusUserOffset;
        private float _titleSizePercent = 100f;
        private float _statusSizePercent = 100f;

        // 残り時間の文字サイズ。基準サイズは組み上げ時に控え、% で拡大縮小する
        private float _baseTimerFontSize;
        private float _timerSizePercent = 100f;

        public GameObject Root => _root;

        /// <summary>レイアウトを組んだときの大きさ。ここからの拡大縮小で最終的な大きさを決める。</summary>
        public Vector2 BuiltSize => _screenSize;

        /// <summary>送金一覧の 1 行分の部品。</summary>
        private class TransferRow
        {
            public GameObject go;
            public Image background;
            public TextMeshProUGUI marker;
            public TextMeshProUGUI route;
            public TextMeshProUGUI amount;
            public TextMeshProUGUI tag;
            public TextMeshProUGUI status;
            public Image progressTrack;
            public Image progressFill;
        }

        public ATMHackingUI(Transform parent, Vector2 screenSize, Vector2 screenCenter)
        {
            _screenSize = screenSize;

            // 基準レイアウトからの縮尺。幅と高さで比が違う場合は、はみ出さないよう小さい方に合わせる
            _uiScale = Mathf.Min(_screenSize.x / DesignSize.x, _screenSize.y / DesignSize.y);
            _white = ATMHackingArt.CreateWhite();

            _root = new GameObject("HackingScreen", typeof(RectTransform));
            _root.transform.SetParent(parent, false);
            _rootRect = _root.GetComponent<RectTransform>();
            _rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            _rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            _rootRect.pivot = new Vector2(0.5f, 0.5f);
            _rootRect.anchoredPosition = screenCenter;
            _rootRect.sizeDelta = _screenSize;
            _rootBasePosition = _rootRect.anchoredPosition;

            float halfHeight = _screenSize.y * 0.5f;

            _background = CreateImage("Background", _root.transform, Vector2.zero, _screenSize, Black);

            _scanlines = CreateRaw("Scanlines", _root.transform, Vector2.zero, _screenSize,
                                   ATMHackingArt.CreateScanlines(new Color(0f, 0f, 0f, 0.35f)));
            _scanlines.uvRect = new Rect(0f, 0f, 1f, _screenSize.y / S(4f));

            // 赤い悪魔は背景の透かしとして中央に置く
            _devil = CreateImage("Devil", _root.transform, new Vector2(0f, -S(10f)),
                                 new Vector2(_screenSize.y * 0.62f, _screenSize.y * 0.62f), new Color(1f, 0.1f, 0.1f, 0.16f));
            _devil.sprite = ATMHackingArt.CreateDevilSilhouette(Color.white);
            _devil.preserveAspect = true;

            Texture2D stripes = ATMHackingArt.CreateDiagonalStripes(new Color(0.85f, 0.05f, 0.05f, 1f), Black);
            float bandHeight = _screenSize.y * 0.075f;
            _stripeTop = CreateRaw("StripeTop", _root.transform,
                                   new Vector2(0f, halfHeight - bandHeight * 0.5f), new Vector2(_screenSize.x, bandHeight), stripes);
            _stripeBottom = CreateRaw("StripeBottom", _root.transform,
                                      new Vector2(0f, -halfHeight + bandHeight * 0.5f), new Vector2(_screenSize.x, bandHeight), stripes);
            SetStripeTiling(_stripeTop, bandHeight);
            SetStripeTiling(_stripeBottom, bandHeight);

            _title = CreateText("Title", _root.transform, new Vector2(0f, halfHeight - bandHeight - S(46f)),
                                new Vector2(_screenSize.x, S(76f)), S(62f), Red, TextAlignmentOptions.Center);
            _title.fontStyle = FontStyles.Bold;
            _title.text = "HACKING MODE";

            _status = CreateText("Status", _root.transform, new Vector2(0f, halfHeight - bandHeight - S(104f)),
                                 new Vector2(_screenSize.x, S(34f)), S(24f), DimRed, TextAlignmentOptions.Center);

            _footer = CreateText("Footer", _root.transform, new Vector2(0f, -halfHeight + bandHeight + S(30f)),
                                 new Vector2(_screenSize.x, S(32f)), S(20f), DimRed, TextAlignmentOptions.Center);

            _baseTitleFontSize = _title.fontSize;
            _baseStatusFontSize = _status.fontSize;

            _titleTopPosition = _title.rectTransform.anchoredPosition;
            _statusTopPosition = _status.rectTransform.anchoredPosition;
            _titleCenterPosition = new Vector2(0f, _screenSize.y * 0.09f);
            _statusCenterPosition = new Vector2(0f, -_screenSize.y * 0.07f);

            _listGroup = BuildTransferList(halfHeight, bandHeight);
            _gameGroup = BuildMinigame(out _barTrack, out _safeZone, out _cursor);

            // 画面全体の点滅用。最前面に置く
            _flash = CreateImage("Flash", _root.transform, Vector2.zero, _screenSize, new Color(1f, 1f, 1f, 0f));

            SetPhase(HackPhase.Hidden);
        }

        /// <summary>基準レイアウト(800x600)の数値を、実際の画面サイズへ換算する。</summary>
        private float S(float designValue) => designValue * _uiScale;

        // --- 構築 ---

        private GameObject BuildTransferList(float halfHeight, float bandHeight)
        {
            var group = new GameObject("TransferList", typeof(RectTransform));
            group.transform.SetParent(_root.transform, false);
            var groupRect = group.GetComponent<RectTransform>();
            groupRect.anchorMin = new Vector2(0.5f, 0.5f);
            groupRect.anchorMax = new Vector2(0.5f, 0.5f);
            groupRect.anchoredPosition = Vector2.zero;
            groupRect.sizeDelta = _screenSize;

            float rowWidth = _screenSize.x * 0.82f;
            float rowHeight = _screenSize.y * 0.155f;
            float spacing = _screenSize.y * 0.028f;
            float top = halfHeight - bandHeight - S(140f);

            for (int i = 0; i < TransferRowCount; i++)
            {
                float y = top - rowHeight * 0.5f - i * (rowHeight + spacing);
                _rows[i] = BuildTransferRow(group.transform, i, new Vector2(0f, y), new Vector2(rowWidth, rowHeight));
            }

            return group;
        }

        private TransferRow BuildTransferRow(Transform parent, int index, Vector2 position, Vector2 size)
        {
            var row = new TransferRow();

            row.go = new GameObject($"Row{index}", typeof(RectTransform));
            row.go.transform.SetParent(parent, false);
            var rect = row.go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            row.background = CreateImage("Background", row.go.transform, Vector2.zero, size, new Color(0.35f, 0.02f, 0.02f, 0.35f));

            float half = size.x * 0.5f;
            row.marker = CreateText("Marker", row.go.transform, new Vector2(-half + S(22f), 0f), new Vector2(S(40f), size.y),
                                    S(30f), Red, TextAlignmentOptions.Center);
            row.marker.text = "";

            row.route = CreateText("Route", row.go.transform, new Vector2(S(14f), size.y * 0.28f),
                                   new Vector2(size.x - S(80f), S(28f)), S(21f), DimRed, TextAlignmentOptions.Left);

            row.amount = CreateText("Amount", row.go.transform, new Vector2(S(14f), -size.y * 0.06f),
                                    new Vector2(size.x - S(200f), S(40f)), S(34f), Red, TextAlignmentOptions.Left);
            row.amount.fontStyle = FontStyles.Bold;

            row.tag = CreateText("Tag", row.go.transform, new Vector2(half - S(90f), -size.y * 0.06f),
                                 new Vector2(S(150f), S(34f)), S(24f), Red, TextAlignmentOptions.Right);
            row.tag.fontStyle = FontStyles.Bold;

            row.status = CreateText("Status", row.go.transform, new Vector2(half - S(100f), size.y * 0.28f),
                                    new Vector2(S(190f), S(24f)), S(16f), DimRed, TextAlignmentOptions.Right);

            float barWidth = size.x - S(60f);
            float barHeight = S(7f);
            var barPosition = new Vector2(0f, -size.y * 0.34f);
            _transferBarBasePositions[index] = barPosition;
            _transferBarBaseWidths[index] = barWidth;
            _transferBarBaseHeights[index] = barHeight;

            row.progressTrack = CreateImage("ProgressTrack", row.go.transform, barPosition,
                                            new Vector2(barWidth, barHeight), new Color(0.3f, 0.06f, 0.06f, 0.9f));
            row.progressFill = CreateImage("ProgressFill", row.progressTrack.transform, Vector2.zero,
                                           new Vector2(barWidth, barHeight), Red);

            // 左端を固定して幅だけ変えたいので、フィルだけ左寄せにする
            var fillRect = row.progressFill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = new Vector2(-barWidth * 0.5f, 0f);

            return row;
        }

        private GameObject BuildMinigame(out Image track, out Image safeZone, out Image cursor)
        {
            var group = new GameObject("Minigame", typeof(RectTransform));
            group.transform.SetParent(_root.transform, false);
            var groupRect = group.GetComponent<RectTransform>();
            groupRect.anchorMin = new Vector2(0.5f, 0.5f);
            groupRect.anchorMax = new Vector2(0.5f, 0.5f);
            groupRect.anchoredPosition = Vector2.zero;
            groupRect.sizeDelta = _screenSize;

            // 見出し(FIREWALL BREACH)と階層表示は共通の Title / Status を使う。
            // ここではその下に「残り時間 → バー → SPACE BAR」を並べる
            _timerText = CreateText("Timer", group.transform, new Vector2(0f, _screenSize.y * 0.045f),
                                    new Vector2(_screenSize.x, S(60f)), S(40f), Color.white, TextAlignmentOptions.Center);
            _timerText.fontStyle = FontStyles.Bold;
            _baseTimerFontSize = _timerText.fontSize;

            _barWidth = _screenSize.x * 0.8f;
            float barHeight = _screenSize.y * 0.1f;
            var barPos = new Vector2(0f, -_screenSize.y * 0.09f);
            _minigameBarBasePosition = barPos;
            _minigameBarBaseWidth = _barWidth;
            _minigameBarBaseHeight = barHeight;
            _cursorOverhang = S(16f);

            track = CreateImage("BarTrack", group.transform, barPos, new Vector2(_barWidth, barHeight),
                                new Color(0.14f, 0.02f, 0.02f, 0.95f));

            safeZone = CreateImage("SafeZone", track.transform, Vector2.zero,
                                   new Vector2(S(60f), barHeight - S(8f)), Green);

            for (int i = 0; i < FakeZonePoolSize; i++)
            {
                _fakeZones[i] = CreateImage($"FakeZone{i}", track.transform, Vector2.zero,
                                            new Vector2(S(60f), barHeight - S(8f)), Green);
                _fakeZones[i].gameObject.SetActive(false);
            }

            cursor = CreateImage("Cursor", track.transform, Vector2.zero,
                                 new Vector2(S(7f), barHeight + S(16f)), Color.white);

            // バーの下に押しボタン。マウスクリックでも SPACE キーでも止められる
            _spaceButton = CreateImage("SpaceButton", group.transform, new Vector2(0f, -_screenSize.y * 0.27f),
                                       new Vector2(S(300f), S(62f)), SpaceButtonNormal);
            _spaceButtonLabel = CreateText("Label", _spaceButton.transform, Vector2.zero,
                                           new Vector2(S(300f), S(62f)), S(28f), Color.white, TextAlignmentOptions.Center);
            _spaceButtonLabel.fontStyle = FontStyles.Bold;
            _spaceButtonLabel.text = "SPACE BAR";

            return group;
        }

        // --- 表示切り替え ---

        public void SetPhase(HackPhase phase)
        {
            bool visible = phase != HackPhase.Hidden;
            _root.SetActive(visible);
            if (!visible) return;

            _listGroup.SetActive(phase == HackPhase.TransferList);
            _gameGroup.SetActive(phase == HackPhase.Minigame);
            _devil.gameObject.SetActive(true);
        }

        /// <summary>
        /// 画面の大きさと位置を設定する。実行中に呼んでも即反映されるので、
        /// Inspector で数値を動かしながら ATM 画面へ合わせられる。
        /// </summary>
        /// <param name="targetSize">最終的な横幅・縦幅(キャンバス座標)</param>
        /// <param name="uniformScale">全体倍率。微調整用</param>
        /// <param name="offset">位置。Z は画面から手前(+)/奥(-)</param>
        public void SetTransform(Vector2 targetSize, float uniformScale, Vector3 offset)
        {
            uniformScale = Mathf.Max(0.01f, uniformScale);

            // 組み上げた大きさから目標の大きさへ伸縮させる。横と縦を別々に指定できる
            float scaleX = Mathf.Max(0.01f, targetSize.x / Mathf.Max(1f, _screenSize.x)) * uniformScale;
            float scaleY = Mathf.Max(0.01f, targetSize.y / Mathf.Max(1f, _screenSize.y)) * uniformScale;
            _root.transform.localScale = new Vector3(scaleX, scaleY, 1f);

            // 揺れの量も見た目の大きさに合わせる
            _appliedScale = Mathf.Min(scaleX, scaleY);

            _userOffset = offset;
            ApplyPosition();
        }

        /// <summary>
        /// プログレスバーの位置と横幅を調整する。位置は既定位置からのずれ、幅は既定幅に対する倍率。
        /// </summary>
        public void SetProgressBarLayout(Vector2 transferBarOffset, float transferBarWidthScale, float transferBarThickness,
                                         Vector2 minigameBarOffset, float minigameBarWidthScale, float minigameBarThickness)
        {
            transferBarWidthScale = Mathf.Max(0.01f, transferBarWidthScale);
            minigameBarWidthScale = Mathf.Max(0.01f, minigameBarWidthScale);
            transferBarThickness = Mathf.Max(0.01f, transferBarThickness);
            minigameBarThickness = Mathf.Max(0.01f, minigameBarThickness);

            for (int i = 0; i < TransferRowCount; i++)
            {
                if (_rows[i]?.progressTrack == null) continue;

                RectTransform track = _rows[i].progressTrack.rectTransform;
                float width = _transferBarBaseWidths[i] * transferBarWidthScale;
                float height = _transferBarBaseHeights[i] * transferBarThickness;
                track.anchoredPosition = _transferBarBasePositions[i] + transferBarOffset;
                track.sizeDelta = new Vector2(width, height);

                // フィルは左端固定。横幅は UpdateTransferProgress が進捗から毎フレーム決める
                RectTransform fill = _rows[i].progressFill.rectTransform;
                fill.anchoredPosition = new Vector2(-width * 0.5f, 0f);
                fill.sizeDelta = new Vector2(fill.sizeDelta.x, height);
            }

            if (_barTrack != null)
            {
                _barWidth = _minigameBarBaseWidth * minigameBarWidthScale;
                float height = _minigameBarBaseHeight * minigameBarThickness;

                _barTrack.rectTransform.anchoredPosition = _minigameBarBasePosition + minigameBarOffset;
                _barTrack.rectTransform.sizeDelta = new Vector2(_barWidth, height);
                ApplyZoneSizes();
            }
        }

        /// <summary>安全地帯(緑)の太さを変える。1 でバーに収まる既定の太さ。</summary>
        public void SetSafeZoneThickness(float thickness)
        {
            thickness = Mathf.Max(0.05f, thickness);
            if (Mathf.Approximately(_safeZoneThickness, thickness)) return;

            _safeZoneThickness = thickness;
            ApplyZoneSizes();
        }

        /// <summary>安全地帯・フェイク・カーソルの大きさをバーの大きさに合わせる。</summary>
        private void ApplyZoneSizes()
        {
            float barHeight = _barTrack.rectTransform.sizeDelta.y;
            float zoneHeight = Mathf.Max(1f, (barHeight - S(8f)) * _safeZoneThickness);
            var zoneSize = new Vector2(_barWidth * _currentSafeHalfWidth * 2f, zoneHeight);

            _safeZone.rectTransform.sizeDelta = zoneSize;
            for (int i = 0; i < FakeZonePoolSize; i++)
            {
                _fakeZones[i].rectTransform.sizeDelta = zoneSize;
            }

            // カーソルはバーより少し上下にはみ出させて見やすくする
            _cursor.rectTransform.sizeDelta = new Vector2(_cursor.rectTransform.sizeDelta.x, barHeight + _cursorOverhang);
        }

        private void ApplyPosition()
        {
            _rootRect.anchoredPosition3D = new Vector3(
                _rootBasePosition.x + _userOffset.x + _shakeOffset.x,
                _rootBasePosition.y + _userOffset.y + _shakeOffset.y,
                _userOffset.z);
        }

        /// <summary>
        /// 大見出しを設定する。sizePercent は組み上げ時の文字サイズに対する割合で、
        /// 100 なら既定のまま。YAML から大きさを指定できるようにするために受け取る。
        /// </summary>
        public void SetTitle(string text, Color color, float sizePercent = 100f)
        {
            _title.text = text;
            _title.color = color;
            _titleSizePercent = Mathf.Max(1f, sizePercent);
            ApplyHeadlineStyle();
        }

        /// <summary>状態行を設定する。色と大きさは既定へ戻る。</summary>
        public void SetStatus(string text) => SetStatus(text, DimRed);

        /// <summary>色と大きさも指定して状態行を設定する。</summary>
        public void SetStatus(string text, Color color, float sizePercent = 100f)
        {
            _status.text = text;
            _status.color = color;
            _statusSizePercent = Mathf.Max(1f, sizePercent);
            ApplyHeadlineStyle();
        }

        /// <summary>
        /// 見出しの並べ方を切り替える。
        /// centered=true で画面中央に大きく出す（起動演出・結果表示用）。false は従来どおり上部。
        /// offset は YAML の x/y による微調整量で、基準レイアウト(800x600)の単位で渡す。
        /// </summary>
        public void SetHeadlineLayout(bool centered, Vector2 titleOffset, Vector2 statusOffset)
        {
            _headlineCentered = centered;
            _titleUserOffset = titleOffset;
            _statusUserOffset = statusOffset;
            ApplyHeadlineStyle();
        }

        private void ApplyHeadlineStyle()
        {
            float titleScale = _headlineCentered ? CenteredTitleScale : 1f;
            float statusScale = _headlineCentered ? CenteredStatusScale : 1f;

            _title.fontSize = _baseTitleFontSize * titleScale * _titleSizePercent * 0.01f;
            _status.fontSize = _baseStatusFontSize * statusScale * _statusSizePercent * 0.01f;

            Vector2 titleBase = _headlineCentered ? _titleCenterPosition : _titleTopPosition;
            Vector2 statusBase = _headlineCentered ? _statusCenterPosition : _statusTopPosition;

            _title.rectTransform.anchoredPosition = titleBase + _titleUserOffset * _uiScale;
            _status.rectTransform.anchoredPosition = statusBase + _statusUserOffset * _uiScale;
        }

        public void SetFooter(string text) => _footer.text = text;

        /// <summary>画面全体の点滅。alpha 0 で消える。</summary>
        public void SetFlash(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            _flash.color = color;
        }

        /// <summary>起動演出用。0 で真っ暗、1 で通常表示。</summary>
        public void SetBootProgress(float t)
        {
            t = Mathf.Clamp01(t);
            _title.gameObject.SetActive(t > 0.15f);
            _stripeTop.gameObject.SetActive(t > 0.35f);
            _stripeBottom.gameObject.SetActive(t > 0.35f);
            _devil.gameObject.SetActive(t > 0.55f);

            var devilColor = _devil.color;
            devilColor.a = 0.16f * Mathf.InverseLerp(0.55f, 1f, t);
            _devil.color = devilColor;
        }

        /// <summary>画面の揺れ。ミニゲーム終盤で使う。量は基準レイアウト基準で渡す。</summary>
        public void SetShake(Vector2 offset)
        {
            _shakeOffset = offset * (_uiScale * _appliedScale);
            ApplyPosition();
        }

        /// <summary>ストライプを流す。見た目の「動いている感」用。</summary>
        public void ScrollStripes(float offset)
        {
            var top = _stripeTop.uvRect;
            top.x = offset;
            _stripeTop.uvRect = top;

            var bottom = _stripeBottom.uvRect;
            bottom.x = -offset;
            _stripeBottom.uvRect = bottom;
        }

        // --- 送金一覧 ---

        public void BindTransfers(IList<HackTransferJob> jobs, IList<HackDifficultySettings> difficulties)
        {
            for (int i = 0; i < TransferRowCount; i++)
            {
                bool used = jobs != null && i < jobs.Count;
                _rows[i].go.SetActive(used);
                if (!used) continue;

                HackTransferJob job = jobs[i];
                _rows[i].route.text = $"{job.fromBank}  >>  {job.toBank}";
                _rows[i].amount.text = DevilCurrency.Format(job.amount);
                _rows[i].tag.text = FindLabel(difficulties, job.difficulty);
            }
        }

        private static string FindLabel(IList<HackDifficultySettings> difficulties, HackDifficulty difficulty)
        {
            if (difficulties != null)
            {
                for (int i = 0; i < difficulties.Count; i++)
                {
                    if (difficulties[i] != null && difficulties[i].difficulty == difficulty) return difficulties[i].label;
                }
            }
            return difficulty.ToString().ToUpperInvariant();
        }

        /// <summary>
        /// 画面座標がどの送金行の上にあるかを返す。無ければ -1。
        /// UI 側は raycastTarget を切ってあるので、EventSystem を使わず自前で判定する。
        /// </summary>
        public int GetRowAtScreenPoint(Vector2 screenPoint, Camera camera)
        {
            for (int i = 0; i < TransferRowCount; i++)
            {
                if (_rows[i] == null || !_rows[i].go.activeSelf) continue;

                var rect = (RectTransform)_rows[i].go.transform;
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, camera)) return i;
            }
            return -1;
        }

        public void SetSelection(int index)
        {
            for (int i = 0; i < TransferRowCount; i++)
            {
                bool selected = i == index;
                _rows[i].marker.text = selected ? ">" : "";
                _rows[i].background.color = selected
                    ? new Color(0.62f, 0.05f, 0.05f, 0.55f)
                    : new Color(0.35f, 0.02f, 0.02f, 0.35f);
                _rows[i].amount.color = selected ? Color.white : Red;
            }
        }

        /// <summary>送金中プログレスバーを進める。一覧を出している間ずっと流し続ける。</summary>
        public void UpdateTransferProgress(IList<HackTransferJob> jobs, float deltaTime)
        {
            if (jobs == null) return;

            for (int i = 0; i < TransferRowCount && i < jobs.Count; i++)
            {
                HackTransferJob job = jobs[i];
                job.progress += job.progressSpeed * deltaTime;
                if (job.progress > 1f) job.progress -= 1f;

                float width = _rows[i].progressTrack.rectTransform.sizeDelta.x;
                var size = _rows[i].progressFill.rectTransform.sizeDelta;
                size.x = width * job.progress;
                _rows[i].progressFill.rectTransform.sizeDelta = size;

                _rows[i].status.text = $"TRANSFERRING {Mathf.FloorToInt(job.progress * 100f):00}%";
            }
        }

        // --- ミニゲーム ---

        /// <summary>ステージ開始時の見た目を整える。文字は Title / Status / Timer 側で出す。</summary>
        public void SetupMinigame(HackLayer layer)
        {
            // 実際の幅は UpdateMinigame が毎フレーム渡してくる（判定と同じ値）
            ApplyZoneSizes();
        }

        /// <summary>
        /// 安全地帯・フェイク・カーソルの位置を反映する。位置はいずれも 0-1 の正規化値。
        /// safeHalfWidth は判定に使っている値をそのまま受け取り、表示と判定を必ず一致させる。
        /// </summary>
        public void UpdateMinigame(float cursor, float safeCenter, IList<float> fakeCenters, float fakeAlpha, float safeHalfWidth)
        {
            if (!Mathf.Approximately(_currentSafeHalfWidth, safeHalfWidth))
            {
                _currentSafeHalfWidth = safeHalfWidth;
                ApplyZoneSizes();
            }

            _cursor.rectTransform.anchoredPosition = new Vector2(ToBarX(cursor), 0f);
            _safeZone.rectTransform.anchoredPosition = new Vector2(ToBarX(safeCenter), 0f);

            // 実際に配置できたフェイクの数だけ出す（置き場所が足りず減ることがある）
            int fakeCount = fakeCenters != null ? fakeCenters.Count : 0;
            for (int i = 0; i < FakeZonePoolSize; i++)
            {
                bool used = i < fakeCount;
                if (_fakeZones[i].gameObject.activeSelf != used) _fakeZones[i].gameObject.SetActive(used);
                if (!used) continue;

                _fakeZones[i].rectTransform.anchoredPosition = new Vector2(ToBarX(fakeCenters[i]), 0f);

                // フェイクは明滅する。ここが本物との唯一の見分け方になる
                var color = Green;
                color.a = fakeAlpha;
                _fakeZones[i].color = color;
            }
        }

        /// <summary>画面座標が SPACE BAR ボタンの上にあるか。</summary>
        public bool IsPointOnSpaceButton(Vector2 screenPoint, Camera camera)
        {
            if (_spaceButton == null || !_spaceButton.gameObject.activeInHierarchy) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(_spaceButton.rectTransform, screenPoint, camera);
        }

        /// <summary>SPACE BAR ボタンの押し込み表示。キー入力でも光らせる。</summary>
        public void SetSpaceButtonPressed(bool pressed)
        {
            if (_spaceButton == null) return;

            _spaceButton.color = pressed ? SpaceButtonPressed : SpaceButtonNormal;
            if (_spaceButtonLabel != null) _spaceButtonLabel.color = pressed ? Color.black : Color.white;
        }

        /// <summary>
        /// 残り時間の表示を更新する。limit が 0 以下なら制限なしとして非表示にする。
        /// 残りが少なくなるほど赤くして、点滅で危険を知らせる。
        /// </summary>
        /// <summary>残り時間の文字の大きさ(%)。100 で組み上げ時の既定サイズ。</summary>
        public void SetTimerSize(float sizePercent)
        {
            _timerSizePercent = Mathf.Max(1f, sizePercent);
            if (_timerText != null) _timerText.fontSize = _baseTimerFontSize * _timerSizePercent * 0.01f;
        }

        public void SetTimer(float remaining, float limit)
        {
            if (_timerText == null) return;

            if (limit <= 0f)
            {
                if (_timerText.gameObject.activeSelf) _timerText.gameObject.SetActive(false);
                return;
            }

            if (!_timerText.gameObject.activeSelf) _timerText.gameObject.SetActive(true);

            remaining = Mathf.Max(0f, remaining);
            _timerText.text = $"TIME  {remaining:0.0}";

            // 残り3割を切ったら赤く点滅させる
            float ratio = remaining / limit;
            if (ratio > 0.3f)
            {
                _timerText.color = Color.white;
                return;
            }

            bool on = Mathf.Repeat(Time.time * 6f, 1f) < 0.5f;
            _timerText.color = on ? Red : new Color(0.5f, 0.1f, 0.1f, 1f);
        }

        public void SetCursorColor(Color color) => _cursor.color = color;

        public void SetSafeZoneColor(Color color) => _safeZone.color = color;

        private float ToBarX(float normalized)
        {
            return (Mathf.Clamp01(normalized) - 0.5f) * _barWidth;
        }

        // --- 生成ヘルパー ---

        private Image CreateImage(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = _white;
            image.color = color;
            image.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return image;
        }

        private static RawImage CreateRaw(string name, Transform parent, Vector2 position, Vector2 size, Texture2D texture)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);

            var raw = go.GetComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return raw;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 position, Vector2 size,
                                                  float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
        }

        /// <summary>ストライプの繰り返し数を帯の大きさに合わせる。</summary>
        private static void SetStripeTiling(RawImage image, float bandHeight)
        {
            Vector2 size = image.rectTransform.sizeDelta;
            float tiles = Mathf.Max(1f, size.x / Mathf.Max(1f, bandHeight));
            image.uvRect = new Rect(0f, 0f, tiles, 1f);
        }
    }
}
