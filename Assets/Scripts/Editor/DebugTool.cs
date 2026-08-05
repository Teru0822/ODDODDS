using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using static UnityEditor.Timeline.Actions.MenuPriority;


public class DebugTool : EditorWindow
{
    private bool _isUseDebugData = false;
    private bool _isCreateDebugSaveData = false;
    

    //private ReactiveProperty<int> _debugHealth = new ReactiveProperty<int>(100);
    //private ReactiveProperty<int> _debugSanValue = new ReactiveProperty<int>(100);
    private int _debugMoney = 10000;
    private int _debugUnwashedMoney = 0;

    private RoguelikeSaveData _tmpSaveData = new RoguelikeSaveData();
    private List<ItemSaveData> _tmpOwnItems = new List<ItemSaveData>();
    private List<ItemSaveData> _tmpOwnConsumeItems = new List<ItemSaveData>();

    private GUIStyle _titleStyle;
    private Texture2D _logo;

    //デバッグ用のメニュ, 項目を作成
    [MenuItem("DebugTool/CreateGUI", false, 1)]
    static void CreateDebugGUI()
    {
        DebugTool window = GetWindow<DebugTool>();
        window.titleContent = new GUIContent("DebugTool");
    }

    private void OnEnable()
    {
        _logo = AssetDatabase.LoadAssetAtPath<Texture2D>(
        "Assets/Resources/illust/LF_Engine_Logo.png"
        );

        Debug.Log(_logo);
        Debug.Log(_logo?.GetType());


    }


    /// <summary>
    /// セーブデータを作成する (デバッグ用)
    /// </summary>
    public static void CreateDebugSaveData(RoguelikeSaveData data)
    {
        string json = JsonUtility.ToJson(data);
        byte[] encryptedBytes = RoguelikeSaveManager.EncodeText(json);

        string path = Path.Combine(
            Application.dataPath,
            "Resources",
            "DebugData",
            "DebugSaveData.dat"
        );
        File.WriteAllBytes(path, encryptedBytes);
        //File.WriteAllText(path, json);

        Debug.Log($"セーブデータ作成完了: {path}");
    }

