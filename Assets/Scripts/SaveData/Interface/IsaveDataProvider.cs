using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

public interface IsaveDataProvider
{
    /// <summary>
    /// セーブデータへ現在の状態を書き込む
    /// </summary>
    void WriteSaveData(RoguelikeSaveData saveData);

    /// <summary>
    /// セーブデータから状態を復元する
    /// </summary>
    void ReadSaveData(RoguelikeSaveData saveData);
}
