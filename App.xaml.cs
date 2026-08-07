using System.Configuration;
using System.Data;
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
        // 启动即创建 Box 目录（Tempbox / Cutbox），保证用户随时可见
        AppPaths.EnsureBoxFolders();
        base.OnStartup(e);
    }
}

