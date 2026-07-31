// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Remastered.Model.Intrinsic;
using Snap.Hutao.Remastered.Model.Intrinsic.Format;
using Snap.Hutao.Remastered.Web.Hoyolab.Takumi.GameRecord.Avatar;
using System.Collections.Immutable;
using System.Globalization;

namespace Snap.Hutao.Remastered.Service.AvatarInfo.Factory;

public static class ReliquaryScoreCalculator
{
    public static double Calculate(
        ImmutableArray<FightProperty> recommendedSubProperties,
        ImmutableArray<ReliquaryProperty> subProperties,
        EnergyType energyType)
    {
        bool hasCritHurt = recommendedSubProperties.Contains(FightProperty.FIGHT_PROP_CRITICAL_HURT);

        double totalScore = 0;

        foreach (ReliquaryProperty subProperty in subProperties)
        {
            double weight = GetWeight(subProperty.PropertyType, recommendedSubProperties, hasCritHurt, energyType);
            if (weight <= 0)
            {
                continue;
            }

            double value = ParseValue(subProperty.PropertyType, subProperty.Value);
            double score = subProperty.PropertyType switch
            {
                FightProperty.FIGHT_PROP_CRITICAL => value * 2.0 * weight,
                FightProperty.FIGHT_PROP_CRITICAL_HURT => value * 1.0 * weight,
                FightProperty.FIGHT_PROP_ELEMENT_MASTERY => value * 0.33 * weight,
                FightProperty.FIGHT_PROP_CHARGE_EFFICIENCY => value * 1.1979 * weight,
                FightProperty.FIGHT_PROP_HP_PERCENT => value * 1.33 * weight,
                FightProperty.FIGHT_PROP_ATTACK_PERCENT => value * 1.33 * weight,
                FightProperty.FIGHT_PROP_DEFENSE_PERCENT => value * 1.06 * weight,
                FightProperty.FIGHT_PROP_ATTACK => value * 0.398 * 0.5 * weight,
                FightProperty.FIGHT_PROP_HP => value * 0.026 * 0.66 * weight,
                FightProperty.FIGHT_PROP_DEFENSE => value * 0.335 * 0.66 * weight,
                _ => 0,
            };

            totalScore += score;
        }

        return totalScore;
    }

    private static double GetWeight(
        FightProperty propertyType,
        ImmutableArray<FightProperty> recommendedSubProperties,
        bool hasCritHurt,
        EnergyType energyType)
    {
        bool isRecommended = recommendedSubProperties.Contains(propertyType);

        // Special handling for Energy Recharge when not in recommended list
        if (propertyType is FightProperty.FIGHT_PROP_CHARGE_EFFICIENCY && !isRecommended)
        {
            return GetChargeEfficiencyWeight(hasCritHurt, energyType);
        }

        if (!isRecommended)
        {
            return 0;
        }

        // Base weight: 100%
        double weight = 1.0;

        // Small/flat substats get 50% weight reduction when effective
        if (propertyType is FightProperty.FIGHT_PROP_HP or FightProperty.FIGHT_PROP_ATTACK or FightProperty.FIGHT_PROP_DEFENSE)
        {
            weight *= 0.5;
        }

        return weight;
    }

    private static double GetChargeEfficiencyWeight(bool hasCritHurt, EnergyType energyType)
    {
        bool isSpecialEnergy = energyType is not EnergyType.SPECIAL_ENERGY_NONE;

        // Special energy type (Mavuika/Skirk) + no crit hurt recommended -> 100%
        if (isSpecialEnergy && !hasCritHurt)
        {
            return 1.0;
        }

        // Special energy type + crit hurt recommended -> 0%
        if (isSpecialEnergy)
        {
            return 0;
        }

        // Normal energy + crit hurt recommended -> 20%
        if (hasCritHurt)
        {
            return 0.2;
        }

        // Normal energy + no crit hurt recommended -> 100%
        return 1.0;
    }

    private static double ParseValue(FightProperty propertyType, string value)
    {
        FormatMethod formatMethod = propertyType.GetFormatMethod();
        if (formatMethod is FormatMethod.Percent)
        {
            if (value.EndsWith('%'))
            {
                value = value[..^1];
            }

            return double.Parse(value, CultureInfo.InvariantCulture);
        }

        return double.Parse(value, CultureInfo.InvariantCulture);
    }
}
