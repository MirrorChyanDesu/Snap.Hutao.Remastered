using Microsoft.UI.Xaml.Controls;

namespace Snap.Hutao.Remastered.Service.BackgroundMediaPlayer;

internal interface IBackgroundMediaPlayerService
{
    ValueTask UpdateMediaPlayerElementAsync(MediaPlayerElement element, CancellationToken token = default);

    void Pause();

    void Play();
}
