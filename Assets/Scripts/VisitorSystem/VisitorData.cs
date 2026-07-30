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
public class RequestElement
{
    public ScriptableObject content;//要求アイテム
    public int num = 1;//個数

    public RequestElement Clone()
    {
        return new RequestElement
        {
            content = content, // ScriptableObjectは参照をコピー
            num = num
        };
    }
}

[Serializable]
public class Request
{
    public List<RequestElement> RequestElements = new();
    public Request Clone()
    {
        Request copy = new Request();

        foreach (var element in RequestElements)
        {
            copy.RequestElements.Add(element.Clone());
        }

        return copy;
    }
}

[Serializable]
public class RewardElement
{
    public ScriptableObject content;//要求アイテム
    public int num = 1;//個数
    
    public RewardElement Clone()
    {
        return new RewardElement
        {
            content = content,
            num = num
        };
    }
}

[Serializable]
public class Reward
{
    public List<RewardElement> RewardElements = new();
    public Reward Clone()
    {
        Reward copy = new Reward();

        foreach (var element in RewardElements)
        {
            copy.RewardElements.Add(element.Clone());
        }

        return copy;
    }
}


[Serializable]
[CreateAssetMenu(menuName = "Data/VisitorData")]
public class VisitorData  : ScriptableObject
{
    public int id;
    public string visitorName;
    public List<Request> requests = new List<Request>();
    public List<Reward> rewards = new List<Reward>();
    [Tooltip("key:会話のキー, value:会話のコンテナの配列（日本語と英語）")]public SerializeDictionary<string, VisitorConversationContainer[]> conversations = new SerializeDictionary<string, VisitorConversationContainer[]>();

}
