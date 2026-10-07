using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace MultiplayerHumanoidAlienRacesPatch.Source.Mods;

/// <summary>
///     Cached reflection handles for HAR internals.
///     HAR renames/reshapes these often, so everything is resolved by name at load.
/// </summary>
public partial class HumanoidAlienRaces
{
    private static Type alienCompType;
    private static Type dummyPawnType;
    private static MethodInfo overwriteColorChannelMethod;
    private static FieldInfo originPawnField;
    private static FieldInfo addonVariantsField;
    private static FieldInfo addonColorsField;
    private static FieldInfo colorChannelsField;
    private static FieldInfo colorChannelLinksField;
    private static FieldInfo bodyVariantField;
    private static FieldInfo headVariantField;
    private static FieldInfo headMaskVariantField;
    private static FieldInfo bodyMaskVariantField;
    private static FieldInfo lastAlienMeatIngestedTickField;

    private static Type colorChannelLinkDataType;
    private static Type colorChannelLinkTargetDataType;
    private static FieldInfo linkOriginalChannelField;
    private static FieldInfo linkTargetsOneField;
    private static FieldInfo linkTargetsTwoField;
    private static FieldInfo linkTargetChannelField;
    private static FieldInfo linkTargetCategoryIndexField;

    private static void ResolveReflection()
    {
        alienCompType = AccessTools.TypeByName("AlienRace.AlienPartGenerator+AlienComp");
        if (alienCompType == null) return;

        // Current HAR: OverwriteColorChannel(string, Color?, Color?)
        // Older HAR: OverwriteColorChannel(string, Color, Color) - keep fallback
        overwriteColorChannelMethod = AccessTools.Method(
            alienCompType,
            "OverwriteColorChannel",
            new[] { typeof(string), typeof(Color?), typeof(Color?) });
        overwriteColorChannelMethod ??= AccessTools.Method(
            alienCompType,
            "OverwriteColorChannel",
            new[] { typeof(string), typeof(Color), typeof(Color) });

        originPawnField = AccessTools.Field(
            dummyPawnType = AccessTools.TypeByName("Multiplayer.Client.Patches.StylingDialog_DummyPawn"),
            "origPawn");

        addonVariantsField = AccessTools.Field(alienCompType, "addonVariants");
        addonColorsField = AccessTools.Field(alienCompType, "addonColors");
        colorChannelsField = AccessTools.Field(alienCompType, "colorChannels");
        colorChannelLinksField = AccessTools.Field(alienCompType, "colorChannelLinks");

        bodyVariantField = AccessTools.Field(alienCompType, "bodyVariant");
        headVariantField = AccessTools.Field(alienCompType, "headVariant");
        headMaskVariantField = AccessTools.Field(alienCompType, "headMaskVariant");
        bodyMaskVariantField = AccessTools.Field(alienCompType, "bodyMaskVariant");
        lastAlienMeatIngestedTickField = AccessTools.Field(alienCompType, "lastAlienMeatIngestedTick");

        colorChannelLinkDataType =
            AccessTools.TypeByName("AlienRace.AlienPartGenerator+AlienComp+ColorChannelLinkData");
        colorChannelLinkTargetDataType =
            AccessTools.TypeByName(
                "AlienRace.AlienPartGenerator+AlienComp+ColorChannelLinkData+ColorChannelLinkTargetData");

        if (colorChannelLinkDataType != null)
        {
            linkOriginalChannelField = AccessTools.Field(colorChannelLinkDataType, "originalChannel");
            linkTargetsOneField = AccessTools.Field(colorChannelLinkDataType, "targetsChannelOne");
            linkTargetsTwoField = AccessTools.Field(colorChannelLinkDataType, "targetsChannelTwo");
        }

        if (colorChannelLinkTargetDataType != null)
        {
            linkTargetChannelField = AccessTools.Field(colorChannelLinkTargetDataType, "targetChannel");
            linkTargetCategoryIndexField = AccessTools.Field(colorChannelLinkTargetDataType, "categoryIndex");
        }

        if (addonVariantsField == null) Log.Error($"{LogPrefix} Missing field AlienComp.addonVariants");

        if (addonColorsField == null) Log.Error($"{LogPrefix} Missing field AlienComp.addonColors");

        if (colorChannelsField == null) Log.Error($"{LogPrefix} Missing field AlienComp.colorChannels");

        if (colorChannelLinksField == null) Log.Error($"{LogPrefix} Missing field AlienComp.colorChannelLinks");
    }

    /// <summary>
    ///     True only for MP's styling dummy pawn. Never touches <c>origPawn</c> on
    ///     real pawns: <c>FieldInfo.GetValue</c> throws when the target object isn't
    ///     a <c>StylingDialog_DummyPawn</c> (e.g. Ratkin or single-player pawns).
    /// </summary>
    private static bool IsStylingDummy(Pawn pawn)
    {
        if (pawn == null || dummyPawnType == null || originPawnField == null) return false;

        try
        {
            return dummyPawnType.IsInstanceOfType(pawn);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Dummy type check failed: {exception}");
            return false;
        }
    }

    private static bool TryGetOriginPawn(Pawn pawn, out Pawn originPawn)
    {
        originPawn = null;
        if (!IsStylingDummy(pawn)) return false;

        try
        {
            if (originPawnField.GetValue(pawn) is Pawn origin)
            {
                originPawn = origin;
                return origin != null;
            }

            return false;
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Origin pawn read failed: {exception}");
            return false;
        }
    }
}