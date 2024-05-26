using Splashpaper.ViewModels;

namespace Splashpaper;

public interface IWindowFactory
{
	public Task ShowMainWindow(MainViewModel MainViewModel);

	void ShowMessageBox(string title, string message);
}