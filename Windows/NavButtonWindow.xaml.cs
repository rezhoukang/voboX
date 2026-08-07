using System.Windows;

namespace voboX.Windows;

/// <summary>
/// 导航展开/收起按钮悬浮窗：
/// 收起时在主窗口左侧外（左箭头=展开）；展开时移到导航栏左侧（右箭头=收起）。
/// </summary>
public partial class NavButtonWindow : Window
{
    public event Action? Clicked;

    /// <summary>设置按钮图标（MDL2 字符）</summary>
    public string Icon
    {
        set => Btn.Tag = value;
    }

    public NavButtonWindow()
    {
        InitializeComponent();
    }

    private void Btn_Click(object sender, RoutedEventArgs e) => Clicked?.Invoke();
}
