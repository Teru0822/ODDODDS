using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inspecter上で操作できるDictionaryを作成するためのスクリプト
/// </summary>
/// <typeparam name="TKey">検索するための変数</typeparam>
/// <typeparam name="TValue">検索結果を格納する変数</typeparam>
[Serializable]
public class SerializeDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
{
    [SerializeField] private List<DicElement> _dicElements = new List<DicElement>();
    private Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();


    [Serializable]
    public class DicElement
    {
        public TKey Key;
        public TValue Value;

        public DicElement() { } //MissingMethodExceptionを回避するため

        public DicElement(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }

    public Dictionary<TKey, TValue> GetDictionary
    {
        get
        {
            if (_dicElements != null)
            {
                _dictionary.Clear();
                foreach (DicElement element in _dicElements)
                {
                    _dictionary.Add(element.Key, element.Value);
                }
                return _dictionary;
            }
            else
                return null;
        }
    }

    // foreach対応：IEnumeratorを返す
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return GetDictionary.GetEnumerator();
    }

    // 非ジェネリック版（IEnumerableを実装する場合に必要）
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}