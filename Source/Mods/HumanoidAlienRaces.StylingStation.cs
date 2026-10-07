using System.Collections;
using RimWorld;
using UnityEngine;
using Verse;

namespace MultiplayerHumanoidAlienRacesPatch.Source.Mods;

/// <summary>
///     Styling station hooks: seed the MP dummy pawn and sync the accepted result.
///     MP edits a dummy pawn locally; we copy the real appearance onto it at open
///     and broadcast the dummy result to everyone at accept.
/// </summary>
public partial class HumanoidAlienRaces
{
    private static void StylingStationCtorPrefix(Pawn pawn)
    {
        try
        {
            if (alienCompType == null || pawn == null) return;

            // Dummy-only: never re-init comps on real pawns (Ratkin/SP).
            if (!IsStylingDummy(pawn)) return;

            if (pawn.AllComps != null && pawn.AllComps.Any(comp => comp.GetType() == alienCompType)) return;

            try
            {
                pawn.InitializeComps();
            }
            catch (Exception exception)
            {
                Log.Error($"{LogPrefix} InitializeComps failed for dummy pawn: {exception}");
                return;
            }

            var dummyComp = pawn.AllComps?.FirstOrDefault(comp => comp.GetType() == alienCompType);
            if (dummyComp == null) return;

            if (!TryGetOriginPawn(pawn, out var originPawn)) return;

            var origComp = originPawn.AllComps?.FirstOrDefault(comp => comp.GetType() == alienCompType);
            if (origComp == null) return;

            // Directly copy fields — CopyAlienData returns early if renderTree unresolved on dummy pawn
            DirectCopyAlienComp(origComp, dummyComp);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} StylingStationCtorPrefix failed: {exception}");
        }
    }

    private static void OnAcceptPrefix()
    {
        try
        {
            pendingAlienSync = null;

            var dialog = Find.WindowStack?.WindowOfType<Dialog_StylingStation>();
            if (dialog?.pawn == null) return;

            var dummyComp = dialog.pawn.AllComps?.FirstOrDefault(comp => comp.GetType() == alienCompType);
            if (dummyComp == null) return;

            if (!TryGetOriginPawn(dialog.pawn, out var originPawn)) return;

            pendingAlienSync = (originPawn, SnapshotAlienComp(dummyComp));
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} OnAcceptPrefix failed: {exception}");
            pendingAlienSync = null;
        }
    }

    private static void OnAcceptPostfix()
    {
        try
        {
            if (!pendingAlienSync.HasValue) return;

            var (originPawn, data) = pendingAlienSync.Value;
            pendingAlienSync = null;
            ResetPreviewState();

            if (originPawn == null || data == null) return;

            SyncAlienData(originPawn, data);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} OnAcceptPostfix failed: {exception}");
            pendingAlienSync = null;
            ResetPreviewState();
        }
    }

    private static void SyncAlienData(Pawn pawn, AlienCompData data)
    {
        try
        {
            ApplyAlienCompData(pawn, data);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} SyncAlienData failed: {exception}");
        }
    }

    private static void ApplyAlienCompData(Pawn pawn, AlienCompData data)
    {
        if (pawn == null || data == null) return;

        var comp = pawn.AllComps?.FirstOrDefault(candidate => candidate.GetType() == alienCompType);
        if (comp == null) return;

        foreach (var channelEntry in data.colorChannels)
            try
            {
                // Works for both (string, Color?, Color?) and legacy (string, Color, Color)
                overwriteColorChannelMethod.Invoke(comp,
                    new object[]
                        { channelEntry.Key, (Color?)channelEntry.Value.Item1, (Color?)channelEntry.Value.Item2 });
            }
            catch (Exception exception)
            {
                Log.Error($"{LogPrefix} OverwriteColorChannel failed for {channelEntry.Key}: {exception}");
            }

        addonVariantsField?.SetValue(comp, data.addonVariants?.ToList() ?? new List<int>());

        if (addonColorsField != null)
        {
            var addonColorsList = (IList)Activator.CreateInstance(addonColorsField.FieldType);
            foreach (var (firstValue, secondValue) in data.addonColors)
                addonColorsList.Add(CreateNullableColorTuple(firstValue, secondValue));

            addonColorsField.SetValue(comp, addonColorsList);
        }

        if (colorChannelLinksField != null)
        {
            var links = (IDictionary)Activator.CreateInstance(colorChannelLinksField.FieldType);
            foreach (var linkEntry in data.colorChannelLinks)
            {
                var linkData = Activator.CreateInstance(colorChannelLinkDataType);
                linkOriginalChannelField?.SetValue(linkData, linkEntry.Key);

                var targetsOne = linkTargetsOneField?.GetValue(linkData) as ICollection;
                var targetsTwo = linkTargetsTwoField?.GetValue(linkData) as ICollection;

                if (targetsOne != null)
                    foreach (var (channel, categoryIndex) in linkEntry.Value.targetsOne)
                        AddToCollection(targetsOne, channel, categoryIndex);

                if (targetsTwo != null)
                    foreach (var (channel, categoryIndex) in linkEntry.Value.targetsTwo)
                        AddToCollection(targetsTwo, channel, categoryIndex);

                links[linkEntry.Key] = linkData;
            }

            colorChannelLinksField.SetValue(comp, links);
        }

        if (bodyVariantField != null) bodyVariantField.SetValue(comp, data.bodyVariant);

        if (headVariantField != null) headVariantField.SetValue(comp, data.headVariant);

        if (headMaskVariantField != null) headMaskVariantField.SetValue(comp, data.headMaskVariant);

        if (bodyMaskVariantField != null) bodyMaskVariantField.SetValue(comp, data.bodyMaskVariant);

        if (lastAlienMeatIngestedTickField != null)
            lastAlienMeatIngestedTickField.SetValue(comp, data.lastAlienMeatIngestedTick);

        pawn.Drawer?.renderer?.SetAllGraphicsDirty();
    }
}