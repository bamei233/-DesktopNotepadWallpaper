namespace DesktopNotepadWallpaper.Services;

public interface IWallpaperSetter
{
    bool Set(string imagePath);
}

public sealed class Win32WallpaperSetter : IWallpaperSetter
{
    public bool Set(string imagePath)
    {
        return WallpaperSetter.SetWallpaper(imagePath);
    }
}
