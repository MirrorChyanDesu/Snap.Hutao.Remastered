// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Snap.Hutao.Remastered.Core;
using Snap.Hutao.Remastered.Core.Database;
using Snap.Hutao.Remastered.Core.Logging;
using Snap.Hutao.Remastered.Model.Entity;
using Snap.Hutao.Remastered.Model.Intrinsic;
using Snap.Hutao.Remastered.Model.Intrinsic.Frozen;
using Snap.Hutao.Remastered.Model.Metadata;
using Snap.Hutao.Remastered.Model.Metadata.Converter;
using Snap.Hutao.Remastered.Service.Backpack;
using Snap.Hutao.Remastered.Service.Metadata.ContextAbstraction;
using Snap.Hutao.Remastered.Service.Notification;
using Snap.Hutao.Remastered.Service.Yae.PlayerStore;
using Snap.Hutao.Remastered.UI.Xaml.Control.AutoSuggestBox;
using Snap.Hutao.Remastered.UI.Xaml.Data;
using Snap.Hutao.Remastered.UI.Xaml.View.Dialog;
using Snap.Hutao.Remastered.ViewModel.Game;

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;

namespace Snap.Hutao.Remastered.ViewModel.Backpack;

[Service(ServiceLifetime.Scoped)]
public sealed partial class BackpackViewModel : Abstraction.ViewModel
{
    private readonly BackpackViewModelScopeContext scopeContext;
    private readonly ExclusiveTokenProvider itemsTokenProvider = new();
    private ImmutableDictionary<BackpackItemCategory, ImmutableArray<BackpackItemView>> categoryItems = [];
    private FrozenDictionary<uint, int> foodQualityMap = FrozenDictionary<uint, int>.Empty;
    private FrozenDictionary<uint, CookFoodType> foodTypeMap = FrozenDictionary<uint, CookFoodType>.Empty;
    private ImmutableDictionary<BackpackItemCategory, FrozenDictionary<string, SearchToken>> categoryTokens = [];

    private static readonly Uri LockedIconUri = new("ms-appx:///Resource/Icon/UI_Icon_Locked.png");
    private static readonly Uri UnlockedIconUri = new("ms-appx:///Resource/Icon/UI_Icon_Unlock.png");
    private static readonly Uri MarkIconUri = new("ms-appx:///Resource/Icon/UI_Icon_UGC_Collect.png");

    private static readonly Uri SuspiciousFoodIconUri = new("ms-appx:///Resource/Icon/Icon_Common_Cook.png");
    private static readonly Uri NormalFoodIconUri = new("ms-appx:///Resource/Icon/Icon_Good_Cook.png");
    private static readonly Uri DeliciousFoodIconUri = new("ms-appx:///Resource/Icon/Icon_Perfect_Cook.png");

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

    [ObservableProperty]
    public partial SearchData? SearchData { get; set; }

