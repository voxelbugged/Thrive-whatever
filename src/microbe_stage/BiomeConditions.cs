﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using ThriveScriptsShared;

/// <summary>
///   The conditions of a biome that can change. This is a separate class to make serialization work regarding the biome
/// </summary>
[UseThriveSerializer]
public class BiomeConditions : IBiomeConditions, ICloneable
{
    [JsonProperty]
    private Dictionary<Compound, BiomeCompoundProperties> compounds;

    [JsonProperty]
    private Dictionary<Compound, BiomeCompoundProperties> currentCompoundAmounts;

    [JsonProperty]
    private Dictionary<Compound, BiomeCompoundProperties> averageCompoundAmounts;

    [JsonProperty]
    private Dictionary<Compound, BiomeCompoundProperties> maximumCompoundAmounts;

    [JsonProperty]
    private Dictionary<Compound, BiomeCompoundProperties> minimumCompoundAmounts;

    /// <summary>
    ///   Creates new biome conditions. The other compound amounts are nullable to allow
    ///   <see cref="SimulationParameters"/> to load this class.
    /// </summary>
    /// <param name="compounds">
    ///   The fallback compound amounts if specific compound amount category doesn't have a value
    /// </param>
    /// <param name="currentCompoundAmounts">Value for <see cref="CurrentCompoundAmounts"/></param>
    /// <param name="averageCompoundAmounts">Value for <see cref="AverageCompounds"/></param>
    /// <param name="maximumCompoundAmounts">Value for <see cref="MaximumCompounds"/></param>
    /// <param name="minimumCompoundAmounts">Value for <see cref="MinimumCompounds"/></param>
    [JsonConstructor]
    public BiomeConditions(Dictionary<Compound, BiomeCompoundProperties> compounds,
        Dictionary<Compound, BiomeCompoundProperties>? currentCompoundAmounts,
        Dictionary<Compound, BiomeCompoundProperties>? averageCompoundAmounts,
        Dictionary<Compound, BiomeCompoundProperties>? maximumCompoundAmounts,
        Dictionary<Compound, BiomeCompoundProperties>? minimumCompoundAmounts)
    {
        this.compounds = compounds;

        // Initialize the backing stores and the adapters that allow access. This is important to do just once to
        // save massively on the number of allocated objects.
        this.currentCompoundAmounts = currentCompoundAmounts ?? new Dictionary<Compound, BiomeCompoundProperties>();
        CurrentCompoundAmounts =
            new DictionaryWithFallback<Compound, BiomeCompoundProperties>(this.currentCompoundAmounts, compounds);

        this.averageCompoundAmounts = averageCompoundAmounts ?? new Dictionary<Compound, BiomeCompoundProperties>();
        AverageCompounds =
            new DictionaryWithFallback<Compound, BiomeCompoundProperties>(this.averageCompoundAmounts, compounds);

        this.maximumCompoundAmounts = maximumCompoundAmounts ?? new Dictionary<Compound, BiomeCompoundProperties>();
        MaximumCompounds =
            new DictionaryWithFallback<Compound, BiomeCompoundProperties>(this.maximumCompoundAmounts, compounds);

        this.minimumCompoundAmounts = minimumCompoundAmounts ?? new Dictionary<Compound, BiomeCompoundProperties>();
        MinimumCompounds =
            new DictionaryWithFallback<Compound, BiomeCompoundProperties>(this.minimumCompoundAmounts, compounds);
    }

    public Dictionary<string, ChunkConfiguration> Chunks { get; set; } = null!;

    /// <summary>
    ///   The compound amounts that change in realtime during gameplay
    /// </summary>
    [JsonIgnore]
    public IDictionary<Compound, BiomeCompoundProperties> CurrentCompoundAmounts { get; }

    /// <summary>
    ///   Average compounds over an in-game day
    /// </summary>
    [JsonIgnore]
    public IDictionary<Compound, BiomeCompoundProperties> AverageCompounds { get; }

