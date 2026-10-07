using UnityEngine;

namespace MultiplayerHumanoidAlienRacesPatch.Source.Mods;

/// <summary>
///     Network-serializable snapshot of AlienComp appearance state.
///     Mirrors AlienComp fields edited by the styling station plus variant/tick
///     fields copied by AlienComp.CopyAlienData.
/// </summary>
public class AlienCompData
{
    public List<(Color? first, Color? second)> addonColors = new();
    public List<int> addonVariants = new();
    public int bodyMaskVariant = -1;

    public int bodyVariant = -1;
    public Dictionary<string, LinkData> colorChannelLinks = new();
    public Dictionary<string, (Color, Color)> colorChannels = new();
    public int headMaskVariant = -1;
    public int headVariant = -1;
    public int lastAlienMeatIngestedTick;

    public class LinkData
    {
        public List<(string channel, int categoryIndex)> targetsOne = new();
        public List<(string channel, int categoryIndex)> targetsTwo = new();
    }
}