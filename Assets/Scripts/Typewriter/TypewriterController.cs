using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// タイプライター本体にアタッチして、任意の文字列を 1 文字ずつキーアニメーションで「打つ」コントローラ。
/// <para>使い方: Inspector の keyBindings に各キーの TypewriterKey を keyId 付きで列挙する。
/// keyId は次のいずれか: 'a'-'z', '0', '2'-'9', '-', ';', '&', '1/2', '3/4', ',', '"', 'space', 'back space', 'left shift'</para>
/// </summary>
[DisallowMultipleComponent]
public class TypewriterController : MonoBehaviour
{
    [Serializable]
    public class KeyBinding
    {
        [Tooltip("キー識別子。詳細は TypewriterController クラスコメント参照")]
        public string keyId;
        public TypewriterKey key;
    }

    [Header("キーバインド (Inspector で各キーをドラッグ)")]
    public List<KeyBinding> keyBindings = new List<KeyBinding>();

    [Tooltip("子オブジェクト名 (大文字小文字を区別しない) から TypewriterKey を自動バインドする。未バインドのキーだけを補完")]
    public bool autoBindByName = true;

    [Header("打鍵モーション (全キー共通)")]
    [Tooltip("押し込み方向 (このコントローラのローカル空間)。デフォルトは local -Y (下)")]
    public Vector3 pressDirection = new Vector3(0f, -1f, 0f);

    [Tooltip("押し込み深さ (m)。typewriter のスケールに合わせて調整。視認できない場合は 0.01〜0.05 に上げる")]
    public float pressDepth = 0.01f;

    [Tooltip("押し込みにかける秒数 (1 キーあたり)")]
    public float pressDownDuration = 0.025f;

    [Tooltip("離す時にかける秒数 (1 キーあたり)")]
    public float pressUpDuration = 0.04f;

    [Header("打鍵タイミング")]
    [Tooltip("1 文字を打ってから次の文字を打ち始めるまでの遅延 (秒)")]
    public float interCharDelay = 0.01f;

    [Tooltip("left shift を下げ終わってから文字キーを押し始めるまでの遅延 (秒)")]
    public float shiftToKeyDelay = 0.0f;

    [Tooltip("文字キーを離してから left shift を上げ始めるまでの遅延 (秒)")]
    public float keyToShiftReleaseDelay = 0.0f;

    [Header("打鍵音")]
    [Tooltip("打鍵音を再生する AudioSource。null なら自身に AddComponent して使う")]
    public AudioSource keyAudioSource;

    [Tooltip("打鍵時に再生する AudioClip 群。複数指定するとランダムに 1 個選ばれる")]
    public AudioClip[] keyAudioClips;

    [Tooltip("打鍵音ピッチのランダム下限")]
    [Range(0.5f, 2f)]
    public float keyAudioPitchMin = 0.97f;

    [Tooltip("打鍵音ピッチのランダム上限")]
    [Range(0.5f, 2f)]
    public float keyAudioPitchMax = 1.05f;

    [Tooltip("打鍵音のボリューム (1 を超えるブーストも可)")]
    [Range(0f, 5f)]
    public float keyAudioVolume = 1.0f;

    [Tooltip("打鍵音の空間ブレンド (0=2D 距離無関係, 1=3D 距離減衰あり)")]
    [Range(0f, 1f)]
    public float keyAudioSpatialBlend = 0f;

    [Header("紙への出力")]
    [Tooltip("打鍵中の紙ギミック。null なら自身/子から自動検索、無ければ機能スキップ")]
    public TypewriterPaperOutput paperOutput;

    [Header("デバッグ")]
    [Tooltip("打鍵・未対応文字・未バインドキーを Console に出力")]
    public bool logEvents = true;

    private Dictionary<string, TypewriterKey> _keyMap;
    private Dictionary<char, (string keyId, bool shift)> _charMap;
    private TypewriterKey _shiftKey;
    private bool _typing;

    /// <summary>打鍵中フラグ (二重起動防止に使える)</summary>
    public bool IsTyping => _typing;

