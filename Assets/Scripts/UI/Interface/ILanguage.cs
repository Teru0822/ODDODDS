using UnityEngine;

public interface ILanguage
{
    /// <summary>
    /// 設定中の言語に応じた処理を実装する
    /// </summary>
    /// <param name="language"></param>
    public void SettingLanguage(Language language);
}
