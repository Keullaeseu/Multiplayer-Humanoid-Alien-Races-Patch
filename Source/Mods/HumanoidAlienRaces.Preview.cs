using System.Collections;
using RimWorld;
using UnityEngine;
using Verse;

namespace MultiplayerHumanoidAlienRacesPatch.Source.Mods;

/// <summary>
///     Live preview fix for the MP dummy pawn.
///     Vanilla edits hair style on the dummy story and hair color on the dialog's
///     desiredHairColor field. HAR renders hair/body-addons through AlienComp
///     (addonVariants/addonColors/colorChannels), and its variant clicks don't dirty
///     the renderer, so the dummy preview goes stale until Accept (which is why
///     Accept looked correct while the preview did not). Mirror the dialog color
///     into the dummy comp locally and dirty the preview when style/color/addons
///     change. Dummy-only: single-player dialogs are untouched.
/// </summary>
public partial class HumanoidAlienRaces
{
    private static Dialog_StylingStation lastPreviewDialog;
    private static HairDef lastPreviewHairDef;
    private static BeardDef lastPreviewBeardDef;
    private static TattooDef lastPreviewFaceTattoo;
    private static TattooDef lastPreviewBodyTattoo;
    private static Color lastPreviewDesiredHairColor;
    private static Color lastPreviewHairChannelFirst;
    private static List<int> lastPreviewAddonVariants;
    private static List<(Color? first, Color? second)> lastPreviewAddonColors;
    private static Dictionary<string, (Color first, Color second)> lastPreviewColorChannels;
    private static bool hasLastPreviewState;

