using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace AboutMe
{
    public partial class GameUI
    {
        Camera savedCharacterCamera;ToyActor savedCharacterActor;RenderTexture savedCharacterTexture;GameObject savedCharacterLight;
        RectTransform savedCharacterCard,savedCharacterDetails;Text savedCharacterLabel,savedPersonaLabel;string savedCharacterSignature;
        Button ArtRaisedButton(Transform parent,string label,float x,float y,float width,float height,Action action,Color color,Color ink)
        {
            var basePlate=ArtPanel(parent,"Button depth · "+label,x,y+6,width,height,Color.Lerp(color,UITheme.Ink,.28f),true,true);
            basePlate.GetComponent<Image>().raycastTarget=false;var shadow=basePlate.GetComponent<Shadow>();shadow.effectDistance=new Vector2(4,-5);shadow.effectColor=new Color(.08f,.10f,.15f,.17f);
            return ArtButton(parent,label,x,y,width,height,action,color,ink);
        }
        InputField ArtScrollableField(Transform parent,float x,float y,float width,float height,string placeholder)
        {
            var viewport=ArtPanel(parent,"Scrollable introduction",x,y,width,height,UITheme.Field,true,false);
            viewport.gameObject.AddComponent<RectMask2D>();
            var input=ArtField(viewport,0,0,width-14,height,placeholder,true);
            input.textComponent.resizeTextForBestFit=false;input.textComponent.fontSize=18;
            input.textComponent.verticalOverflow=VerticalWrapMode.Overflow;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=(RectTransform)input.transform;
            scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            var track=ArtPanel(viewport,"Text scrollbar",width-12,4,8,height-8,new Color(0,0,0,.1f),false,false);
            var handle=ArtPanel(track,"Text scrollbar handle",0,0,8,height-8,UITheme.Muted,false,false);handle.sizeDelta=Vector2.zero;
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=handle.GetComponent<Image>();bar.direction=Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar=bar;
            var sizing=viewport.gameObject.AddComponent<ScrollableIntroduction>();sizing.Configure(input,scroll,height);
            return input;
        }
        void BuildSavedCharacterCard()
        {
            savedCharacterCard=ArtPanel(studioCard,"Saved character card",464,334,394,199,UITheme.Field,true,false);
            var raw=ArtRect(savedCharacterCard,"Current avatar",new Vector2(8,-8),new Vector2(142,183)).gameObject.AddComponent<RawImage>();raw.raycastTarget=false;
            savedCharacterTexture=new RenderTexture(320,400,24){antiAliasing=2};raw.texture=savedCharacterTexture;
            savedCharacterCamera=new GameObject("Saved avatar camera").AddComponent<Camera>();savedCharacterCamera.orthographic=true;savedCharacterCamera.orthographicSize=1.55f;savedCharacterCamera.cullingMask=1<<29;
            savedCharacterCamera.backgroundColor=UITheme.Field;savedCharacterCamera.clearFlags=CameraClearFlags.SolidColor;savedCharacterCamera.targetTexture=savedCharacterTexture;
            savedCharacterCamera.transform.position=new Vector3(1100,1.4f,5);savedCharacterCamera.transform.LookAt(new Vector3(1100,1.25f,0));
            savedCharacterLight=new GameObject("Saved avatar light");var light=savedCharacterLight.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.cullingMask=1<<29;light.transform.rotation=Quaternion.Euler(35,155,0);
            savedCharacterLabel=ArtText(savedCharacterCard,"尚未生成角色",163,15,216,48,18,UITheme.Ink,FontStyle.Bold);
            savedPersonaLabel=ArtText(savedCharacterCard,"",163,72,216,60,15,UITheme.Muted);
            ArtButton(savedCharacterCard,"角色卡",163,142,116,36,()=>{savedCharacterDetails.gameObject.SetActive(!savedCharacterDetails.gameObject.activeSelf);RefreshSavedCharacterCard(true);},UITheme.Paper,UITheme.Ink);
            savedCharacterDetails=ArtPanel(studioCard,"Expanded character card",464,318,394,230,UITheme.Paper,true,true);
            ArtButton(savedCharacterDetails,"关闭",294,10,86,32,()=>savedCharacterDetails.gameObject.SetActive(false),UITheme.Field,UITheme.Ink);
            savedCharacterDetails.gameObject.SetActive(false);RefreshSavedCharacterCard();
        }
        void RefreshSavedCharacterCard(bool force=false)
        {
            if(!savedCharacterCard)return;
            string signature=JsonUtility.ToJson(profile)+"|"+net.Sample+"|"+GameLocalization.English;
            if(!force&&signature==savedCharacterSignature)return;savedCharacterSignature=signature;
            var appearance=AppearanceData.Resolve(profile?.appearance,profile?.color??"#77C8BD",profile?.outfit??0);
            appearance.character=AISkinRegistry.CharacterForSkin(AISkinRegistry.Verified(profile));
            if(savedCharacterActor==null){savedCharacterActor=new ToyActor("Saved character",appearance.topColor,0,null,appearance);savedCharacterActor.root.transform.position=new Vector3(1100,1,0);savedCharacterActor.label.text="";}
            savedCharacterActor.ApplyAppearance(appearance);
            var persona=profile?.personas?.FirstOrDefault(a=>a.visualId==TraitAbilityCatalog.VisualId)??profile?.personas?.FirstOrDefault();
            if(savedCharacterActor.held)Destroy(savedCharacterActor.held);
            if(persona!=null){savedCharacterActor.held=ToyFactory.Item(persona.visualId,persona.color??"#77C8BD",savedCharacterActor.PersonaAttachment,persona.name,persona.emblemPath,persona.emblemCrop,persona.generatedBy,persona.wordText,persona.abilityId);savedCharacterActor.held.transform.localScale=Vector3.one*.72f;savedCharacterActor.held.transform.position=new Vector3(1100.67f,1.05f,.4f);if(persona.visualId=="school-disc")savedCharacterActor.held.transform.rotation=Quaternion.Euler(90,0,0);if(persona.visualId==TraitAbilityCatalog.VisualId)savedCharacterActor.held.transform.rotation=Quaternion.Euler(0,-16,0);}
            foreach(var labelMesh in savedCharacterActor.root.GetComponentsInChildren<TextMesh>())labelMesh.text="";
            foreach(var t in savedCharacterActor.root.GetComponentsInChildren<Transform>())t.gameObject.layer=29;
            string label=profile==null||(profile.personas?.Length??0)==0?"尚未生成角色":net.Sample?"练习角色（非 AI）":"当前已保存角色";
            savedCharacterLabel.text=GameLocalization.Text(label)+"\n"+GameLocalization.Protect(profile?.nickname??"");
            savedPersonaLabel.text=profile==null?GameLocalization.Text("生成后展示真实角色与道具"):"Persona × "+(profile.personas?.Length??0)+"\n"+(persona==null?"—":GameLocalization.PersonaName(persona));
            foreach(Transform t in savedCharacterDetails)if(t.name=="Identity detail")Destroy(t.gameObject);
            var detail=ArtText(savedCharacterDetails,label+"\n"+GameLocalization.Protect(profile?.nickname??"")+"\n"+string.Join(" / ",(profile?.personas??Array.Empty<PersonaAsset>()).Select(GameLocalization.PersonaName)),15,48,360,170,16,UITheme.Ink);detail.name="Identity detail";
        }
        void DestroySavedCharacterPreview()
        {if(savedCharacterActor!=null)Destroy(savedCharacterActor.root);if(savedCharacterCamera)Destroy(savedCharacterCamera.gameObject);if(savedCharacterLight)Destroy(savedCharacterLight);if(savedCharacterTexture){savedCharacterTexture.Release();Destroy(savedCharacterTexture);}}
    }
    // Input keeps its complete text layout; the outer viewport scrolls it without losing characters.
    public sealed class ScrollableIntroduction:MonoBehaviour
    {
        InputField input;ScrollRect scroll;float minimum;string previous;int caret=-1;
        public void Configure(InputField value,ScrollRect owner,float height){input=value;scroll=owner;minimum=height;}
        void LateUpdate()
        {
            if(!input)return;bool changed=previous!=input.text;bool moved=caret!=input.caretPosition;
            if(!changed&&!moved)return;previous=input.text;caret=input.caretPosition;
            var text=input.textComponent;float height=Mathf.Max(minimum,text.preferredHeight+26);
            scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height-10);
            if(input.isFocused&&(changed||moved))
            {
                var prefix=input.text.Substring(0,Mathf.Clamp(caret,0,input.text.Length));
                var settings=text.GetGenerationSettings(new Vector2(text.rectTransform.rect.width,0));
                float lineY=text.cachedTextGeneratorForLayout.GetPreferredHeight(prefix+"M",settings)/text.pixelsPerUnit;
                float top=scroll.content.anchoredPosition.y,view=scroll.viewport.rect.height;
                if(lineY>top+view-28)top=lineY-view+28;else if(lineY<top+24)top=Mathf.Max(0,lineY-24);
                scroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(top,0,height-view));
            }
        }
    }
}
