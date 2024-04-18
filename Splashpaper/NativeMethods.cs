using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Splashpaper;

public static class NativeMethods
{
	[SuppressMessage("ReSharper", "InconsistentNaming")]
	public enum SPI : uint
	{
		SPI_SETDESKWALLPAPER = 20
	}

	[SuppressMessage("ReSharper", "InconsistentNaming")]
	public enum SPIF : uint
	{
		SPIF_UPDATEINIFILE = 0x01,
		SPIF_SENDCHANGE = 0x02
	}

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	private static extern int SystemParametersInfo(SPI uAction, int uParam, string lpvParam, SPIF fuWinIni);

	public static void SetWallpaper(string picturePath)
	{
		SystemParametersInfo(SPI.SPI_SETDESKWALLPAPER, 0, picturePath, SPIF.SPIF_UPDATEINIFILE | SPIF.SPIF_SENDCHANGE);
	}
}