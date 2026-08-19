using UnityEngine;

/// <summary>
/// ゲームオーバー時に表示する情報（時間など）を格納するスクリプト
/// </summary>
public class InfomationManager : MonoBehaviour, IsaveDataProvider
{
    private float _playTimer;
    private bool _isStopTimer = true;
    public bool IsStopTimer{get{return _isStopTimer;}set{_isStopTimer = value;}}

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Update()
    {
        if(_isStopTimer) return;

        _playTimer += Time.deltaTime;
    }

    public void WriteSaveData(RoguelikeSaveData data)
    {
        data.playTime = _playTimer;
    }

    public void ReadSaveData(RoguelikeSaveData data)
    {
        _playTimer = data.playTime;
        _isStopTimer = false;
    }
}