    private static void StylingPreviewPostfix(Dialog_StylingStation __instance)
    {
        try
        {
            if (__instance?.pawn == null || alienCompType == null) return;

            // Only MP dummies. Real pawns (single-player, Ratkin) already preview correctly.
            if (!IsStylingDummy(__instance.pawn)) return;

            var comp = __instance.pawn.AllComps?.FirstOrDefault(candidate => candidate.GetType() == alienCompType);
            if (comp == null) return;

            var currentHairDef = __instance.pawn.story?.hairDef;
            var currentBeardDef = __instance.pawn.style?.beardDef;
            var currentFaceTattoo = __instance.pawn.style?.FaceTattoo;
            var currentBodyTattoo = __instance.pawn.style?.BodyTattoo;
            var currentDesiredHairColor = __instance.desiredHairColor;
            var currentHairChannelFirst = ReadHairChannelFirst(comp);
            var currentAddonVariants = ReadAddonVariants(comp);
            var currentAddonColors = ReadAddonColors(comp);
            var currentColorChannels = ReadColorChannels(comp);

            if (!hasLastPreviewState || !ReferenceEquals(lastPreviewDialog, __instance))
            {
                lastPreviewDialog = __instance;
                lastPreviewHairDef = currentHairDef;
                lastPreviewBeardDef = currentBeardDef;
                lastPreviewFaceTattoo = currentFaceTattoo;
                lastPreviewBodyTattoo = currentBodyTattoo;
                lastPreviewDesiredHairColor = currentDesiredHairColor;
                lastPreviewHairChannelFirst = currentHairChannelFirst;
                lastPreviewAddonVariants = currentAddonVariants;
                lastPreviewAddonColors = currentAddonColors;
                lastPreviewColorChannels = currentColorChannels;
                hasLastPreviewState = true;
                return;
            }

            var styleChanged = !ReferenceEquals(lastPreviewHairDef, currentHairDef)
                               || !ReferenceEquals(lastPreviewBeardDef, currentBeardDef)
                               || !ReferenceEquals(lastPreviewFaceTattoo, currentFaceTattoo)
                               || !ReferenceEquals(lastPreviewBodyTattoo, currentBodyTattoo);
            var colorChanged = lastPreviewDesiredHairColor != currentDesiredHairColor;
            var channelChanged = lastPreviewHairChannelFirst != currentHairChannelFirst
                                 || !ColorChannelsEqual(lastPreviewColorChannels, currentColorChannels);
            var addonsChanged = !AddonVariantsEqual(lastPreviewAddonVariants, currentAddonVariants)
                                || !AddonColorsEqual(lastPreviewAddonColors, currentAddonColors);

            if (colorChanged)
                try
                {
                    // Preview-only: keep the dummy's hair channel in step with the
                    // vanilla color picker. Second stays untouched (null keeps).
                    overwriteColorChannelMethod.Invoke(comp,
                        new object[] { "hair", (Color?)currentDesiredHairColor, null });
                    currentHairChannelFirst = ReadHairChannelFirst(comp);
                    currentColorChannels = ReadColorChannels(comp);
                    channelChanged = lastPreviewHairChannelFirst != currentHairChannelFirst
                                     || !ColorChannelsEqual(lastPreviewColorChannels, currentColorChannels);
                }
                catch (Exception exception)
                {
                    Log.Error($"{LogPrefix} Preview hair color mirror failed: {exception}");
                }

            if (styleChanged || colorChanged || channelChanged || addonsChanged)
            {
                try
                {
                    __instance.pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                }
                catch (Exception exception)
                {
                    Log.Error($"{LogPrefix} Preview dirty graphics failed: {exception}");
                }

                try
                {
                    PortraitsCache.SetDirty(__instance.pawn);
                }
                catch (Exception exception)
                {
                    Log.Error($"{LogPrefix} Preview portrait dirty failed: {exception}");
                }
            }

            lastPreviewHairDef = currentHairDef;
            lastPreviewBeardDef = currentBeardDef;
            lastPreviewFaceTattoo = currentFaceTattoo;
            lastPreviewBodyTattoo = currentBodyTattoo;
            lastPreviewDesiredHairColor = currentDesiredHairColor;
            lastPreviewHairChannelFirst = currentHairChannelFirst;
            lastPreviewAddonVariants = currentAddonVariants;
            lastPreviewAddonColors = currentAddonColors;
            lastPreviewColorChannels = currentColorChannels;
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} StylingPreviewPostfix failed: {exception}");
        }
    }

    private static Color ReadHairChannelFirst(object comp)
    {
        try
        {
            var channels = colorChannelsField?.GetValue(comp) as IDictionary;
            var tuple = channels?["hair"];
            if (tuple == null) return Color.clear;

            return (Color)tuple.GetType().GetField("first")?.GetValue(tuple);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Preview hair channel read failed: {exception}");
            return Color.clear;
        }
    }

    private static List<int> ReadAddonVariants(object comp)
    {
        try
        {
            return (addonVariantsField?.GetValue(comp) as IEnumerable<int>)?.ToList() ?? new List<int>();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Preview addon variants read failed: {exception}");
            return new List<int>();
        }
    }

    private static List<(Color? first, Color? second)> ReadAddonColors(object comp)
    {
        var result = new List<(Color? first, Color? second)>();
        try
        {
            var colors = addonColorsField?.GetValue(comp) as IEnumerable;
            if (colors == null) return result;

            foreach (var item in colors)
            {
                if (item == null) continue;

                var itemType = item.GetType();
                result.Add((
                    (Color?)itemType.GetField("first")?.GetValue(item),
                    (Color?)itemType.GetField("second")?.GetValue(item)));
            }
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Preview addon colors read failed: {exception}");
        }

        return result;
    }

    private static Dictionary<string, (Color first, Color second)> ReadColorChannels(object comp)
    {
        var result = new Dictionary<string, (Color first, Color second)>();
        try
        {
            var channels = colorChannelsField?.GetValue(comp) as IDictionary;
            if (channels == null) return result;

            foreach (DictionaryEntry entry in channels)
            {
                if (entry.Key is not string key || entry.Value == null) continue;

                var tupleType = entry.Value.GetType();
                result[key] = (
                    (Color)tupleType.GetField("first")?.GetValue(entry.Value),
                    (Color)tupleType.GetField("second")?.GetValue(entry.Value));
            }
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Preview color channels read failed: {exception}");
        }

        return result;
    }

    private static bool AddonVariantsEqual(List<int> left, List<int> right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null || left.Count != right.Count) return false;

        for (var index = 0; index < left.Count; index++)
            if (left[index] != right[index])
                return false;

        return true;
    }

    private static bool AddonColorsEqual(List<(Color? first, Color? second)> left,
        List<(Color? first, Color? second)> right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null || left.Count != right.Count) return false;

        for (var index = 0; index < left.Count; index++)
            if (left[index].first != right[index].first || left[index].second != right[index].second)
                return false;

        return true;
    }

    private static bool ColorChannelsEqual(Dictionary<string, (Color first, Color second)> left,
        Dictionary<string, (Color first, Color second)> right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null || left.Count != right.Count) return false;

        foreach (var entry in left)
        {
            if (!right.TryGetValue(entry.Key, out var other)) return false;
            if (entry.Value.first != other.first || entry.Value.second != other.second) return false;
        }

        return true;
    }

    private static void ResetPreviewState()
    {
        lastPreviewDialog = null;
        lastPreviewAddonVariants = null;
        lastPreviewAddonColors = null;
        lastPreviewColorChannels = null;
        hasLastPreviewState = false;
    }
}