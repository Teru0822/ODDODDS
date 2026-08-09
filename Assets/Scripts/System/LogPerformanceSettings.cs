using UnityEngine;

/// <summary>
/// ログ出力のコストを下げるための設定。
///
/// Unity の Debug.Log は既定で毎回スタックトレースを収集する。これが非常に重く、
/// 毎フレーム何度も呼ぶとフレームレートが目に見えて落ちる。
/// 情報ログと警告についてはトレースを切り、原因追跡が必要なエラーと例外だけ残す。
///
/// 配置は不要（起動時に自動適用）。
/// </summary>
public static class LogPerformanceSettings
{
    /// <summary>
    /// 情報ログ(Debug.Log)自体を止めるか。
    /// true にすると Warning / Error / Exception だけが出力される。
    /// デバッグ中に元へ戻したい場合は <see cref="SetInfoLogsEnabled"/> を呼ぶ。
    /// </summary>
    private const bool SuppressInfoLogsOnStartup = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        // スタックトレースの収集がログコストの大半を占めるので、情報系は切る
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);

        // エラーと例外は発生箇所を追えないと困るので残す
        Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
        Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);
        Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);

        SetInfoLogsEnabled(!SuppressInfoLogsOnStartup);
    }

    /// <summary>
    /// 情報ログ(Debug.Log)の出力可否を切り替える。
    /// false にすると Warning 以上だけになり、大量のデバッグ出力による負荷をほぼゼロにできる。
    /// </summary>
    public static void SetInfoLogsEnabled(bool enabled)
    {
        Debug.unityLogger.filterLogType = enabled ? LogType.Log : LogType.Warning;
    }
}
