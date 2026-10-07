using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using RimWorld;
using Verse;

namespace MultiplayerHumanoidAlienRacesPatch.Source.Mods;

/// <summary>
///     Multiplayer Patch for Humanoid Alien Races by erdelf,
///     Last Update: 7 Sep @ 6:07pm 2026
///     <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=839005762" />
///     <see href="https://github.com/erdelf/AlienRaces" />
///     Ported from Multiplayer-Compatibility Source/Mods/AlienRace.cs and updated
///     for current AlienRaces (OverwriteColorChannel with nullable colors,
///     HashSet link targets, private colorChannels/colorChannelLinks fields).
///     Bootstrap part: entry point and patch registration. See other partials for
///     reflection, styling dialog hooks, copying and network serialization.
/// </summary>
[MpCompatFor("erdelf.HumanoidAlienRaces")]
public partial class HumanoidAlienRaces
{
    internal const string LogPrefix = "[Multiplayer Humanoid Alien Races Patch]";

    // Captured before dialog closes
    private static (Pawn origPawn, AlienCompData data)? pendingAlienSync;

    public HumanoidAlienRaces(ModContentPack content)
    {
        LongEventHandler.ExecuteWhenFinished(LatePatch);
    }

    private static void LatePatch()
    {
        Log.Message($"{LogPrefix} Initializing...");

        try
        {
            ResolveReflection();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Failed to resolve reflection: {exception}");
            return;
        }

        if (alienCompType == null)
        {
            Log.Error($"{LogPrefix} Could not find AlienComp type, patch disabled.");
            return;
        }

        if (overwriteColorChannelMethod == null)
        {
            Log.Error($"{LogPrefix} Could not find OverwriteColorChannel method, patch disabled.");
            return;
        }

        try
        {
            var stylingCtor =
                AccessTools.Constructor(typeof(Dialog_StylingStation), new[] { typeof(Pawn), typeof(Thing) });
            if (stylingCtor == null)
                Log.Error($"{LogPrefix} Could not find Dialog_StylingStation(Pawn, Thing) constructor.");
            else
                MpCompat.harmony.Patch(
                    stylingCtor,
                    new HarmonyMethod(typeof(HumanoidAlienRaces), nameof(StylingStationCtorPrefix)));

            if (dummyPawnType == null)
                Log.Error($"{LogPrefix} Could not find StylingDialog_DummyPawn type, dummy copy disabled.");

            var onAccept =
                AccessTools.Method(AccessTools.TypeByName("Multiplayer.Client.Patches.StylingDialog_HandleAccept"),
                    "OnAccept");
            if (onAccept == null)
                Log.Error($"{LogPrefix} Could not find StylingDialog_HandleAccept.OnAccept, accept sync disabled.");
            else
                MpCompat.harmony.Patch(
                    onAccept,
                    new HarmonyMethod(typeof(HumanoidAlienRaces), nameof(OnAcceptPrefix)),
                    new HarmonyMethod(typeof(HumanoidAlienRaces), nameof(OnAcceptPostfix)));

            var stylingContents = AccessTools.Method(typeof(Dialog_StylingStation),
                nameof(Dialog_StylingStation.DoWindowContents));
            if (stylingContents == null)
                Log.Error($"{LogPrefix} Could not find Dialog_StylingStation.DoWindowContents, preview fix disabled.");
            else
                MpCompat.harmony.Patch(
                    stylingContents,
                    postfix: new HarmonyMethod(typeof(HumanoidAlienRaces), nameof(StylingPreviewPostfix)));

            MP.RegisterSyncWorker<AlienCompData>(SyncAlienCompData);
            MP.RegisterSyncMethod(typeof(HumanoidAlienRaces), nameof(SyncAlienData));
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Failed to apply patches: {exception}");
            return;
        }

        Log.Message($"{LogPrefix} Initialized.");
    }
}