using Microsoft.UI.Xaml.Controls;
using System.IO;
using Snap.Hutao.Remastered.Core;
using Snap.Hutao.Remastered.Core.Caching;
using Snap.Hutao.Remastered.Core.IO;
using Snap.Hutao.Remastered.Web.Hoyolab.HoyoPlay;
using Windows.Media.Core;

namespace Snap.Hutao.Remastered.Service.BackgroundMediaPlayer;

[Service(ServiceLifetime.Singleton, typeof(IBackgroundMediaPlayerService))]
internal sealed partial class BackgroundMediaPlayerService : IBackgroundMediaPlayerService
{
    private static readonly HashSet<string> AllowedVideoFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".webm", ".m4v", ".mov", ".wmv", ".avi"
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

            case BackgroundMediaType.OfficialLauncher:
                using (IServiceScope scope = serviceProvider.CreateScope())
                {
                    OfficialLauncherClient? launcherClient = scope.ServiceProvider.GetService<OfficialLauncherClient>();
                    if (launcherClient is not null)
                    {
                        try
                        {
                            string? videoUrl = await launcherClient.GetBackgroundVideoUrlAsync(token).ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(videoUrl))
                            {
                                IImageCache? imageCache = scope.ServiceProvider.GetService<IImageCache>();
                                Uri targetUri = new(videoUrl);

                                if (imageCache is not null)
                                {
                                    try
                                    {
                                        ValueFile file = await imageCache.GetFileFromCacheAsync(targetUri).ConfigureAwait(false);

                                        string filePath = file.ToString();
                                        string cacheDir = HutaoRuntime.GetLocalCacheImageCacheDirectory();
                                        if (Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(cacheDir), StringComparison.OrdinalIgnoreCase) && File.Exists(filePath))
                                        {
                                            await taskContext.SwitchToMainThreadAsync();
                                            element.Source = MediaSource.CreateFromUri(new Uri(filePath));
                                            break;
                                        }

                                        try
                                        {
                                            imageCache.Remove(targetUri);
                                        }
                                        catch
                                        {
                                            // ignore remove failure
                                        }
                                    }
                                    catch
                                    {
                                        // ignore cache error, fallback to streaming
                                    }
                                }

                                element.Source = MediaSource.CreateFromUri(targetUri);
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
