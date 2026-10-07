using Multiplayer.API;
using UnityEngine;
using Verse;

namespace MultiplayerHumanoidAlienRacesPatch.Source.Mods;

/// <summary>
///     Network serialization for <see cref="AlienCompData" />.
///     Field order must match between write and read.
/// </summary>
public partial class HumanoidAlienRaces
{
    private static void SyncAlienCompData(SyncWorker sync, ref AlienCompData data)
    {
        try
        {
            if (sync.isWriting)
            {
                // colorChannels
                sync.Write(data.colorChannels.Count);
                foreach (var channelEntry in data.colorChannels)
                {
                    sync.Write(channelEntry.Key);
                    sync.Write(channelEntry.Value.Item1);
                    sync.Write(channelEntry.Value.Item2);
                }

                // addonVariants
                sync.Write(data.addonVariants.Count);
                foreach (var variant in data.addonVariants) sync.Write(variant);

                // addonColors
                sync.Write(data.addonColors.Count);
                foreach (var (firstValue, secondValue) in data.addonColors)
                {
                    sync.Write(firstValue.HasValue);
                    if (firstValue.HasValue) sync.Write(firstValue.Value);

                    sync.Write(secondValue.HasValue);
                    if (secondValue.HasValue) sync.Write(secondValue.Value);
                }

                // colorChannelLinks
                sync.Write(data.colorChannelLinks.Count);
                foreach (var linkEntry in data.colorChannelLinks)
                {
                    sync.Write(linkEntry.Key);
                    WriteTargets(sync, linkEntry.Value.targetsOne);
                    WriteTargets(sync, linkEntry.Value.targetsTwo);
                }

                // variants + tick
                sync.Write(data.bodyVariant);
                sync.Write(data.headVariant);
                sync.Write(data.headMaskVariant);
                sync.Write(data.bodyMaskVariant);
                sync.Write(data.lastAlienMeatIngestedTick);
            }
            else
            {
                data = new AlienCompData();

                // colorChannels
                var count = sync.Read<int>();
                for (var index = 0; index < count; index++)
                {
                    var key = sync.Read<string>();
                    var firstColor = sync.Read<Color>();
                    var secondColor = sync.Read<Color>();
                    data.colorChannels[key] = (firstColor, secondColor);
                }

                // addonVariants
                count = sync.Read<int>();
                for (var index = 0; index < count; index++) data.addonVariants.Add(sync.Read<int>());

                // addonColors
                count = sync.Read<int>();
                for (var index = 0; index < count; index++)
                {
                    var firstValue = sync.Read<bool>() ? sync.Read<Color>() : (Color?)null;
                    var secondValue = sync.Read<bool>() ? sync.Read<Color>() : (Color?)null;
                    data.addonColors.Add((firstValue, secondValue));
                }

                // colorChannelLinks
                count = sync.Read<int>();
                for (var index = 0; index < count; index++)
                {
                    var key = sync.Read<string>();
                    var linkData = new AlienCompData.LinkData
                    {
                        targetsOne = ReadTargets(sync),
                        targetsTwo = ReadTargets(sync)
                    };
                    data.colorChannelLinks[key] = linkData;
                }

                // variants + tick (older saves/packets may not have them, keep defaults on failure)
                try
                {
                    data.bodyVariant = sync.Read<int>();
                    data.headVariant = sync.Read<int>();
                    data.headMaskVariant = sync.Read<int>();
                    data.bodyMaskVariant = sync.Read<int>();
                    data.lastAlienMeatIngestedTick = sync.Read<int>();
                }
                catch (Exception exception)
                {
                    Log.Warning($"{LogPrefix} SyncAlienCompData missing variant fields, using defaults: {exception}");
                    data.bodyVariant = -1;
                    data.headVariant = -1;
                    data.headMaskVariant = -1;
                    data.bodyMaskVariant = -1;
                    data.lastAlienMeatIngestedTick = 0;
                }
            }
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} SyncAlienCompData failed: {exception}");
            if (!sync.isWriting) data = new AlienCompData();
        }
    }

    private static void WriteTargets(SyncWorker sync, List<(string channel, int categoryIndex)> targets)
    {
        sync.Write(targets.Count);
        foreach (var (channel, categoryIndex) in targets)
        {
            sync.Write(channel);
            sync.Write(categoryIndex);
        }
    }

    private static List<(string channel, int categoryIndex)> ReadTargets(SyncWorker sync)
    {
        var list = new List<(string, int)>();
        var count = sync.Read<int>();
        for (var index = 0; index < count; index++) list.Add((sync.Read<string>(), sync.Read<int>()));

        return list;
    }
}