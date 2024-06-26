﻿using KsWare.Windows;
using ReactiveUI;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using UnsplashSharp;
using UnsplashSharp.Models;
using UnsplashSharp.Models.Enums;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Color = Windows.UI.Color;

namespace Splashpaper.ViewModels;

public class MainViewModel : ReactiveObject
{
	// From https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/apply-windows-themes?WT.mc_id=DT-MVP-5003978#know-when-dark-mode-is-enabled
	private static PictureTone GetColorTone(Color clr)
	{
		return 5 * clr.G + 2 * clr.R + clr.B > 8 * 128 ? PictureTone.Bright : PictureTone.Dark;
	}

	private readonly HttpClient _httpClient;
	private readonly UISettings _uiSettings;
	private readonly UnsplashService _unsplashService;

	private static readonly string _wallpapersDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Wallpapers");
	private static readonly string _appdataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Splashpaper");
	private static readonly string _currentWallpaperInfoFile = Path.Combine(_appdataDirectory, "current_wallpaper.json");
	private readonly IWindowFactory _windowFactory;

	private readonly ObservableAsPropertyHelper<WallpaperInfoViewModel?> _currentWallpaper;

	private double _themeThreshold = 0.20;

	private string _topics;

	private double _updateIntervalMin = 30;

	public WallpaperInfoViewModel? CurrentWallpaper => _currentWallpaper.Value;

	public string Topics
	{
		get => _topics;
		set => this.RaiseAndSetIfChanged(ref _topics, value);
	}

	public double ThemeThreshold
	{
		get => _themeThreshold;
		set => this.RaiseAndSetIfChanged(ref _themeThreshold, value);
	}

	public double UpdateIntervalMin
	{
		get => _updateIntervalMin;
		set => this.RaiseAndSetIfChanged(ref _updateIntervalMin, value);
	}

	public ReactiveCommand<Unit, Unit> Show { get; }

	public ReactiveCommand<Unit, WallpaperInfoViewModel?> Update { get; }

	public ReactiveCommand<Unit, Unit> Exit { get; }

	public MainViewModel(IWindowFactory windowFactory)
	{
		_windowFactory = windowFactory;
		_httpClient = new HttpClient();
		_uiSettings = new UISettings();
		_unsplashService = new UnsplashService("oNDd__hajD3C6ud3MGnvCFp6oOWed9xUe6CTFNLfZHo");
		Directory.CreateDirectory(_appdataDirectory);

		_topics = "wallpapers, travel, textures-patterns, animals";

		Show = ReactiveCommand.CreateFromTask(ShowWindow);
		Update = ReactiveCommand.CreateFromTask(UpdateWallpaper);
		Exit = ReactiveCommand.Create(() => Application.Current.Shutdown());

		Update.ThrownExceptions
		      .Subscribe(ex => _windowFactory.ShowMessageBox("Could not update a wallpaper", ex.Message));

		new[]
			{
				this.WhenAnyValue(x => x.UpdateIntervalMin)
				    .Select(interval => Update.Select(_ => Unit.Default).StartWith(Unit.Default).Throttle(TimeSpan.FromMinutes(interval)))
				    .Switch(),
				Observable.FromEventPattern<TypedEventHandler<UISettings, object>, object>(
					           h => _uiSettings.ColorValuesChanged += h,
					           h => _uiSettings.ColorValuesChanged -= h)
				          .Select(_ => Unit.Default)
			}
		   .Merge()
		   .InvokeCommand(Update);

		Update.Do(SaveWallpaperInfo)
		      .Merge(Observable.StartAsync(LoadWallpaperInfo))
		      .ToProperty(this, x => x.CurrentWallpaper, out _currentWallpaper, scheduler: RxApp.MainThreadScheduler);
	}

	private async Task<WallpaperInfoViewModel?> LoadWallpaperInfo(CancellationToken cancellation)
	{
		if (!File.Exists(_currentWallpaperInfoFile))
			return null;

		var json = await File.ReadAllTextAsync(_currentWallpaperInfoFile, cancellation);
		return JsonSerializer.Deserialize<WallpaperInfoViewModel>(json);
	}

	private void SaveWallpaperInfo(WallpaperInfoViewModel? wallpaperInfo)
	{
		if (wallpaperInfo is null)
			File.Delete(_currentWallpaperInfoFile);

		File.WriteAllText(_currentWallpaperInfoFile, JsonSerializer.Serialize(wallpaperInfo));
	}

