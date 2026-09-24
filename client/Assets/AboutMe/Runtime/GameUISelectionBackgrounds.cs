using UnityEngine;

namespace AboutMe
{
    public partial class GameUI
    {
        const string CampusSelectionArt="StartMenu2D/background";
        const string StudioSelectionArt="SelectionBackgrounds/studio-wardrobe";

        // Page cards share the full-screen canvas art, so safe-area scaling
        // cannot leave seams between the card stage and the surrounding image.
        RectTransform SelectionStage(string name)
        {
            return ArtPanel(artboard,name,0,0,1600,900,Color.clear,false,false);
        }
    }
}