    private void Awake()
    {
        if (autoBindByName) AutoBindKeysByName();
        BuildKeyMap();
        BuildCharMap();
        EnsureAudioSource();
        if (paperOutput == null) paperOutput = GetComponentInChildren<TypewriterPaperOutput>();
        // 重要な状態は logEvents に関わらず常に出力 (シーン既存配置で logEvents=false の場合の切り分け用)
        var ids = new List<string>(_keyMap.Keys);
        ids.Sort();
        Debug.Log($"[Typewriter] '{name}' 起動。バインド済みキー数 = {_keyMap.Count} / shift={(_shiftKey != null ? "OK" : "MISSING")} / paper={(paperOutput != null ? paperOutput.name : "NULL")} / clips={(keyAudioClips != null ? keyAudioClips.Length : 0)} / keys=[{string.Join(",", ids)}]", this);
    }

    /// <summary>子オブジェクトを走査し、名前が既知のキー ID に一致したら自動的に TypewriterKey を付与してキーバインドに追加。</summary>
    private void AutoBindKeysByName()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in keyBindings)
        {
            if (b != null && !string.IsNullOrEmpty(b.keyId)) existing.Add(b.keyId);
        }

        int added = 0;
        var transforms = GetComponentsInChildren<Transform>(true);
        foreach (var t in transforms)
        {
            if (t == transform) continue;
            string keyId = NormalizeKeyId(t.name);
            if (keyId == null) continue;
            if (existing.Contains(keyId)) continue;

            var tk = t.GetComponent<TypewriterKey>();
            if (tk == null) tk = t.gameObject.AddComponent<TypewriterKey>();
            keyBindings.Add(new KeyBinding { keyId = keyId, key = tk });
            existing.Add(keyId);
            added++;
        }
        if (added > 0)
        {
            Debug.Log($"[Typewriter] 子オブジェクト名から {added} 個のキーを自動バインドしました", this);
        }
    }

    /// <summary>GameObject 名を正規のキー ID にマップ。一致なしなら null。</summary>
    private static string NormalizeKeyId(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        // 完全一致 (記号・複合キーが GameObject 名そのままのケース) を最優先
        switch (name)
        {
            case "-": case ";": case "&": case ",": case "\"":
            case "1/2": case "3/4":
            case "back space": case "left shift": case "space":
                return name;
        }
        // 正規化: 小文字化 + _ / . / - → 空白
        string n = name.Trim().ToLowerInvariant().Replace('_', ' ').Replace('.', ' ');
        // "key X" / "btn X" 形式の prefix を剥がす
        if (n.StartsWith("key ")) n = n.Substring(4).Trim();
        if (n.StartsWith("btn ")) n = n.Substring(4).Trim();
        // 単独の記号キーは prefix 剥がしの後でも再チェック
        switch (n)
        {
            case "-": case ";": case "&": case ",": case "\"":
            case "1/2": case "3/4":
            case "back space": case "left shift": case "space":
                return n;
        }
        // 以降の比較では - も区切りとして扱う
        n = n.Replace('-', ' ');
        // 単一文字キー (a-z, 0, 2-9)
        if (n.Length == 1)
        {
            char c = n[0];
            if (c >= 'a' && c <= 'z') return c.ToString();
            if (c == '0' || (c >= '2' && c <= '9')) return c.ToString();
        }
        // エイリアス (区切り無視)
        string compact = n.Replace(" ", "");
        switch (compact)
        {
            case "leftshift": case "shift": return "left shift";
            case "backspace": return "back space";
            case "space": case "spacebar": case "spacekey": return "space";
            case "comma": return ",";
            case "quote": case "doublequote": case "quotation": return "\"";
            case "semicolon": return ";";
            case "ampersand": return "&";
            case "hyphen": case "minus": case "dash": case "minussign": return "-";
            case "half": case "onehalf": case "12": case "1of2": return "1/2";
            case "threequarter": case "threequarters": case "34": case "3of4": return "3/4";
        }
        return null;
    }

    private void BuildKeyMap()
    {
        _keyMap = new Dictionary<string, TypewriterKey>(StringComparer.Ordinal);
        foreach (var b in keyBindings)
        {
            if (b == null) continue;
            if (string.IsNullOrEmpty(b.keyId) || b.key == null) continue;
            _keyMap[b.keyId] = b.key;
        }
        _keyMap.TryGetValue("left shift", out _shiftKey);
    }

    private void BuildCharMap()
    {
        _charMap = new Dictionary<char, (string, bool)>();

        // 英字 (大文字は left shift)
        for (char c = 'a'; c <= 'z'; c++) _charMap[c] = (c.ToString(), false);
        for (char c = 'A'; c <= 'Z'; c++) _charMap[c] = (char.ToLowerInvariant(c).ToString(), true);

        // 数字 (1 はこのタイプライターには無いので未対応)
        _charMap['0'] = ("0", false);
        for (char c = '2'; c <= '9'; c++) _charMap[c] = (c.ToString(), false);

        // 単独で打てる記号・空白
        _charMap[' '] = ("space", false);
        _charMap['-'] = ("-", false);
        _charMap[';'] = (";", false);
        _charMap['&'] = ("&", false);
        _charMap[','] = (",", false);
        _charMap['"'] = ("\"", false);

        // left shift と組み合わせる記号
        _charMap['*'] = ("-", true);
        _charMap['#'] = ("3", true);
        _charMap['$'] = ("4", true);
        _charMap['%'] = ("5", true);
        _charMap['_'] = ("6", true);
        _charMap['/'] = ("8", true);
        _charMap[')'] = ("9", true);
        _charMap['('] = ("0", true);
        _charMap['@'] = ("&", true);
        _charMap[':'] = (";", true);
        _charMap['.'] = ("\"", true);
        _charMap['?'] = (",", true);

        // 半角フラクション (Unicode 文字でも記述可)
        _charMap['½'] = ("1/2", false);
        _charMap['¾'] = ("3/4", false);
    }

    /// <summary>文字列を 1 文字ずつ打鍵するコルーチンを開始する。</summary>
    public Coroutine TypeText(string text)
    {
        if (_typing)
        {
            Debug.LogWarning($"[Typewriter] TypeText 無視: 既に打鍵中 (\"{text}\")", this);
            return null;
        }
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("[Typewriter] TypeText 無視: text が空", this);
            return null;
        }
        Debug.Log($"[Typewriter] TypeText 呼び出し: \"{text}\" (Bindings={_keyMap.Count})", this);
        return StartCoroutine(TypeTextCoroutine(text));
    }

    /// <summary>
    /// 1 文字分のキーを手動で打鍵する (物理キーボード連動用)。文字列の自動打鍵中 (TypeText) は無視。
    /// 紙への出力は行わず、キーアニメーション + 打鍵音のみ。
    /// </summary>
    public void StrikeKey(char c)
    {
        if (_typing) return; // 自動打鍵中は手動入力を無視
        if (_charMap == null) return;
        if (!_charMap.TryGetValue(c, out var entry)) return;            // 未対応文字は無視
        if (!_keyMap.TryGetValue(entry.keyId, out var key) || key == null) return; // 未バインドは無視
        StartCoroutine(StrikeKeyRoutine(c, key, entry.shift));
    }

    private IEnumerator StrikeKeyRoutine(char c, TypewriterKey key, bool shift)
    {
        Vector3 worldDelta = GetPressWorldDelta();
        if (shift && _shiftKey != null)
        {
            yield return _shiftKey.HoldDown(worldDelta, pressDownDuration);
            yield return key.PressDown(worldDelta, pressDownDuration);
            PlayKeySound();
            yield return key.PressUp(pressUpDuration);
            yield return _shiftKey.Release(pressUpDuration);
        }
        else
        {
            yield return key.PressDown(worldDelta, pressDownDuration);
            PlayKeySound();
            yield return key.PressUp(pressUpDuration);
        }
    }

    /// <summary>現在打鍵中ならコルーチンを止めてシフトも戻す (緊急停止用)。</summary>
    public void AbortTyping()
    {
        if (!_typing) return;
        StopAllCoroutines();
        if (_shiftKey != null) StartCoroutine(_shiftKey.Release(pressUpDuration));
        _typing = false;
    }

    private IEnumerator TypeTextCoroutine(string text)
    {
        _typing = true;
        Debug.Log($"[Typewriter] 打鍵コルーチン開始: \"{text}\"", this);
        if (paperOutput != null) paperOutput.BeginNewPaper();
        int typed = 0, skipped = 0;
        for (int i = 0; i < text.Length; i++)
        {
            bool ok = false;
            yield return TypeOneChar(text[i], r => ok = r);
            if (ok) typed++; else skipped++;
            if (interCharDelay > 0f) yield return new WaitForSeconds(interCharDelay);
        }
        if (paperOutput != null) paperOutput.EndPaper();
        Debug.Log($"[Typewriter] 打鍵完了: typed={typed} skipped={skipped}", this);
        _typing = false;
    }

    private IEnumerator TypeOneChar(char c, System.Action<bool> setOk)
    {
        if (!_charMap.TryGetValue(c, out var entry))
        {
            Debug.LogWarning($"[Typewriter] 未対応の文字 '{c}' (U+{(int)c:X4}) をスキップ", this);
            setOk?.Invoke(false);
            yield break;
        }
        if (!_keyMap.TryGetValue(entry.keyId, out var key) || key == null)
        {
            Debug.LogWarning($"[Typewriter] キー '{entry.keyId}' が未バインドのため '{c}' をスキップ", this);
            setOk?.Invoke(false);
            yield break;
        }

        Vector3 worldDelta = GetPressWorldDelta();
        if (entry.shift)
        {
            if (_shiftKey == null)
            {
                Debug.LogWarning($"[Typewriter] 'left shift' が未バインドのためシフト文字 '{c}' をスキップ", this);
                setOk?.Invoke(false);
                yield break;
            }
            yield return _shiftKey.HoldDown(worldDelta, pressDownDuration);
            if (shiftToKeyDelay > 0f) yield return new WaitForSeconds(shiftToKeyDelay);
            // キーを底まで沈める → 底着き瞬間に音 + 紙への文字追加 → 戻す
            yield return key.PressDown(worldDelta, pressDownDuration);
            PlayKeySound();
            if (paperOutput != null) paperOutput.AppendChar(c);
            yield return key.PressUp(pressUpDuration);
            if (keyToShiftReleaseDelay > 0f) yield return new WaitForSeconds(keyToShiftReleaseDelay);
            yield return _shiftKey.Release(pressUpDuration);
        }
        else
        {
            yield return key.PressDown(worldDelta, pressDownDuration);
            PlayKeySound();
            if (paperOutput != null) paperOutput.AppendChar(c);
            yield return key.PressUp(pressUpDuration);
        }
        setOk?.Invoke(true);
    }

    /// <summary>押し込みベクトルを world 空間に変換して返す。</summary>
    private Vector3 GetPressWorldDelta()
    {
        Vector3 dir = pressDirection.sqrMagnitude > 0f ? pressDirection.normalized : Vector3.down;
        return transform.TransformDirection(dir) * pressDepth;
    }

    private void EnsureAudioSource()
    {
        if (keyAudioSource == null) keyAudioSource = GetComponent<AudioSource>();
        if (keyAudioSource == null)
        {
            keyAudioSource = gameObject.AddComponent<AudioSource>();
            keyAudioSource.playOnAwake = false;
        }
        keyAudioSource.spatialBlend = keyAudioSpatialBlend;
        keyAudioSource.volume = 1f; // PlayOneShot 側で volumeScale を渡すので Source 側は 1 固定
    }

    private void PlayKeySound()
    {
        if (keyAudioSource == null || keyAudioClips == null || keyAudioClips.Length == 0) return;
        var clip = keyAudioClips[UnityEngine.Random.Range(0, keyAudioClips.Length)];
        if (clip == null) return;
        keyAudioSource.pitch = UnityEngine.Random.Range(keyAudioPitchMin, keyAudioPitchMax);
        keyAudioSource.PlayOneShot(clip, keyAudioVolume);
    }
}