    /// <summary>
    ///   Maximum compounds this patch can reach. Not related to maximum during an in-game day, for that use
    ///   <see cref="Compounds"/>!
    /// </summary>
    [JsonIgnore]
    public IDictionary<Compound, BiomeCompoundProperties> MaximumCompounds { get; }

    /// <summary>
    ///   Minimum compounds during an in-game day
    /// </summary>
    [JsonIgnore]
    public IDictionary<Compound, BiomeCompoundProperties> MinimumCompounds { get; }

    /// <summary>
    ///   The normal, large timescale compound amounts. The maximum amounts compounds are at during a day in this
    ///   patch (if they wary during a day).
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     I couldn't come up with a better name so this is named unimaginatively like this - hhyyrylainen
    ///   </para>
    /// </remarks>
    [JsonIgnore]
    public IReadOnlyDictionary<Compound, BiomeCompoundProperties> Compounds => compounds;

    /// <summary>
    ///   Allows access to modification of the compound values in the biome permanently. Should only be used by
    ///   auto-evo or map generator. After changing <see cref="AverageCompounds"/> must to be updated.
    ///   <see cref="ModifyLongTermCondition"/> is the preferred method to update this data which handles that
    ///   automatically.
    /// </summary>
    [JsonIgnore]
    public IDictionary<Compound, BiomeCompoundProperties> ChangeableCompounds => compounds;

    /// <summary>
    ///   Returns a new dictionary where <see cref="Compounds"/> is combined with compounds contained in
    ///   <see cref="Chunks"/>.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<Compound, BiomeCompoundProperties> CombinedCompounds
    {
        get
        {
            var result = new Dictionary<Compound, BiomeCompoundProperties>(compounds);

            foreach (var chunk in Chunks.Values)
            {
                if (chunk.Compounds == null)
                    continue;

                foreach (var compound in chunk.Compounds)
                {
                    compounds.TryGetValue(compound.Key, out BiomeCompoundProperties properties);
                    properties.Amount += compound.Value.Amount;
                    result[compound.Key] = properties;
                }
            }

            return result;
        }
    }

    public BiomeCompoundProperties GetCompound(Compound compound, CompoundAmountType amountType)
    {
        if (TryGetCompound(compound, amountType, out var result))
        {
            return result;
        }

        throw new KeyNotFoundException("Compound type not found in BiomeConditions");
    }

    public bool TryGetCompound(Compound compound, CompoundAmountType amountType, out BiomeCompoundProperties result)
    {
        switch (amountType)
        {
            case CompoundAmountType.Current:
                return CurrentCompoundAmounts.TryGetValue(compound, out result);
            case CompoundAmountType.Maximum:
                return MaximumCompounds.TryGetValue(compound, out result);
            case CompoundAmountType.Minimum:
                return MinimumCompounds.TryGetValue(compound, out result);
            case CompoundAmountType.Average:
                return AverageCompounds.TryGetValue(compound, out result);
            case CompoundAmountType.Biome:
                return Compounds.TryGetValue(compound, out result);
            case CompoundAmountType.Template:
                throw new NotSupportedException("BiomeConditions doesn't have access to template");
            default:
                throw new ArgumentOutOfRangeException(nameof(amountType), amountType, null);
        }
    }

    /// <summary>
    ///   Modifies the long-term amount of a compound in this biome. This is preferable to directly modifying
    ///   <see cref="ChangeableCompounds"/>. This is only usable for compounds that don't vary along an in-game day.
    /// </summary>
    /// <param name="compound">The compound to modify</param>
    /// <param name="newValue">New value to set</param>
    public void ModifyLongTermCondition(Compound compound, BiomeCompoundProperties newValue)
    {
        // Ensure negative values can't be calculated and applied accidentally. This is here as conditions are modified
        // from many places so the easiest and safes thing is to just clamp stuff to non-zero here
        if (newValue.Ambient < 0)
            newValue.Ambient = 0;

        if (newValue.Density < 0)
            newValue.Density = 0;

        // As this is the cloud size, this is unlikely to be wrong, but probably better to check here than to cause
        // weird issues in cloud spawning
        if (newValue.Amount < 0)
            newValue.Amount = 0;

        ChangeableCompounds[compound] = newValue;

        // Reset other related values
        AverageCompounds[compound] = newValue;

        // This is only fine to do on compounds that don't vary along a day
        CurrentCompoundAmounts[compound] = newValue;
    }

