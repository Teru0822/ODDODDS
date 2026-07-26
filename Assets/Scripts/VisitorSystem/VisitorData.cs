using UnityEngine;
using System;
using System.Collections.Generic;

public enum VisitorAnimation
{
    Normal = 0,//通常
    Happy = 1,//嬉しい
    Fun = 2,//楽しそう
    Sad = 3,//悲しい
    Angry = 4,//怒っている
}

[Serializable]
public class VisitorConversationContainer
{
    public VisitorAnimation animation;//会話のアニメーション
    public string lineJp;//会話のテキスト(日本語)
    public string lineEn;//会話のテキスト(英語)
}


[Serializable]
[CreateAssetMenu(menuName = "Data/VisitorData")]
public class VisitorData  : ScriptableObject
{
    public int id;
    public string visitorName;
    public List<ScriptableObject> requests = new List<ScriptableObject>();
    public List<ScriptableObject> rewards = new List<ScriptableObject>();
    [Tooltip("key:会話のキー, value:会話のコンテナの配列（日本語と英語）")]public SerializeDictionary<string, VisitorConversationContainer[]> conversations = new SerializeDictionary<string, VisitorConversationContainer[]>();

}
