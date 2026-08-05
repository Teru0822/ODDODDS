using System;
using System.Collections.Generic;

public enum DevilExpression
{
    Normal = 0,//通常
    Mock = 1,//あざ笑う
    Fun = 2,//楽しそう
    Happy = 3,//嬉しい
    Sad = 4,//悲しい
    Angry = 5,//怒っている
}

public enum ConversationType
{
    Conversation,
    Success,
    Fail,
    Other
}

[Serializable]
public class DevilConversationData
{
    public string key;//会話のキー
    public string nextKey;//次の会話のキー
    public string[] lines;// デビルとの会話で登場するテキスト
    public DevilExpression[] devilExpressions;//デビルの表情
    public string bgmKey;//BGMのキー
    public ConversationType conversationType;
}


[Serializable]
public class DevilConversationContainer
{
    public List<DevilConversationData> conversations;
}

