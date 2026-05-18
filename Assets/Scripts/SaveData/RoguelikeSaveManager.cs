using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// ローグライク要素のセーブ・ロードを管理するクラス（XOR難読化付き）
/// </summary>
public static class RoguelikeSaveManager
{
    private static readonly string SaveFileName = "RoguelikeSave.dat"; // 難読化するので拡張子をdatにするのが一般的です
    
    // 難読化用のキー (適当な4バイト)
    private static readonly byte[] ObfuscationKeys = { 0x5a, 0x9f, 0x3b, 0x7c };

    private static string GetSaveFilePath()
    {
        Debug.Log(Path.Combine(Application.persistentDataPath, SaveFileName));
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    /// <summary>
    /// セーブデータを保存する
    /// </summary>
    public static void Save(RoguelikeSaveData data)
    {
        string json = JsonUtility.ToJson(data);
        byte[] encryptedBytes = EncodeText(json);

        string path = GetSaveFilePath();
        File.WriteAllBytes(path, encryptedBytes);
        
        Debug.Log($"セーブ完了: {path}");
    }

    /// <summary>
    /// セーブデータを読み込む
    /// </summary>
    public static RoguelikeSaveData Load()
    {
        string path = GetSaveFilePath();
        
        if (!File.Exists(path))
        {
            Debug.Log("セーブデータが見つかりません。新規データを作成します。");
            Save(new RoguelikeSaveData());//新規データを作成
        }

        try
        {
            byte[] encryptedBytes = File.ReadAllBytes(path);
            string json = DecodeBytes(encryptedBytes);
            RoguelikeSaveData data = JsonUtility.FromJson<RoguelikeSaveData>(json);
            Debug.Log($"ロード完了: {path}");

            return data ?? new RoguelikeSaveData();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"セーブデータのロードに失敗しました: {e.Message}");
            Debug.LogError("新規データを作成し直します");
            Save(new RoguelikeSaveData());//新規データを作成
            return new RoguelikeSaveData();
        }
    }

    /// <summary>
    /// セーブデータを削除する (デバッグ用)
    /// </summary>
    public static void DeleteSaveData()
    {
        string path = GetSaveFilePath();
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"セーブデータを削除しました: {path}");
        }
    }

    #region 難読化ロジック (XOR)

    private static byte[] EncodeText(string text)
    {
        byte[] byteArray = Encoding.UTF8.GetBytes(text);
        DecodeEncode(ref byteArray);
        return byteArray;
    }

    private static string DecodeBytes(byte[] byteArray)
    {
        DecodeEncode(ref byteArray);
        return Encoding.UTF8.GetString(byteArray);
    }

    private static void DecodeEncode(ref byte[] byteArray)
    {
        for (int i = 0; i < byteArray.Length; i++)
        {
            byteArray[i] ^= ObfuscationKeys[i % ObfuscationKeys.Length];
        }
    }

    #endregion
}
