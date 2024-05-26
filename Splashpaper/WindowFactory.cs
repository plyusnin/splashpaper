using Splashpaper.ViewModels;

namespace Splashpaper;

public interface IWindowFactory
{
	public Task ShowMainWindow(MainViewModel MainViewModel);
}