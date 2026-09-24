using UnityEngine;
using UnityEngine.UI;

namespace AboutMe
{
    public partial class GameUI
    {
        const string CampusSelectionArt="StartMenu2D/background";
        const string StudioSelectionArt="SelectionBackgrounds/studio-wardrobe";

        // Page-local art sits below cards and is fully opaque. The integration
        // canvas owns the safe-area bleed outside the 1600 x 900 artboard.
        void AddSelectionBackground(RectTransform page,string resource,Color wash)
        {
            var art=MenuSprite(page,"Static selection art",resource,0,0,1600,900,false);
            art.SetAsFirstSibling();
            var veil=ArtPanel(page,"Quiet paper wash",0,0,1600,900,wash,false,false);
            veil.GetComponent<Image>().raycastTarget=false;
            veil.SetSiblingIndex(1);
        }

        RectTransform SelectionStage(string name,string resource,Color wash)
        {
            var stage=ArtPanel(artboard,name,0,0,1600,900,Color.clear,false,false);
            AddSelectionBackground(stage,resource,wash);
            return stage;
        }
    }
}
