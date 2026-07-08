using Microsoft.UI.Xaml.Controls;
using System.IO;
using Snap.Hutao.Remastered.Core;
using Snap.Hutao.Remastered.Core.Caching;
using Snap.Hutao.Remastered.Core.IO;
using Snap.Hutao.Remastered.Web.Hutao.Wallpaper;
using Snap.Hutao.Remastered.Web.Response;
using Windows.Media.Core;

namespace Snap.Hutao.Remastered.Service.BackgroundMediaPlayer;

[Service(ServiceLifetime.Singleton, typeof(IBackgroundMediaPlayerService))]
internal sealed partial class BackgroundMediaPlayerService : IBackgroundMediaPlayerService
{
    private static readonly HashSet<string> AllowedVideoFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".wmv", ".avi", ".webm"
    };

    private readonly BackgroundMediaPlayerOptions options;
    private readonly IServiceProvider serviceProvider;

    [GeneratedConstructor]
    public partial BackgroundMediaPlayerService(IServiceProvider serviceProvider);

    public async ValueTask UpdateMediaPlayerElementAsync(MediaPlayerElement element, CancellationToken token = default)
    {
        if (element is null)
        {
            return;
        }

        ITaskContext taskContext = TaskContext.GetForDependencyObject(element);

        await taskContext.SwitchToMainThreadAsync();

        element.AutoPlay = true;
        element.AreTransportControlsEnabled = false;

        if (element.MediaPlayer is not null)
        {
            element.MediaPlayer.IsMuted = options.IsMuted;
            element.MediaPlayer.IsLoopingEnabled = options.IsLooping;
        }

        switch (options.BackgroundMediaType)
        {
            case BackgroundMediaType.LocalFolder:
                string folder = string.IsNullOrEmpty(options.BackgroundMediaPath) ? HutaoRuntime.GetDataBackgroundDirectory() : options.BackgroundMediaPath!;

                if (!Directory.Exists(folder))
                {
                    element.Source = null;
                    return;
                }

                IEnumerable<string> files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                    .Where(p => AllowedVideoFormats.Contains(Path.GetExtension(p)));

                string? selected = files.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();

                if (selected is null)
                {
                    element.Source = null;
                    return;
                }

                // Ensure local file path uses file:// scheme for MediaSource
                                element.Source = MediaSource.CreateFromUri(new Uri(Path.GetFullPath(selected)));
                break;

            case BackgroundMediaType.HutaoWeb:
                // If BackgroundMediaPath is a URL, try caching it and play from cache; fallback to streaming if cache fails.
                using (IServiceScope scope = serviceProvider.CreateScope())
                {
                    IImageCache? imageCache = scope.ServiceProvider.GetService<IImageCache>();

                    if (!string.IsNullOrEmpty(options.BackgroundMediaPath) && Uri.IsWellFormedUriString(options.BackgroundMediaPath, UriKind.Absolute))
                    {
                        Uri targetUri = new Uri(options.BackgroundMediaPath);

                        if (imageCache is not null)
                        {
                            try
                            {
                                ValueFile file = await imageCache.GetFileFromCacheAsync(targetUri).ConfigureAwait(false);

                                // Verify cached file path is under Hutao cache directory and exists.
                                string filePath = file.ToString();
                                string cacheDir = HutaoRuntime.GetLocalCacheImageCacheDirectory();
                                if (!Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(cacheDir), StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
                                {
                                    // If verification fails, remove and fallback to streaming.
                                    try
                                    {
                                        imageCache.Remove(targetUri);
                                    }
                                    catch
                                    {
                                        // ignore remove failure
                                    }

                                    element.Source = MediaSource.CreateFromUri(targetUri);
                                    break;
                                }

                                await taskContext.SwitchToMainThreadAsync();

                                // Use file path as URI for local file playback.
                                element.Source = MediaSource.CreateFromUri(new Uri(filePath));
                                break;
                            }
                            catch
                            {
                                // ignore cache error and fallback to streaming
                            }
                        }

                        element.Source = MediaSource.CreateFromUri(targetUri);
                        break;
                    }

                    // No explicit URL provided: try to get wallpaper from Hutao wallpaper client and cache it
                    HutaoWallpaperClient? wallpaperClient = scope.ServiceProvider.GetService<HutaoWallpaperClient>();
                    if (wallpaperClient is not null)
                    {
                        try
                        {
                            Response<Wallpaper> resp = await wallpaperClient.GetTodayWallpaperAsync(token).ConfigureAwait(false);
                            if (resp?.Data is { } wallpaper && wallpaper.Url is { } url)
                            {
                                if (imageCache is not null)
                                {
                                    try
                                    {
                                        ValueFile file = await imageCache.GetFileFromCacheAsync(url).ConfigureAwait(false);

                                        string filePath = file.ToString();
                                        string cacheDir = HutaoRuntime.GetLocalCacheImageCacheDirectory();
                                        if (!Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(cacheDir), StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
                                        {
                                            try
                                            {
                                                imageCache.Remove(url);
                                            }
                                            catch
                                            {
                                                // ignore remove failure
                                            }

                                            element.Source = MediaSource.CreateFromUri(url);
                                            break;
                                        }

                                        await taskContext.SwitchToMainThreadAsync();
                                        // Use file:// style URI for local file playback
                                        element.Source = MediaSource.CreateFromUri(new Uri(Path.GetFullPath(filePath)));
                                        break;
                                    }
                                    catch
                                    {
                                        // ignore cache error, fallback to streaming
                                    }
                                }

                                element.Source = MediaSource.CreateFromUri(url);
                                break;
                            }
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    element.Source = null;
                }

                break;

            case BackgroundMediaType.None:
            default:
                // Clear source
                element.Source = null;
                break;
        }
    }
}
