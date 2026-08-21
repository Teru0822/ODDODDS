using UnityEngine;
using TMPro;

public class FontChanger : MonoBehaviour, ILanguage
{
    [Tooltip("0:日本語, 1:英語, 2:中国語, 3:韓国語？")]
    [SerializeField] private TMP_FontAsset[] _fonts;

    public void SettingLanguage(Language language)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if(texts.Length != 0)
        {
            foreach(var text in texts)
            {
                text.font = _fonts[(int)language];
            }
        }
    }
}