    private void OnGUI()
    {
        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
            };
        }

        EditorGUILayout.Space(10);
        if (_logo != null)
        {
            Rect rect = GUILayoutUtility.GetRect(0, 150);
            GUI.DrawTexture(rect, _logo, ScaleMode.ScaleToFit);
        }
        EditorGUILayout.Space(10);

        /*各処理について記述していく*/
        //検証用のデータを用いる(再生前のみ表示)
        
        EditorGUILayout.LabelField("検証用データの設定", _titleStyle);
        EditorGUILayout.Space(10);


        if (!Application.isPlaying)
        {
            _isUseDebugData = EditorGUILayout.Toggle("デバッグ用のセーブデータを使う", _isUseDebugData);
            EditorPrefs.SetBool(
            "LFEngine_DebugMode",
            _isUseDebugData);

            //デバッグ用のセーブデータを作成する機能
            if (_isUseDebugData)
            {
                _isCreateDebugSaveData = EditorGUILayout.Toggle("デバッグ用のセーブデータを作成する", _isCreateDebugSaveData);
                if (_isCreateDebugSaveData)
                {
                    _tmpSaveData.money = Mathf.Max(0, EditorGUILayout.IntField("所持金の数値", _tmpSaveData.money));
                    _tmpSaveData.unwashedMoney = Mathf.Max(0, EditorGUILayout.IntField("未洗浄金の数値", _tmpSaveData.unwashedMoney));
                    _tmpSaveData.bronzeCoin = Mathf.Max(0, EditorGUILayout.IntField("ブロンズコインの数値", _tmpSaveData.bronzeCoin));
                    _tmpSaveData.silverCoin = Mathf.Max(0, EditorGUILayout.IntField("シルバーコインの数値", _tmpSaveData.silverCoin));
                    _tmpSaveData.goldCoin = Mathf.Max(0, EditorGUILayout.IntField("ゴールドコインの数値", _tmpSaveData.goldCoin));
                    _tmpSaveData.blackDiamond = Mathf.Max(0, EditorGUILayout.IntField("ブラックダイヤモンドの数値", _tmpSaveData.blackDiamond));
                    _tmpSaveData.virtuePoints = Mathf.Max(0, EditorGUILayout.IntField("徳ポイントの数値", _tmpSaveData.virtuePoints));
                    _tmpSaveData.isUnlockPinball = EditorGUILayout.Toggle("ピンボール機能を解放", _tmpSaveData.isUnlockPinball);
                    _tmpSaveData.isUnlockTypewriter = EditorGUILayout.Toggle("タイプライター機能を解放", _tmpSaveData.isUnlockTypewriter);
                    _tmpSaveData.isUnlockMinigame = EditorGUILayout.Toggle("ミニゲーム機能を解放", _tmpSaveData.isUnlockMinigame);
                    _tmpSaveData.isUnlockVisitor = EditorGUILayout.Toggle("訪問者機能を解放", _tmpSaveData.isUnlockVisitor);

                    // --- アイテムドロップダウン用データの作成 ---
                    ItemDataBase db = AssetDatabase.LoadAssetAtPath<ItemDataBase>("Assets/Resources/ItemData/ItemDataBase.asset");
                    
                    // 恒常アイテム用
                    string[] permDisplayNames;
                    int[] permItemIds;
                    
                    // 消費アイテム用
                    string[] consumeDisplayNames;
                    int[] consumeItemIds;

                    if (db != null && db.itemDataBase != null)
                    {
                        // 恒常アイテムの抽出
                        List<ItemData> permItems = db.itemDataBase.FindAll(x => x != null && x.itemType == ItemType.Permanent);
                        permDisplayNames = new string[permItems.Count + 1];
                        permItemIds = new int[permItems.Count + 1];
                        permDisplayNames[0] = "未選択 (ID: -1)";
                        permItemIds[0] = -1;
                        for (int idx = 0; idx < permItems.Count; idx++)
                        {
                            permDisplayNames[idx + 1] = $"{permItems[idx].itemName} (ID: {permItems[idx].id})";
                            permItemIds[idx + 1] = permItems[idx].id;
                        }

                        // 消費アイテムの抽出
                        List<ItemData> consumeItems = db.itemDataBase.FindAll(x => x != null && x.itemType == ItemType.Consume);
                        consumeDisplayNames = new string[consumeItems.Count + 1];
                        consumeItemIds = new int[consumeItems.Count + 1];
                        consumeDisplayNames[0] = "未選択 (ID: -1)";
                        consumeItemIds[0] = -1;
                        for (int idx = 0; idx < consumeItems.Count; idx++)
                        {
                            consumeDisplayNames[idx + 1] = $"{consumeItems[idx].itemName} (ID: {consumeItems[idx].id})";
                            consumeItemIds[idx + 1] = consumeItems[idx].id;
                        }
                    }
                    else
                    {
                        permDisplayNames = new string[] { "未選択 (ID: -1)" };
                        permItemIds = new int[] { -1 };
                        consumeDisplayNames = new string[] { "未選択 (ID: -1)" };
                        consumeItemIds = new int[] { -1 };
                    }
                    // ----------------------------------------

                    // 恒常アイテムの表示
                    EditorGUILayout.LabelField(
                    $"登録恒常アイテム数 : {_tmpOwnItems.Count}",
                    EditorStyles.boldLabel);

                    EditorGUILayout.Space();

                    for (int i = 0; i < _tmpOwnItems.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();

                        EditorGUILayout.LabelField(
                            $"[{i}] 恒常アイテム:",
                            GUILayout.Width(90));

                        if (_tmpOwnItems[i] == null)
                        {
                            _tmpOwnItems[i] = new ItemSaveData();
                            _tmpOwnItems[i].id = -1;
                            _tmpOwnItems[i].count = 1;
                        }

                        // IDに対応する選択肢のインデックスを決定
                        int selectedIndex = 0;
                        for (int k = 0; k < permItemIds.Length; k++)
                        {
                            if (permItemIds[k] == _tmpOwnItems[i].id)
                            {
                                selectedIndex = k;
                                break;
                            }
                        }

                        int newSelectedIndex = EditorGUILayout.Popup(selectedIndex, permDisplayNames, GUILayout.Width(180));
                        if (newSelectedIndex != selectedIndex)
                        {
                            _tmpOwnItems[i].id = permItemIds[newSelectedIndex];
                        }

                        EditorGUILayout.LabelField(
                            "所持数:",
                            GUILayout.Width(50));

                        _tmpOwnItems[i].count = Mathf.Max(0, EditorGUILayout.IntField(_tmpOwnItems[i].count, GUILayout.Width(60)));

                        if (GUILayout.Button("削除", GUILayout.Width(50)))
                        {
                            _tmpOwnItems.RemoveAt(i);
                            GUIUtility.ExitGUI();
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.Space();

                    if (GUILayout.Button("恒常アイテム追加"))
                    {
                        var newItem = new ItemSaveData();
                        newItem.id = -1;
                        newItem.count = 1;
                        _tmpOwnItems.Add(newItem);
                    }

                    EditorGUILayout.Space(10);

                    // 消費アイテムの表示
                    EditorGUILayout.LabelField(
                    $"登録消費アイテム数 : {_tmpOwnConsumeItems.Count}",
                    EditorStyles.boldLabel);

                    EditorGUILayout.Space();

                    for (int i = 0; i < _tmpOwnConsumeItems.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();

                        EditorGUILayout.LabelField(
                            $"[{i}] 消費アイテム:",
                            GUILayout.Width(90));

                        if (_tmpOwnConsumeItems[i] == null)
                        {
                            _tmpOwnConsumeItems[i] = new ItemSaveData();
                            _tmpOwnConsumeItems[i].id = -1;
                            _tmpOwnConsumeItems[i].count = 1;
                        }

                        // IDに対応する選択肢のインデックスを決定
                        int selectedIndex = 0;
                        for (int k = 0; k < consumeItemIds.Length; k++)
                        {
                            if (consumeItemIds[k] == _tmpOwnConsumeItems[i].id)
                            {
                                selectedIndex = k;
                                break;
                            }
                        }

                        int newSelectedIndex = EditorGUILayout.Popup(selectedIndex, consumeDisplayNames, GUILayout.Width(180));
                        if (newSelectedIndex != selectedIndex)
                        {
                            _tmpOwnConsumeItems[i].id = consumeItemIds[newSelectedIndex];
                        }

                        EditorGUILayout.LabelField(
                            "所持数:",
                            GUILayout.Width(50));

                        _tmpOwnConsumeItems[i].count = Mathf.Max(0, EditorGUILayout.IntField(_tmpOwnConsumeItems[i].count, GUILayout.Width(60)));

                        if (GUILayout.Button("削除", GUILayout.Width(50)))
                        {
                            _tmpOwnConsumeItems.RemoveAt(i);
                            GUIUtility.ExitGUI();
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.Space();

                    if (GUILayout.Button("消費アイテム追加"))
                    {
                        ItemSaveData newItem = new ItemSaveData();
                        newItem.id = -1;
                        newItem.count = 1;
                        _tmpOwnConsumeItems.Add(newItem);
                    }


                    EditorGUILayout.Space(15);
                    if (GUILayout.Button("セーブデータを作成する"))
                    {
                        // バリデーション: 未選択のID（-1）があるかチェック
                        bool hasUnselected = false;
                        foreach (var item in _tmpOwnItems)
                        {
                            if (item != null && item.id == -1)
                            {
                                hasUnselected = true;
                                break;
                            }
                        }
                        foreach (var item in _tmpOwnConsumeItems)
                        {
                            if (item != null && item.id == -1)
                            {
                                hasUnselected = true;
                                break;
                            }
                        }

                        if (hasUnselected)
                        {
                            EditorUtility.DisplayDialog("エラー", "未選択のアイテムが含まれているため、セーブデータを作成できません。", "OK");
                            Debug.LogError("未選択のアイテムがあるため、セーブデータの作成を中止しました。");
                            return;
                        }

                        
                        if(db == null)
                        {
                            Debug.LogError("ItemDataBaseアセットが見つかりません。Assets/Resources/ItemData/ItemDataBase.assetを確認してください。");
                        }

                        _tmpSaveData.ownedPermanentItems = _tmpOwnItems;
                        _tmpSaveData.ownedConsumeItems = _tmpOwnConsumeItems;
                        CreateDebugSaveData(_tmpSaveData);
                    }
                }
            }
        }

        EditorGUILayout.Space(10);
        /*--- ゲームプレイ時のみに使用するシステムを記述 ---*/
        if (!Application.isPlaying)
        {
            EditorGUILayout.LabelField("これ以降はプレイ時に表示", EditorStyles.largeLabel);
            return;
        }

        //お金の増減
        _debugMoney = EditorGUILayout.IntSlider("所持金", _debugMoney, 0, 100000);
        _debugUnwashedMoney = EditorGUILayout.IntSlider("所持未洗浄金", _debugUnwashedMoney, 0, 100000);
        PlayerWallet.Local.WashedAmount = _debugMoney;
        PlayerWallet.Local.UnwashedAmount = _debugUnwashedMoney;
    }
}