    [ObservableProperty]
    public partial double? FilterLevel { get; set; }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        // Set empty SearchData so AutoSuggestTokenBox has a non-null binding target
        SearchData = SearchData.Create(FrozenDictionary<string, SearchToken>.Empty);

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
        BuildSearchData();
        UpdateItemsFilter();
    }

    partial void OnFilterLevelChanged(double? value) => UpdateItemsFilter();

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
            categoryItems = [];
            return;
        }

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, CancellationToken);
        BackpackServiceMetadataContext context = await scopeContext.MetadataService
            .GetContextAsync<BackpackServiceMetadataContext>(linkedCts.Token)
            .ConfigureAwait(false);

        ImmutableArray<BackpackItemView> allItems = [.. scopeContext.BackpackService
            .GetBackpackItemImmutableArrayByArchiveId(archive.InnerId)
            .Select(item => BackpackItemView.Create(item, context))];

        categoryItems = BuildCategoryViews(allItems);

        // Build food quality/type reverse lookup maps
        Dictionary<uint, int> qualityMap = [];
        Dictionary<uint, CookFoodType> typeMap = [];
        foreach (ref readonly CookRecipe recipe in context.CookRecipes.AsSpan())
        {
            ImmutableArray<IdCount> outputs = recipe.QualityOutput;
            for (int i = 0; i < outputs.Length; i++)
            {
                qualityMap.TryAdd(outputs[i].Id, i);
                typeMap.TryAdd(outputs[i].Id, recipe.FoodType);
            }
        }

        foodQualityMap = qualityMap.ToFrozenDictionary();
        foodTypeMap = typeMap.ToFrozenDictionary();

        // Pre-build token dictionaries for all categories (on background thread)
        ImmutableDictionary<BackpackItemCategory, FrozenDictionary<string, SearchToken>>.Builder tokenBuilder =
            ImmutableDictionary.CreateBuilder<BackpackItemCategory, FrozenDictionary<string, SearchToken>>();
        foreach (BackpackItemCategory cat in Enum.GetValues<BackpackItemCategory>())
        {
            ImmutableArray<BackpackItemView> catItems = categoryItems.GetValueOrDefault(cat, []);
            tokenBuilder.Add(cat, BuildTokenDictionary(cat, catItems));
        }

        categoryTokens = tokenBuilder.ToImmutable();

        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        token.ThrowIfCancellationRequested();

        BuildSearchData();
        UpdateItemsFilter();
    }

    [Command("FilterCommand")]
    private void ApplyFilter()
    {
        UpdateItemsFilter();
    }

    private static readonly ImmutableArray<BackpackItemCategory> CategoryIndexMap = [
        BackpackItemCategory.Weapon,
        BackpackItemCategory.Reliquary,
        BackpackItemCategory.UpgradeItem,
        BackpackItemCategory.Food,
        BackpackItemCategory.Material,
        BackpackItemCategory.Gadget,
        BackpackItemCategory.Quest,
        BackpackItemCategory.PreciousItem,
        BackpackItemCategory.Furniture,
    ];

    private BackpackItemCategory GetSelectedCategory()
    {
        uint index = (uint)SelectedCategoryIndex;
        return index < (uint)CategoryIndexMap.Length
            ? CategoryIndexMap[(int)index]
            : BackpackItemCategory.Weapon;
    }

    private void BuildSearchData()
    {
        SearchData = SearchData.Create(categoryTokens.GetValueOrDefault(GetSelectedCategory(), FrozenDictionary<string, SearchToken>.Empty));
    }

    private FrozenDictionary<string, SearchToken> BuildTokenDictionary(BackpackItemCategory category, ImmutableArray<BackpackItemView> items)
    {
        List<KeyValuePair<string, SearchToken>> tokens = [];

        switch (category)
        {
            case BackpackItemCategory.Weapon:
                // Weapon type tokens
                tokens.AddRange(IntrinsicFrozen.WeaponTypeNameValues
                    .Where(nv => nv.Value is not WeaponType.WEAPON_NONE)
                    .Select(nv => KeyValuePair.Create(nv.Name, new SearchToken(SearchTokenKind.WeaponType, nv.Name, (int)nv.Value, iconUri: WeaponTypeIconConverter.WeaponTypeToIconUri(nv.Value)))));

                // Lock state tokens
                tokens.Add(KeyValuePair.Create(SH.ViewPageBackpackFilterLocked, new SearchToken(SearchTokenKind.BackpackLockState, SH.ViewPageBackpackFilterLocked, 0, iconUri: LockedIconUri)));
                tokens.Add(KeyValuePair.Create(SH.ViewPageBackpackFilterUnlocked, new SearchToken(SearchTokenKind.BackpackLockState, SH.ViewPageBackpackFilterUnlocked, 1, iconUri: UnlockedIconUri)));
                break;

            case BackpackItemCategory.Reliquary:
                foreach (EquipType equipType in Enum.GetValues<EquipType>())
                {
                    if (equipType is EquipType.EQUIP_NONE or EquipType.EQUIP_WEAPON)
                    {
                        continue;
                    }

                    string name = equipType.GetLocalizedDescriptionOrDefault(SH.ResourceManager, CultureInfo.CurrentCulture) ?? equipType.ToString();
                    tokens.Add(KeyValuePair.Create(name, new SearchToken(SearchTokenKind.BackpackEquipType, name, (int)equipType, sideIconUri: EquipTypeIconConverter.EquipTypeToIconUri(equipType))));
                }

                // Reliquary set tokens (use sideIconUri for colored version)
                HashSet<string> seen = [];
                foreach (BackpackReliquaryItemView reliquary in items.OfType<BackpackReliquaryItemView>())
                {
                    if (reliquary.SetName is { } name && reliquary.SetIconUri is { } uri && seen.Add(name))
                    {
                        tokens.Add(KeyValuePair.Create(name, new SearchToken(SearchTokenKind.BackpackReliquarySet, name, 0, sideIconUri: uri)));
                    }
                }

                // Lock state tokens
                tokens.Add(KeyValuePair.Create(SH.ViewPageBackpackFilterLocked, new SearchToken(SearchTokenKind.BackpackLockState, SH.ViewPageBackpackFilterLocked, 0, iconUri: LockedIconUri)));
                tokens.Add(KeyValuePair.Create(SH.ViewPageBackpackFilterUnlocked, new SearchToken(SearchTokenKind.BackpackLockState, SH.ViewPageBackpackFilterUnlocked, 1, iconUri: UnlockedIconUri)));

                // Mark state tokens
                tokens.Add(KeyValuePair.Create(SH.ViewPageBackpackFilterMarked, new SearchToken(SearchTokenKind.BackpackMarkState, SH.ViewPageBackpackFilterMarked, 0, iconUri: MarkIconUri)));
                tokens.Add(KeyValuePair.Create(SH.ViewPageBackpackFilterUnmarked, new SearchToken(SearchTokenKind.BackpackMarkState, SH.ViewPageBackpackFilterUnmarked, 1, iconUri: MarkIconUri)));
                break;

            case BackpackItemCategory.Food:
                // Cook food type tokens
                foreach (CookFoodType foodType in Enum.GetValues<CookFoodType>())
                {
                    if (foodType is CookFoodType.COOK_FOOD_NONE or CookFoodType.COOK_RECIPE)
                    {
                        continue;
                    }

                    string name = foodType.GetLocalizedDescriptionOrDefault(SH.ResourceManager, CultureInfo.CurrentCulture)!;
                    Uri iconUri = CookFoodTypeIconConverter.CookFoodTypeToIconUri(foodType);
                    tokens.Add(KeyValuePair.Create(name, new SearchToken(SearchTokenKind.BackpackCookFoodType, name, (int)foodType, sideIconUri: iconUri)));
                }

                // Food quality tokens
                tokens.Add(KeyValuePair.Create(SH.ViewPageBackpackFilterFoodQualitySuspicious, new SearchToken(SearchTokenKind.BackpackFoodQuality, SH.ViewPageBackpackFilterFoodQualitySuspicious, 0, sideIconUri: SuspiciousFoodIconUri)));
                tokens.Add(KeyValuePair.Create(SH.ViewPageBackpackFilterFoodQualityNormal, new SearchToken(SearchTokenKind.BackpackFoodQuality, SH.ViewPageBackpackFilterFoodQualityNormal, 1, sideIconUri: NormalFoodIconUri)));
                tokens.Add(KeyValuePair.Create(SH.ViewPageBackpackFilterFoodQualityDelicious, new SearchToken(SearchTokenKind.BackpackFoodQuality, SH.ViewPageBackpackFilterFoodQualityDelicious, 2, sideIconUri: DeliciousFoodIconUri)));
                break;
        }

        // Item quality tokens (after category-specific tokens)
        tokens.AddRange(IntrinsicFrozen.ItemQualityNameValues
            .Select(nv => KeyValuePair.Create(nv.Name, new SearchToken(SearchTokenKind.BackpackQuality, nv.Name, (int)nv.Value, quality: QualityColorConverter.QualityToColor(nv.Value)))));

        return tokens.ToFrozenDictionary();
    }

    private void UpdateItemsFilter()
    {
        BackpackItemCategory category = GetSelectedCategory();
        ImmutableArray<BackpackItemView> items = categoryItems.GetValueOrDefault(category, []);
        Predicate<BackpackItemView>? predicate = BackpackFilter.Compile(SearchData, FilterLevel, foodQualityMap, foodTypeMap);
        Items = predicate is null ? items : [.. items.Where(item => predicate(item))];
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
