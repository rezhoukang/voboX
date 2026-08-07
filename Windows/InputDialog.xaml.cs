using System.Windows;
using System.Windows.Input;

namespace voboX.Windows;

/// <summary>通用输入对话框</summary>
public partial class InputDialog : Window
{
    public string Value => InputBox.Text;

    public InputDialog(string title, string label, string defaultValue)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = label;
        InputBox.Text = defaultValue;
        Loaded += (s, e) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Ok_Click(sender, e);
        else if (e.Key == Key.Escape)
            Cancel_Click(sender, e);
    }
}
