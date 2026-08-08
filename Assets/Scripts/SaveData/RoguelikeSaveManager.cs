using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
// UnityEditor はプレイヤービルドに含まれないため、エディタ限定で参照する。
// ガードを外すと Build 時に CS0234 でコンパイルが落ちる
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ローグライク要素のセーブ・ロードを管理するクラス（XOR難読化付き）
/// </summary>
public static class RoguelikeSaveManager
{
    private static readonly string SaveFileName = "RoguelikeSave.dat"; // 難読化するので拡張子をdatにするのが一般的です
    
    // 難読化用のキー (適当な4バイト)
    private static readonly byte[] ObfuscationKeys = { 0x5a, 0x9f, 0x3b, 0x7c };

    private static RoguelikeSaveData _previousSaveData;//以前のデータを保持
    private static string GetSaveFilePath()
    {
#if UNITY_EDITOR
        bool debugMode = EditorPrefs.GetBool(
            "LFEngine_DebugMode",
            false);

        //DEBUG:デバッグするときの処理
        if (debugMode)
        {
            string path = Path.Combine(
                Application.dataPath,
                "Resources",
                "DebugData",
                "DebugSaveData.dat"
            );
            return path;
        }
#endif
        Debug.Log(Path.Combine(Application.persistentDataPath, SaveFileName));
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    /// <summary>
    /// セーブデータにセーブを行う
    /// </summary>
    /// <param name="isNewData">Trueの場合、新規データを作成</param>
    public static void Save(bool isNewData = false)
    {
#if UNITY_EDITOR
        bool debugMode = EditorPrefs.GetBool(
            "LFEngine_DebugMode",
            false);

        //DEBUG:デバッグするときの処理
        if (debugMode)
        {
            Debug.LogError("デバッグ用のデータのため、セーブはできません");
            return;
        }
#endif
        if (isNewData)//セーブデータを新規で作る場合
        {
            var newSaveData = new RoguelikeSaveData();
            newSaveData.money = 10000;
            string newJson = JsonUtility.ToJson(newSaveData);
            byte[] newEncryptedBytes = EncodeText(newJson);

            string newPath = GetSaveFilePath();
            File.WriteAllBytes(newPath, newEncryptedBytes);

            Debug.Log($"新規セーブデータ作成完了: {newPath}");
            Load();
            return;
        }

        if (!File.Exists(GetSaveFilePath()))
        {
            Debug.LogError("セーブデータが見つからないので、セーブできません");
            return;
        }


        //以前のセーブデータと差別点がある項目のみを検出（個別でセーブしやすくできる）
        var saveData = new RoguelikeSaveData();
        var providers = InterfaceFinder.FindAllByInterface<IsaveDataProvider>();
        foreach (var provider in providers)
        {
            provider.WriteSaveData(saveData);
        }

        Debug.LogError(
        $@"===== RoguelikeSaveData Result=====

        money : {saveData.money}
        unwashedMoney : {saveData.unwashedMoney}
        bronzeCoin : {saveData.bronzeCoin}
        silverCoin : {saveData.silverCoin}
        goldCoin : {saveData.goldCoin}
        blackDiamond : {saveData.blackDiamond}
        virtuePoints : {saveData.virtuePoints}

        isUnlockPinball : {saveData.isUnlockPinball}
        isUnlockTypewriter : {saveData.isUnlockTypewriter}
        isUnlockMinigame : {saveData.isUnlockMinigame}
        isUnlockVisitor : {saveData.isUnlockVisitor}

        isUfoCatcherUnlocked : {saveData.isUfoCatcherUnlocked}
        isFallBallUnlocked : {saveData.isFallBallUnlocked}
        isPinballUnlocked : {saveData.isPinballUnlocked}

        isRouletteCandyObtained : {saveData.isRouletteCandyObtained}
        isRoulettePinballObtained : {saveData.isRoulettePinballObtained}

        ownedPermanentItems : {(saveData.ownedPermanentItems == null ? "null" : saveData.ownedPermanentItems.Count.ToString())}
        ownedConsumeItems   : {(saveData.ownedConsumeItems == null ? "null" : saveData.ownedConsumeItems.Count.ToString())}
        ownedEffects        : {(saveData.ownedEffects == null ? "null" : saveData.ownedEffects.Count.ToString())}
        visitorSaveDatas    : {(saveData.visitorSaveDatas == null ? "null" : saveData.visitorSaveDatas.Count.ToString())}

        ============================");
        string json = JsonUtility.ToJson(saveData);
        //string path = GetSaveFilePath();
        //File.WriteAllText(path, json);

        
        byte[] encryptedBytes = EncodeText(json);
        string path = GetSaveFilePath();
        File.WriteAllBytes(path, encryptedBytes);
        
        Debug.Log($"セーブ完了: {path}");
    }

    /// <summary>
    /// セーブデータを読み込む
    /// </summary>
    public static void Load()
    {
        string path = GetSaveFilePath();        
        if (!File.Exists(path))
        {
            Debug.Log("セーブデータが見つかりません。新規データを作成します。");
            Save(true);//新規データを作成
            return;
        }

        RoguelikeSaveData saveData = new RoguelikeSaveData();
        try
        {
            byte[] encryptedBytes = File.ReadAllBytes(path);
            string json = DecodeBytes(encryptedBytes);
            saveData = JsonUtility.FromJson<RoguelikeSaveData>(json);
            Debug.Log($"ロード完了: {path}");
            _previousSaveData = saveData;

        }
        catch (System.Exception e)
        {
            Debug.LogError($"セーブデータのロードに失敗しました: {e.Message}");
            Debug.LogError("新規データを作成し直します");
            Save(true);//新規データを作成
            return;
        }

        Debug.LogError(
        $@"===== RoguelikeSaveData Load Result=====

        money : {saveData.money}
        unwashedMoney : {saveData.unwashedMoney}
        bronzeCoin : {saveData.bronzeCoin}
        silverCoin : {saveData.silverCoin}
        goldCoin : {saveData.goldCoin}
        blackDiamond : {saveData.blackDiamond}
        virtuePoints : {saveData.virtuePoints}

        isUnlockPinball : {saveData.isUnlockPinball}
        isUnlockTypewriter : {saveData.isUnlockTypewriter}
        isUnlockMinigame : {saveData.isUnlockMinigame}
        isUnlockVisitor : {saveData.isUnlockVisitor}

        isUfoCatcherUnlocked : {saveData.isUfoCatcherUnlocked}
        isFallBallUnlocked : {saveData.isFallBallUnlocked}
        isPinballUnlocked : {saveData.isPinballUnlocked}

        isRouletteCandyObtained : {saveData.isRouletteCandyObtained}
        isRoulettePinballObtained : {saveData.isRoulettePinballObtained}

        ownedPermanentItems : {(saveData.ownedPermanentItems == null ? "null" : saveData.ownedPermanentItems.Count.ToString())}
        ownedConsumeItems   : {(saveData.ownedConsumeItems == null ? "null" : saveData.ownedConsumeItems.Count.ToString())}
        ownedEffects        : {(saveData.ownedEffects == null ? "null" : saveData.ownedEffects.Count.ToString())}
        visitorSaveDatas    : {(saveData.visitorSaveDatas == null ? "null" : saveData.visitorSaveDatas.Count.ToString())}

        ============================");
        //すべてのデータが必要なスクリプトに欲しいデータを送る
        var providers = InterfaceFinder.FindAllByInterface<IsaveDataProvider>();
        foreach (var provider in providers)
        {
            //1つのProviderが例外を出しても、他のProviderと呼び出し元の処理を止めない
            try
            {
                provider.ReadSaveData(saveData);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RoguelikeSaveManager] {provider.GetType().Name} のReadSaveDataに失敗しました");
                Debug.LogException(e);
            }
        }

        Debug.LogError("ロード完了");
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

    /// <summary>
    /// ゲームオーバー時に特定のデータを削除する関数
    /// </summary>
    public static void DeleteDataInGameOver()
    {
        string path = GetSaveFilePath();
        RoguelikeSaveData saveData = new RoguelikeSaveData();
        try
        {
            byte[] encryptedBytes = File.ReadAllBytes(path);
            string json = DecodeBytes(encryptedBytes);
            saveData = JsonUtility.FromJson<RoguelikeSaveData>(json);

            //一部データを消してセーブしなおす
            saveData.money = 0;
            saveData.unwashedMoney = 0;
            saveData.bronzeCoin = 0;
            saveData.silverCoin = 0;
            saveData.goldCoin = 0;
            saveData.blackDiamond = 0;
            saveData.ownedConsumeItems = new List<ItemSaveData>();
            saveData.ownedEffects = new List<EffectSaveData>();
            saveData.visitorSaveDatas = new List<VisitorSaveData>();
            
            //セーブ処理
            json = JsonUtility.ToJson(saveData);
            encryptedBytes = EncodeText(json);
            File.WriteAllBytes(path, encryptedBytes);
            
            Debug.Log($"ゲームオーバー時のセーブ完了: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            return;
        }
    }

    #region 難読化ロジック (XOR)

    public static byte[] EncodeText(string text)
    {
        byte[] byteArray = Encoding.UTF8.GetBytes(text);
        DecodeEncode(ref byteArray);
        return byteArray;
    }

    public static string DecodeBytes(byte[] byteArray)
    {
        DecodeEncode(ref byteArray);
        return Encoding.UTF8.GetString(byteArray);
    }

    public static void DecodeEncode(ref byte[] byteArray)
    {
        for (int i = 0; i < byteArray.Length; i++)
        {
            byteArray[i] ^= ObfuscationKeys[i % ObfuscationKeys.Length];
        }
    }

    #endregion
}
