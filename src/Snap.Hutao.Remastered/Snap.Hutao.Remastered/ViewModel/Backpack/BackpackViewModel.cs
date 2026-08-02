// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Snap.Hutao.Remastered.Core;
using Snap.Hutao.Remastered.Core.Database;
using Snap.Hutao.Remastered.Core.Logging;
using Snap.Hutao.Remastered.Model.Entity;
using Snap.Hutao.Remastered.Service.Backpack;
using Snap.Hutao.Remastered.Service.Metadata.ContextAbstraction;
using Snap.Hutao.Remastered.Service.Notification;
using Snap.Hutao.Remastered.Service.Yae.PlayerStore;
using Snap.Hutao.Remastered.UI.Xaml.Data;
using Snap.Hutao.Remastered.UI.Xaml.View.Dialog;
using Snap.Hutao.Remastered.ViewModel.Game;
using System.Collections.Immutable;

namespace Snap.Hutao.Remastered.ViewModel.Backpack;

[Service(ServiceLifetime.Scoped)]
public sealed partial class BackpackViewModel : Abstraction.ViewModel
{
    private readonly BackpackViewModelScopeContext scopeContext;
    private readonly ExclusiveTokenProvider itemsTokenProvider = new();
    private ImmutableArray<BackpackItemView> allItems = [];
    private ImmutableDictionary<BackpackItemCategory, ImmutableArray<BackpackItemView>> categoryItems = [];

    [GeneratedConstructor]
    public partial BackpackViewModel(IServiceProvider serviceProvider);

    public IAdvancedDbCollectionView<BackpackArchive>? Archives
    {
        get;
        set
        {
            AdvancedCollectionViewCurrentChanged.Detach(field, OnCurrentArchiveChanged);
            SetProperty(ref field, value);
            AdvancedCollectionViewCurrentChanged.Attach(value, OnCurrentArchiveChanged);
        }
    }

    [ObservableProperty]
    public partial ImmutableArray<BackpackItemView> Items { get; set; } = [];

    [ObservableProperty]
    public partial int SelectedCategoryIndex { get; set; }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        if (!await scopeContext.MetadataService.InitializeAsync().ConfigureAwait(false))
        {
            return false;
        }

        token.ThrowIfCancellationRequested();

        IAdvancedDbCollectionView<BackpackArchive> archives;
        using (await EnterCriticalSectionAsync().ConfigureAwait(false))
        {
            archives = await scopeContext.BackpackService.GetArchiveCollectionAsync().ConfigureAwait(false);
        }

        await scopeContext.TaskContext.SwitchToMainThreadAsync();

        Archives = archives;
        Archives.MoveCurrentTo(Archives.Source.SelectedOrFirstOrDefault());

        UpdateItemsAsync(Archives.CurrentItem, itemsTokenProvider.GetNewToken()).SafeForget();

