using System.Collections;
using UnityEngine;
using Verse;

namespace MultiplayerHumanoidAlienRacesPatch.Source.Mods;

/// <summary>
///     Comp-to-comp and comp-to-data copying via reflection.
///     Used to seed the MP dummy pawn without triggering lazy random generation,
///     and to snapshot the dummy for network sync.
/// </summary>
public partial class HumanoidAlienRaces
{
    private static void DirectCopyAlienComp(object source, object destination)
    {
        try
        {
            // Set colorChannels field directly to bypass lazy-init random generation
            var sourceChannels = colorChannelsField?.GetValue(source) as IDictionary;
            if (sourceChannels != null && colorChannelsField != null)
            {
                var destinationChannels = (IDictionary)Activator.CreateInstance(colorChannelsField.FieldType);
                foreach (DictionaryEntry entry in sourceChannels)
                {
                    var tuple = entry.Value;
                    if (tuple == null) continue;

                    var tupleType = tuple.GetType();
                    var firstField = tupleType.GetField("first");
                    var secondField = tupleType.GetField("second");
                    if (firstField == null || secondField == null) continue;

                    var firstColor = (Color)firstField.GetValue(tuple);
                    var secondColor = (Color)secondField.GetValue(tuple);
                    destinationChannels[entry.Key] = Activator.CreateInstance(tupleType, firstColor, secondColor);
                }

                colorChannelsField.SetValue(destination, destinationChannels);
            }

            // Copy addonVariants
            if (addonVariantsField != null)
            {
                var variants = addonVariantsField.GetValue(source) as IEnumerable<int>;
                addonVariantsField.SetValue(destination, variants?.ToList() ?? new List<int>());
            }

            // Copy addonColors
            var sourceColors = addonColorsField?.GetValue(source) as IEnumerable;
            if (sourceColors != null && addonColorsField != null)
            {
                var destinationColors = (IList)Activator.CreateInstance(addonColorsField.FieldType);
                foreach (var item in sourceColors)
                {
                    if (item == null) continue;

                    var itemType = item.GetType();
                    var firstField = itemType.GetField("first");
                    var secondField = itemType.GetField("second");
                    if (firstField == null || secondField == null) continue;

                    var firstValue = (Color?)firstField.GetValue(item);
                    var secondValue = (Color?)secondField.GetValue(item);
                    destinationColors.Add(CreateNullableColorTuple(firstValue, secondValue));
                }

                addonColorsField.SetValue(destination, destinationColors);
            }

            // Copy colorChannelLinks (missing in old DirectCopy, needed for dummy preview)
            var sourceLinks = colorChannelLinksField?.GetValue(source) as IDictionary;
            if (sourceLinks != null && colorChannelLinksField != null)
            {
                var destinationLinks = (IDictionary)Activator.CreateInstance(colorChannelLinksField.FieldType);
                foreach (DictionaryEntry entry in sourceLinks)
                {
                    var clonedLink = CloneLinkData(entry.Value);
                    if (clonedLink != null && entry.Key is string key) destinationLinks[key] = clonedLink;
                }

                colorChannelLinksField.SetValue(destination, destinationLinks);
            }

            CopyVariantFields(source, destination);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} DirectCopyAlienComp failed: {exception}");
        }
    }

    private static void CopyVariantFields(object source, object destination)
    {
        try
        {
            if (bodyVariantField != null) bodyVariantField.SetValue(destination, bodyVariantField.GetValue(source));

            if (headVariantField != null) headVariantField.SetValue(destination, headVariantField.GetValue(source));

            if (headMaskVariantField != null)
                headMaskVariantField.SetValue(destination, headMaskVariantField.GetValue(source));

            if (bodyMaskVariantField != null)
                bodyMaskVariantField.SetValue(destination, bodyMaskVariantField.GetValue(source));

            if (lastAlienMeatIngestedTickField != null)
                lastAlienMeatIngestedTickField.SetValue(destination, lastAlienMeatIngestedTickField.GetValue(source));
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} CopyVariantFields failed: {exception}");
        }
    }

    private static object CloneLinkData(object sourceLink)
    {
        if (sourceLink == null || colorChannelLinkDataType == null) return null;

        var clone = Activator.CreateInstance(colorChannelLinkDataType);
        linkOriginalChannelField?.SetValue(clone, linkOriginalChannelField?.GetValue(sourceLink));

        var sourceOne = linkTargetsOneField?.GetValue(sourceLink) as IEnumerable;
        var sourceTwo = linkTargetsTwoField?.GetValue(sourceLink) as IEnumerable;
        var destinationOne = linkTargetsOneField?.GetValue(clone) as ICollection;
        var destinationTwo = linkTargetsTwoField?.GetValue(clone) as ICollection;

        if (sourceOne != null && destinationOne != null)
            foreach (var target in sourceOne)
            {
                var channel = (string)linkTargetChannelField?.GetValue(target);
                var categoryIndex = (int)(linkTargetCategoryIndexField?.GetValue(target) ?? 0);
                AddToCollection(destinationOne, channel, categoryIndex);
            }

        if (sourceTwo != null && destinationTwo != null)
            foreach (var target in sourceTwo)
            {
                var channel = (string)linkTargetChannelField?.GetValue(target);
                var categoryIndex = (int)(linkTargetCategoryIndexField?.GetValue(target) ?? 0);
                AddToCollection(destinationTwo, channel, categoryIndex);
            }

        return clone;
    }

    private static AlienCompData SnapshotAlienComp(object comp)
    {
        var data = new AlienCompData();

        try
        {
            var channels = colorChannelsField?.GetValue(comp) as IDictionary;
            if (channels != null)
                foreach (DictionaryEntry entry in channels)
                {
                    var tuple = entry.Value;
                    if (tuple == null || entry.Key is not string key) continue;

                    var tupleType = tuple.GetType();
                    var firstColor = (Color)tupleType.GetField("first")?.GetValue(tuple);
                    var secondColor = (Color)tupleType.GetField("second")?.GetValue(tuple);
                    data.colorChannels[key] = (firstColor, secondColor);
                }

            data.addonVariants = (addonVariantsField?.GetValue(comp) as IEnumerable<int>)?.ToList() ?? new List<int>();

            var addonColors = addonColorsField?.GetValue(comp) as IEnumerable;
            if (addonColors != null)
                foreach (var item in addonColors)
                {
                    if (item == null) continue;

                    var itemType = item.GetType();
                    var firstValue = (Color?)itemType.GetField("first")?.GetValue(item);
                    var secondValue = (Color?)itemType.GetField("second")?.GetValue(item);
                    data.addonColors.Add((firstValue, secondValue));
                }

            var links = colorChannelLinksField?.GetValue(comp) as IDictionary;
            if (links != null)
                foreach (DictionaryEntry entry in links)
                {
                    if (entry.Key is not string key || entry.Value == null) continue;

                    var linkData = new AlienCompData.LinkData();
                    var targetsOne = linkTargetsOneField?.GetValue(entry.Value) as IEnumerable;
                    var targetsTwo = linkTargetsTwoField?.GetValue(entry.Value) as IEnumerable;

                    if (targetsOne != null)
                        foreach (var target in targetsOne)
                            linkData.targetsOne.Add((
                                (string)linkTargetChannelField?.GetValue(target),
                                (int)(linkTargetCategoryIndexField?.GetValue(target) ?? 0)));

                    if (targetsTwo != null)
                        foreach (var target in targetsTwo)
                            linkData.targetsTwo.Add((
                                (string)linkTargetChannelField?.GetValue(target),
                                (int)(linkTargetCategoryIndexField?.GetValue(target) ?? 0)));

                    data.colorChannelLinks[key] = linkData;
                }

            data.bodyVariant = (int)(bodyVariantField?.GetValue(comp) ?? -1);
            data.headVariant = (int)(headVariantField?.GetValue(comp) ?? -1);
            data.headMaskVariant = (int)(headMaskVariantField?.GetValue(comp) ?? -1);
            data.bodyMaskVariant = (int)(bodyMaskVariantField?.GetValue(comp) ?? -1);
            data.lastAlienMeatIngestedTick = (int)(lastAlienMeatIngestedTickField?.GetValue(comp) ?? 0);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} SnapshotAlienComp failed: {exception}");
        }

        return data;
    }

    private static void AddToCollection(ICollection collection, string channel, int categoryIndex)
    {
        var item = Activator.CreateInstance(colorChannelLinkTargetDataType);
        linkTargetChannelField?.SetValue(item, channel);
        linkTargetCategoryIndexField?.SetValue(item, categoryIndex);
        collection.GetType().GetMethod("Add")?.Invoke(collection, new[] { item });
    }

    private static object CreateNullableColorTuple(Color? first, Color? second)
    {
        var tupleType = addonColorsField.FieldType.GetGenericArguments()[0];
        return Activator.CreateInstance(tupleType, first, second);
    }
}