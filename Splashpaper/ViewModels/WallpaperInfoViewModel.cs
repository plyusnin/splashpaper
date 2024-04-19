using ReactiveUI;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;

namespace Splashpaper.ViewModels;

public class WallpaperInfoViewModel
{
	public required string Title { get; init; }

	public required string Url { get; init; }

	public string? Locaton { get; init; }

	public ReactiveCommand<Unit, Unit> OpenPhoto { get; }

	public string? Author { get; init; }

	public string? AuthorUrl { get; init; }

	public ReactiveCommand<Unit, Unit> OpenAuthorPage { get; }

	public WallpaperInfoViewModel()
	{
		OpenPhoto = ReactiveCommand.Create(OpenPhotoImpl);
		OpenAuthorPage = ReactiveCommand.Create(OpenAuthorPageImpl);
	}

	private void OpenPhotoImpl()
	{
		Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true });
	}

	private void OpenAuthorPageImpl()
	{
		Process.Start(new ProcessStartInfo(AuthorUrl!) { UseShellExecute = true });
	}
}