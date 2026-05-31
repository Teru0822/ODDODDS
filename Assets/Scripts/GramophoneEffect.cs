using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GramophoneEffect : MonoBehaviour
{
    [Header("レコードのノイズ設定")]
    [Tooltip("全体にうっすら乗る「サーッ」というホワイトノイズの音量")]
    [Range(0f, 0.2f)] public float noiseLevel = 0.015f;
    
    [Tooltip("「プチッ」「チリッ」というレコードの傷によるノイズの音量")]
    [Range(0f, 1f)] public float crackleLevel = 0.3f;
    
    [Tooltip("チリチリノイズが発生する頻度")]
    [Range(0f, 0.05f)] public float crackleProbability = 0.005f;

    [Header("レコードの回転ムラ (Wow & Flutter)")]
    [Tooltip("ピッチ（音程）が揺れるスピード")]
    public float wowSpeed = 5.0f;
    
    [Tooltip("ピッチ（音程）が揺れる幅")]
    public float wowAmount = 0.015f;

    private AudioSource _audioSource;
    private float _originalPitch;
    private System.Random _rand = new System.Random();
    
    private AudioListener _listener;
    private float _currentNoiseLevel;
    private float _currentCrackleLevel;
    private bool _isPlaying = false;

    void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        _originalPitch = _audioSource.pitch;
    }

    void Update()
    {
        // メインスレッドで再生状態を取得
        _isPlaying = _audioSource.isPlaying;

        if (!_isPlaying)
        {
            // 停止している時はピッチを元に戻す
            _audioSource.pitch = _originalPitch;
            return;
        }

        // サイン波を使って、古いレコード特有の「音程の揺れ（回転ムラ）」を再現
        _audioSource.pitch = _originalPitch + Mathf.Sin(Time.time * wowSpeed) * wowAmount;

        // Unityの仕様上、プログラムで生成したノイズは距離減衰が効きにくいため、手動で距離を計算して音量を下げます
        if (_listener == null)
        {
            _listener = FindFirstObjectByType<AudioListener>();
        }

        if (_listener != null)
        {
            float distance = Vector3.Distance(transform.position, _listener.transform.position);
            float maxDist = _audioSource.maxDistance;
            float minDist = _audioSource.minDistance;
            
            float attenuation = 1f;
            if (distance > maxDist) 
            {
                attenuation = 0f;
            }
            else if (distance > minDist)
            {
                // 距離に応じて0〜1の割合で音量を小さくする
                attenuation = 1f - ((distance - minDist) / (maxDist - minDist));
            }

            // 元の設定値に減衰率を掛ける
            _currentNoiseLevel = noiseLevel * attenuation;
            _currentCrackleLevel = crackleLevel * attenuation;
        }
        else
        {
            _currentNoiseLevel = noiseLevel;
            _currentCrackleLevel = crackleLevel;
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (_rand == null) return;
        
        // 再生停止時はノイズを追加しない（無音）
        if (!_isPlaying) return;

        for (int i = 0; i < data.Length; i += channels)
        {
            float noise = ((float)_rand.NextDouble() * 2f - 1f) * _currentNoiseLevel;
            
            float crackle = 0f;
            if ((float)_rand.NextDouble() < crackleProbability)
            {
                crackle = ((float)_rand.NextDouble() * 2f - 1f) * _currentCrackleLevel;
            }

            for (int c = 0; c < channels; c++)
            {
                data[i + c] += noise + crackle;
            }
        }
    }
}
