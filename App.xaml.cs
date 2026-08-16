using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using voboX.Services;

namespace voboX;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // 全局异常捕获：未处理异常写入 crash.log，避免闪退无痕、便于定位
        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashLog("DispatcherUnhandledException", args.Exception);
            // 不标记 Handled：保持崩溃行为，按原路径复现；堆栈已留档
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            WriteCrashLog("AppDomain.UnhandledException", args.ExceptionObject as Exception);

        // 启动即创建 Box 目录（tempBox / cutBox），保证用户随时可见
        AppPaths.EnsureBoxFolders();
        base.OnStartup(e);
    }

    /// <summary>把未处理异常追加写入应用数据目录的 crash.log</summary>
    private static void WriteCrashLog(string source, Exception? ex)
    {
        try
        {
            var log = Path.Combine(AppPaths.DataDir, "crash.log");
            File.AppendAllText(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n{ex}\n\n");
        }
        catch
        {
            // 日志写入失败忽略
        }
    }
}

