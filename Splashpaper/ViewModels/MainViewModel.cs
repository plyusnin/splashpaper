using KsWare.Windows;
using ReactiveUI;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using UnsplashSharp;
using UnsplashSharp.Models;
using UnsplashSharp.Models.Enums;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Splashpaper.ViewModels;

public class MainViewModel : ReactiveObject
{
	// From https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/apply-windows-themes?WT.mc_id=DT-MVP-5003978#know-when-dark-mode-is-enabled
	private static bool IsColorLight(Color clr)
	{
		return 5 * clr.G + 2 * clr.R + clr.B > 8 * 128;
	}

	private readonly HttpClient _httpClient;
	private readonly UISettings _uiSettings;
	private readonly UnsplashService _unsplashService;

	private WallpaperInfoViewModel? _currentWallpaper;

	public WallpaperInfoViewModel? CurrentWallpaper
	{
		get => _currentWallpaper;
		set => this.RaiseAndSetIfChanged(ref _currentWallpaper, value);
	}

	public ReactiveCommand<Unit, Unit> Update { get; }

	public MainViewModel()
	{
		_httpClient = new HttpClient();

		_unsplashService = new UnsplashService("oNDd__hajD3C6ud3MGnvCFp6oOWed9xUe6CTFNLfZHo");

		Update = ReactiveCommand.CreateFromTask(UpdateWallpaper);

		_uiSettings = new UISettings();

		new[]
			{
				Observable.Interval(TimeSpan.FromMinutes(30)),
				Observable.FromEventPattern<TypedEventHandler<UISettings, object>, object>(
					           h => _uiSettings.ColorValuesChanged += h,
					           h => _uiSettings.ColorValuesChanged -= h)
				          .Select(_ => 0L)
			}
		   .Merge()
		   .Select(_ => Observable.StartAsync(UpdateWallpaper))
		   .Switch()
		   .Subscribe();
	}

	private async Task UpdateWallpaper(CancellationToken cancellation = default)
	{
		var screenSize = DisplayInfo.Displays.MaxBy(d => d.Bounds.Width * d.Bounds.Height)!.Bounds.Size;

		var isLightTheme = IsColorLight(_uiSettings.GetColorValue(UIColorType.Background));

		var query = isLightTheme ? null : "dim";

		var picture = await GetRandomPhotosAsync(query, cancellation)
		                   .Where(ph => ph.Width >= screenSize.Width && ph.Height >= screenSize.Height)
		                   .FirstAsync(cancellation);

		var response = await _httpClient.GetAsync(picture.Urls.Full, cancellation);
		if (response.StatusCode == HttpStatusCode.OK)
		{
			var wallpapersDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Wallpapers");
			Directory.CreateDirectory(wallpapersDirectory);
			var picturePath = Path.Combine(wallpapersDirectory, $"{picture.Id}.jpg");
			await using (var fileStream = File.OpenWrite(picturePath))
			{
				await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
				await stream.CopyToAsync(fileStream, cancellation);
			}

			NativeMethods.SetWallpaper(picturePath);

			CurrentWallpaper = new WallpaperInfoViewModel
			{
				Title = picture.Description ?? picture.AltDescription ?? picture.Id,
				Author = picture.User.Name,
				Url = picture.Links.Html,
				AuthorUrl = picture.User.Links.Html,
				Locaton = picture.Location.Name
			};
		}
	}

	private async IAsyncEnumerable<Photo> GetRandomPhotosAsync(string? query, [EnumeratorCancellation] CancellationToken cancellation)
	{
		while (!cancellation.IsCancellationRequested)
		{
			var photos = await _unsplashService.GetRandomPhotosAsync(10, query: query, orientation: Orientation.Landscape);
			foreach (var photo in photos)
			{
				yield return photo;
			}
		}
	}
}