        return true;
    }

    protected override void UninitializeOverride()
    {
        using (Archives?.SuppressChangeCurrentItem())
        {
            Archives = default;
        }

        Items = [];
    }

    private void OnCurrentArchiveChanged(object? sender, object? e)
    {
        UpdateItemsAsync(Archives?.CurrentItem, itemsTokenProvider.GetNewToken()).SafeForget();
    }

    partial void OnSelectedCategoryIndexChanged(int value)
    {
        UpdateItemsFilter();
    }

    [Command("AddArchiveCommand")]
    private async Task AddArchiveAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Add archive", "BackpackViewModel.Command"));

        if (Archives is null)
        {
            return;
        }

        BackpackArchiveCreateDialog dialog = await scopeContext.ContentDialogFactory.CreateInstanceAsync<BackpackArchiveCreateDialog>(scopeContext.ServiceProvider).ConfigureAwait(false);
        if (await dialog.GetInputAsync().ConfigureAwait(false) is not (true, { } name))
        {
            return;
        }

        BackpackArchive added = scopeContext.BackpackService.AddArchive(name);

        IAdvancedDbCollectionView<BackpackArchive> archives = await scopeContext.BackpackService.GetArchiveCollectionAsync().ConfigureAwait(false);
        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        Archives = archives;

        BackpackArchive? current = Archives.Source.FirstOrDefault(a => a.InnerId == added.InnerId);
        Archives.MoveCurrentTo(current ?? Archives.Source.FirstOrDefault());

        scopeContext.Messenger.Send(InfoBarMessage.Success(SH.FormatViewPageBackpackArchiveAdded(name)));
    }

    [Command("RemoveArchiveCommand")]
    private async Task RemoveArchiveAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Remove archive", "BackpackViewModel.Command"));

        if (Archives?.CurrentItem is not { } current)
        {
            return;
        }

        ContentDialogResult result = await scopeContext.ContentDialogFactory
            .CreateForConfirmCancelAsync(
                SH.FormatViewPageBackpackRemoveArchiveTitle(current.Name),
                SH.ViewPageBackpackRemoveArchiveContent)
            .ConfigureAwait(false);

        if (result is not ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            using (await EnterCriticalSectionAsync().ConfigureAwait(false))
            {
                await scopeContext.BackpackService.RemoveArchiveAsync(current).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }

        IAdvancedDbCollectionView<BackpackArchive> archives = await scopeContext.BackpackService.GetArchiveCollectionAsync().ConfigureAwait(false);
        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        Archives = archives;
        Archives.MoveCurrentTo(Archives.Source.SelectedOrFirstOrDefault());
    }

    [Command("RefreshByEmbeddedYaeCommand")]
    private async Task RefreshByEmbeddedYaeAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateUI("Refresh backpack", "BackpackViewModel.Command", [("source", "Embedded Yae")]));

        if (!HutaoRuntime.IsProcessElevated)
        {
            await scopeContext.ContentDialogFactory
                .CreateForConfirmAsync(SH.ViewModelYaeProcessNotElevatedTitle, SH.ViewModelYaeProcessNotElevatedDescription)
                .ConfigureAwait(false);
            return;
        }

        if (Archives?.CurrentItem is not { } archive)
        {
            return;
        }

        EmbeddedYaeLaunchExecutionViewModel viewModel = scopeContext.ServiceProvider.GetRequiredService<EmbeddedYaeLaunchExecutionViewModel>();
        if (!await viewModel.InitializeAsync().ConfigureAwait(false))
        {
            return;
        }

        PlayerStoreResult? storeResult = await scopeContext.YaeService.GetPlayerStoreResultAsync(viewModel).ConfigureAwait(false);

        if (storeResult is null)
        {
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewPageBackpackRefreshWarning));
            return;
        }

        if (await scopeContext.BackpackService.RefreshByEmbeddedYaeAsync(archive, storeResult).ConfigureAwait(false))
        {
            scopeContext.Messenger.Send(InfoBarMessage.Success(SH.ViewPageBackpackRefreshSuccess));
        }
        else
        {
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewPageBackpackRefreshWarning));
        }

        await UpdateItemsAsync(archive, itemsTokenProvider.GetNewToken()).ConfigureAwait(false);
    }

    private async ValueTask UpdateItemsAsync(BackpackArchive? archive, CancellationToken token)
    {
        await scopeContext.TaskContext.InvokeOnMainThreadAsync(() => Items = []).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();

        if (archive is null)
        {
            allItems = [];
            categoryItems = [];
            return;
        }

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, CancellationToken);
        BackpackServiceMetadataContext context = await scopeContext.MetadataService
            .GetContextAsync<BackpackServiceMetadataContext>(linkedCts.Token)
            .ConfigureAwait(false);

        allItems = [.. scopeContext.BackpackService
            .GetBackpackItemImmutableArrayByArchiveId(archive.InnerId)
            .Select(item => BackpackItemView.Create(item, context))];

        categoryItems = BuildCategoryViews(allItems);

        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        token.ThrowIfCancellationRequested();

        UpdateItemsFilter();
    }

    private void UpdateItemsFilter()
    {
        BackpackItemCategory category = SelectedCategoryIndex switch
        {
            0 => BackpackItemCategory.Weapon,
            1 => BackpackItemCategory.Reliquary,
            2 => BackpackItemCategory.UpgradeItem,
            3 => BackpackItemCategory.Food,
            4 => BackpackItemCategory.Material,
            5 => BackpackItemCategory.Gadget,
            6 => BackpackItemCategory.Quest,
            7 => BackpackItemCategory.PreciousItem,
            8 => BackpackItemCategory.Furniture,
            _ => BackpackItemCategory.Weapon,
        };

        Items = categoryItems.GetValueOrDefault(category, []);
    }

    private static uint GetRank(BackpackItemView item)
    {
        return item switch
        {
            BackpackWeaponItemView w => (uint)w.Weapon.RankLevel,
            _ when item.Material is not null => (uint)item.Material.RankLevel,
            _ => 1,
        };
    }

    private static ImmutableDictionary<BackpackItemCategory, ImmutableArray<BackpackItemView>> BuildCategoryViews(ImmutableArray<BackpackItemView> all)
    {
        ImmutableDictionary<BackpackItemCategory, ImmutableArray<BackpackItemView>>.Builder builder =
            ImmutableDictionary.CreateBuilder<BackpackItemCategory, ImmutableArray<BackpackItemView>>();

        foreach (BackpackItemCategory cat in Enum.GetValues<BackpackItemCategory>())
        {
            IEnumerable<BackpackItemView> filtered = all
                .Where(item => item.Category == cat && IsCorrectType(item, cat));

            ImmutableArray<BackpackItemView> sorted = cat switch
            {
                BackpackItemCategory.Weapon => [.. filtered
                    .Cast<BackpackWeaponItemView>()
                    .OrderByDescending(w => w.Weapon.RankLevel)
                    .ThenByDescending(w => w.Level)
                    .ThenBy(w => w.Entity.ItemId)],
                BackpackItemCategory.Reliquary => [.. filtered
                    .Cast<BackpackReliquaryItemView>()
                    .OrderByDescending(r => r.Level)
                    .ThenBy(r => r.Entity.ItemId)],
                _ => [.. filtered
                    .OrderByDescending(GetRank)
                    .ThenBy(item => item.Entity.ItemId)],
            };

            builder.Add(cat, sorted);
        }

        return builder.ToImmutable();
    }

    private static bool IsCorrectType(BackpackItemView item, BackpackItemCategory category)
    {
        return category switch
        {
            BackpackItemCategory.Weapon => item is BackpackWeaponItemView,
            BackpackItemCategory.Reliquary => item is BackpackReliquaryItemView,
            _ => item is not BackpackWeaponItemView and not BackpackReliquaryItemView,
        };
    }
}
