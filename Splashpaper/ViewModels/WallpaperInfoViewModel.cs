using ReactiveUI;
using System.Diagnostics;
using System.Reactive;

namespace Splashpaper.ViewModels;

public class WallpaperInfoViewModel
{
	public required string Title { get; init; }

	public required string Url { get; init; }

	public string? Location { get; init; }

	public ReactiveCommand<Unit, Unit> OpenPhoto { get; }

	public string? Author { get; init; }

	public string? AuthorUrl { get; init; }

	public ReactiveCommand<Unit, Unit> OpenAuthorPage { get; }

	public DateTime? Date { get; init; }

	public string ShortTitle => (Title, Location) switch
	{
		// If title is short, just show the title
		({ Length: < 20 }, null) => Title,
		// If title is short and location is available, show both
		({ Length: < 20 }, not null) => $"{Title} ({Location})",
		// If title is long, but there is a location, show location
		(_, not null) => Location!,
		// If title is long and there is no location, cut title to first space after 20 characters
		(_, null) => Title[..(Title.IndexOf(' ', 20) + 1)]
	};

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