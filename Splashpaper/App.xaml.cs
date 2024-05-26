using Chapter.Net.WPF.SystemTray;
using Splashpaper.ViewModels;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Splashpaper;

/// <summary>Interaction logic for App.xaml</summary>
public partial class App : Application, IWindowFactory
{
	public static readonly object ContextMenuKey = new();

	private MainViewModel _mainViewModel = null!;

	/// <inheritdoc />
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		_mainViewModel = new MainViewModel(this);

		var contextMenu = (ContextMenu)FindResource(ContextMenuKey)!;
		contextMenu.DataContext = _mainViewModel;

		var icon = new TrayIcon
		{
			Icon = "Splashpaper.exe",
			ContextMenu = contextMenu,
			ClickCommand = _mainViewModel.Show
		};
		icon.Show();
	}

	async Task IWindowFactory.ShowMainWindow(MainViewModel mainViewModel)
	{
		var window = new MainWindow(mainViewModel);
		window.Show();
		await Observable.FromEventPattern(h => window.Closed += h, h => window.Closed -= h)
		                .FirstAsync();
	}

	/// <inheritdoc />
	public void ShowMessageBox(string title, string message)
	{
		MessageBox.Show(message, title);
	}
}