	private async Task ShowWindow()
	{
		await _windowFactory.ShowMainWindow(this);
	}

	private async Task<WallpaperInfoViewModel?> UpdateWallpaper(CancellationToken cancellation = default)
	{
		var screenSize = DisplayInfo.Displays.MaxBy(d => d.Bounds.Width * d.Bounds.Height)!.Bounds.Size;

		string? query = null;
		var theme = GetColorTone(_uiSettings.GetColorValue(UIColorType.Background));

		var picture = await GetRandomPhotosAsync(query, cancellation)
		                   .Where(ph => ph.Width >= screenSize.Width && ph.Height >= screenSize.Height)
		                   .WhereAwaitWithCancellation(async (ph, c) => await CheckPictureToneAsync(ph, c) == theme)
		                   .FirstAsync(cancellation);

		WallpaperInfoViewModel? wallpaperInfo = null;
		var response = await _httpClient.GetAsync(picture.Urls.Full, cancellation);
		if (response.StatusCode == HttpStatusCode.OK)
		{
			Directory.CreateDirectory(_wallpapersDirectory);
			var picturePath = Path.Combine(_wallpapersDirectory, $"{picture.Id}.jpg");
			await using (var fileStream = File.OpenWrite(picturePath))
			{
				await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
				await stream.CopyToAsync(fileStream, cancellation);
			}

			NativeMethods.SetWallpaper(picturePath);

			wallpaperInfo = new WallpaperInfoViewModel
			{
				Title = picture.Description ?? picture.AltDescription ?? picture.Id,
				Author = picture.User.Name,
				Date = picture.CreatedAt,
				Url = picture.Links.Html,
				AuthorUrl = picture.User.Links.Html,
				Location = picture.Location.Name
			};
		}

		CleanupWallpaperDirectory();
		return wallpaperInfo;
	}

	private async ValueTask<PictureTone> CheckPictureToneAsync(Photo Photo, CancellationToken cancellation)
	{
		var response = await _httpClient.GetAsync(Photo.Urls.Thumbnail, cancellation);
		if (response.StatusCode != HttpStatusCode.OK)
		{
			return PictureTone.Bright;
		}

		await using var stream = await response.Content.ReadAsStreamAsync(cancellation);

		using var bitmap = new Bitmap(stream);
		long totalBrightness = 0;
		var pixelCount = bitmap.Width * bitmap.Height;

		for (var y = 0; y < bitmap.Height; y++)
		for (var x = 0; x < bitmap.Width; x++)
		{
			var pixel = bitmap.GetPixel(x, y);
			totalBrightness += (5 * pixel.R + 2 * pixel.G + pixel.B) / 8;
		}

		var tone = totalBrightness / (double)pixelCount;
		Debug.WriteLine($"Tone: {tone:F1}");

		return tone > ThemeThreshold * 256
			? PictureTone.Bright
			: PictureTone.Dark;
	}

	private async IAsyncEnumerable<Photo> GetRandomPhotosAsync(string? query, [EnumeratorCancellation] CancellationToken cancellation)
	{
		var topicsss = await _unsplashService.GetTopicsAsync(orderBy: TopicOrderBy.Featured);

		// var topics = _topics.Split([Environment.NewLine, ",", ";"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
		//                     .Where(topic => !string.IsNullOrWhiteSpace(topic))
		//                     .ToArray();

		var topics = topicsss.Take(1).Select(t => t.Id).ToArray();

		while (!cancellation.IsCancellationRequested)
		{
			var photos = await _unsplashService.GetRandomPhotosAsync(10, topics: topics, orientation: Orientation.Landscape);
			foreach (var photo in photos)
			{
				yield return photo;
			}
		}
	}

	private void CleanupWallpaperDirectory()
	{
		var files = Directory.EnumerateFiles(_wallpapersDirectory)
		                     .OrderByDescending(File.GetCreationTime);

		foreach (var file in files.Skip(10))
		{
			try
			{
				File.Delete(file);
			}
			catch (Exception e)
			{
				Debug.WriteLine(e.Message);
			}
		}
	}
}

internal enum PictureTone
{
	Bright,
	Dark
}