    public IEnumerable<Compound> GetAmbientCompoundsThatVary()
    {
        const float epsilon = 0.000001f;

        foreach (var minimumCompound in MinimumCompounds)
        {
            if (!Compounds.TryGetValue(minimumCompound.Key, out var maxValue) ||
                Math.Abs(maxValue.Ambient - minimumCompound.Value.Ambient) > epsilon)
            {
                yield return minimumCompound.Key;
            }
        }
    }

    public bool HasCompoundsThatVary()
    {
        const float epsilon = 0.000001f;

        foreach (var minimumCompound in MinimumCompounds)
        {
            if (!Compounds.TryGetValue(minimumCompound.Key, out var maxValue) ||
                Math.Abs(maxValue.Ambient - minimumCompound.Value.Ambient) > epsilon)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsVaryingCompound(Compound compound)
    {
        const float epsilon = 0.000001f;

        bool hasNormally = Compounds.TryGetValue(compound, out var normal);
        bool hasMinimum = MinimumCompounds.TryGetValue(compound, out var minimum);

        // Not varying if the numbers are the same
        if (!hasNormally && !hasMinimum)
            return false;

        if (hasNormally && hasMinimum && Math.Abs(normal.Ambient - minimum.Ambient) < epsilon)
            return false;

        return true;
    }

    public void Check(string name)
    {
        if (compounds == null)
        {
            throw new InvalidRegistryDataException(name, GetType().Name,
                "Compounds missing");
        }

        if (Chunks == null)
        {
            throw new InvalidRegistryDataException(name, GetType().Name,
                "Chunks missing");
        }

        float sumOfGasses = 0;

        foreach (var compound in compounds)
        {
            if (compound.Value.Density * Constants.CLOUD_SPAWN_DENSITY_SCALE_FACTOR is < 0 or > 1)
            {
                throw new InvalidRegistryDataException(name, GetType().Name,
                    $"Density {compound.Value.Density} invalid for {compound.Key} " +
                    $"(scale factor is {Constants.CLOUD_SPAWN_DENSITY_SCALE_FACTOR})");
            }

            if (compound.Value.Ambient > 0 && SimulationParameters.Instance.GetCompoundDefinition(compound.Key).IsGas)
            {
                sumOfGasses += compound.Value.Ambient;
            }
        }

        if (sumOfGasses > 1)
        {
            throw new InvalidRegistryDataException(name, GetType().Name,
                "Gas compounds shouldn't together be over 100%");
        }

        foreach (var chunk in Chunks)
        {
            if (chunk.Value.Density * Constants.CLOUD_SPAWN_DENSITY_SCALE_FACTOR is < 0 or > 1)
            {
                throw new InvalidRegistryDataException(name, GetType().Name,
                    $"Density {chunk.Value.Density} invalid for {chunk.Key} " +
                    $"(scale factor is {Constants.CLOUD_SPAWN_DENSITY_SCALE_FACTOR})");
            }

            if (string.IsNullOrEmpty(chunk.Value.Name))
            {
                throw new InvalidRegistryDataException(name, GetType().Name, "Missing name for chunk type");
            }

            if (chunk.Value.PhysicsDensity <= 0)
            {
                throw new InvalidRegistryDataException(name, GetType().Name, "Missing physics density for chunk type");
            }
        }
    }

    public object Clone()
    {
        // Shallow cloning is enough here thanks to us using value types (structs) as the dictionary values
        var result = new BiomeConditions(compounds.CloneShallow(), currentCompoundAmounts.CloneShallow(),
            averageCompoundAmounts.CloneShallow(), maximumCompoundAmounts.CloneShallow(),
            minimumCompoundAmounts.CloneShallow())
        {
            Chunks = Chunks.CloneShallow(),
        };

        return result;
    }
}
