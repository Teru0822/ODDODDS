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
    private List<int> _tmpOwnItems = new List<int>();

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
                    _tmpSaveData.virtuePoints = EditorGUILayout.IntField("徳ポイントの数値", _tmpSaveData.virtuePoints);
                    _tmpSaveData.isUnlockPinball = EditorGUILayout.Toggle("ピンボール機能を解放", _tmpSaveData.isUnlockPinball);
                    _tmpSaveData.isUnlockTypewriter = EditorGUILayout.Toggle("タイプライター機能を解放", _tmpSaveData.isUnlockTypewriter);
                    _tmpSaveData.isUnlockMinigame = EditorGUILayout.Toggle("ミニゲーム機能を解放", _tmpSaveData.isUnlockMinigame);
                    _tmpSaveData.isUnlockVisitor = EditorGUILayout.Toggle("訪問者機能を解放", _tmpSaveData.isUnlockVisitor);

                    EditorGUILayout.LabelField(
                    $"登録アイテム数 : {_tmpOwnItems.Count}",
                    EditorStyles.boldLabel);

                    EditorGUILayout.Space();

                    for (int i = 0; i < _tmpOwnItems.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();

                        EditorGUILayout.LabelField(
                            $"[{i}]",
                            GUILayout.Width(30));

                        _tmpOwnItems[i] = EditorGUILayout.IntField(_tmpOwnItems[i]);

                        if (GUILayout.Button("削除", GUILayout.Width(50)))
                        {
                            _tmpOwnItems.RemoveAt(i);
                            GUIUtility.ExitGUI();
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.Space();

                    if (GUILayout.Button("アイテム追加"))
                    {
                        _tmpOwnItems.Add(0);
                    }


                    EditorGUILayout.Space(15);
                    if (GUILayout.Button("セーブデータを作成する"))
                    {
                        _tmpSaveData.ownedItems = _tmpOwnItems;
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
