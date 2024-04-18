using KsWare.Windows;
using ReactiveUI;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reactive;
using System.Runtime.CompilerServices;
using UnsplashSharp;
using UnsplashSharp.Models;
using UnsplashSharp.Models.Enums;

namespace Splashpaper.ViewModels;

public class MainViewModel : ReactiveObject
{
	private readonly HttpClient _httpClient;
	private readonly UnsplashService _unsplashService;

	public ReactiveCommand<Unit, Unit> Update { get; }

	public MainViewModel()
	{
		_httpClient = new HttpClient();

		_unsplashService = new UnsplashService("oNDd__hajD3C6ud3MGnvCFp6oOWed9xUe6CTFNLfZHo");

		Update = ReactiveCommand.CreateFromTask(UpdateWallpaper);
	}

	private async Task UpdateWallpaper(CancellationToken cancellation)
	{
		var screenSize = DisplayInfo.Displays.MaxBy(d => d.Bounds.Width * d.Bounds.Height)!.Bounds.Size;

		var picture = await GetRandomPhotosAsync(cancellation)
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
		}
	}

	private async IAsyncEnumerable<Photo> GetRandomPhotosAsync([EnumeratorCancellation] CancellationToken cancellation)
	{
		while (!cancellation.IsCancellationRequested)
		{
			var photos = await _unsplashService.GetRandomPhotosAsync(10, orientation: Orientation.Landscape);
			foreach (var photo in photos)
			{
				yield return photo;
			}
		}
	}
}