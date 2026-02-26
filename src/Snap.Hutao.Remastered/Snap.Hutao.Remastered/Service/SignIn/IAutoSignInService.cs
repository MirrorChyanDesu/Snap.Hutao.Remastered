using Snap.Hutao.Remastered.ViewModel.User;

public interface IAutoSignInService
{
    ValueTask InitializeAsync(UserAndUid userAndUid, CancellationToken token = default);

    ValueTask RunOnceAsync(UserAndUid userAndUid, CancellationToken token = default);

    bool IsEnabled { get; set; }
}
