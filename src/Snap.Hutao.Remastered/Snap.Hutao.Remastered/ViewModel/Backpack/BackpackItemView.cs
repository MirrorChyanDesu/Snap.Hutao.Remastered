// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Remastered.Model.Entity;
using Snap.Hutao.Remastered.Model.Metadata.Item;
using Snap.Hutao.Remastered.Model.Metadata.Reliquary;
using Snap.Hutao.Remastered.Model.Metadata.Weapon;
using Snap.Hutao.Remastered.Service.Backpack;

namespace Snap.Hutao.Remastered.ViewModel.Backpack;

public class BackpackItemView
{
    public BackpackItem Entity { get; protected init; } = default!;

    public BackpackItemCategory Category { get; protected set; }

    public Material? Material { get; protected set; }

    public string Name => this switch
    {
        BackpackWeaponItemView w => w.Weapon.Name,
        BackpackReliquaryItemView r => r.Reliquary.Name,
        _ => Material?.Name ?? string.Empty,
    };

    public string DisplayCount => Entity.Count > 1 ? $"x{Entity.Count}" : string.Empty;

    public static BackpackItemView Create(BackpackItem entity, BackpackServiceMetadataContext context)
    {
        if (context.IdWeaponMap.TryGetValue(entity.ItemId, out Weapon? weapon))
        {
            return BackpackWeaponItemView.Create(entity, context, weapon);
        }

        if (entity.MainPropId is not null && context.IdReliquaryMap.TryGetValue(entity.ItemId, out Reliquary? reliquary))
        {
            return BackpackReliquaryItemView.Create(entity, context, reliquary);
        }

        // First try to classify by ItemId alone
        // If that returns Material (default), try to get Material metadata and classify by MaterialType
        context.IdMaterialMap.TryGetValue(entity.ItemId, out Material? material);

        BackpackItemView view = new()
        {
            Entity = entity,
            Material = material,
            Category = material.GetCategory(entity.ItemId),
        };

        return view;
    }
}
