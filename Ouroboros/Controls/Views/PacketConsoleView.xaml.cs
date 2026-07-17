using System.Windows;
using Ouroboros.ViewModel;

namespace Ouroboros.Controls.Views;

/// <summary>Interaction logic for PacketConsoleView.xaml — live packet log + craft/inject box.</summary>
public sealed partial class PacketConsoleView
{
    public PacketConsoleView() => InitializeComponent();

    private PacketConsoleViewModel? ViewModel => DataContext as PacketConsoleViewModel;

    private void ClearButton_Click(object sender, RoutedEventArgs e) => ViewModel?.ClearLog();

    private void SendServerButton_Click(object sender, RoutedEventArgs e) => ViewModel?.SendToServer();

    private void SendClientButton_Click(object sender, RoutedEventArgs e) => ViewModel?.SendToClient();
}
