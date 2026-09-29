using DesktopNotepadWallpaper.Interop;

namespace DesktopNotepadWallpaper.Services;

public static class WallpaperSetter
{
    public static bool SetWallpaper(string imagePath)
    {
        return NativeMethods.SystemParametersInfo(
            NativeMethods.SPI_SETDESKWALLPAPER, 0, imagePath,
            NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE) != 0;
    }
}
