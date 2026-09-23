using System;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    [Serializable, UnityEngine.Scripting.Preserve] public class AppearanceData
    {
        public int version=1;
        // The selected character is carried by the trusted Profile/PublicPlayer field.
        // It is deliberately absent from the catalog-bound API appearance JSON.
        [NonSerialized] public string character="normal";
        public string expression="friendly";
        public string hair="short",top="hoodie",bottom="trousers",shoes="canvas",headAccessory="none",bag="none";
        public string skinColor="#F3CDA9",hairColor="#493A36",topColor="#79CFC5",bottomColor="#202A3D",shoeColor="#FFF9EF",accessoryColor="#FFD773";
        public bool SameAs(AppearanceData a)=>a!=null&&character==a.character&&SameStyleAs(a);
        public bool SameStyleAs(AppearanceData a)=>a!=null&&version==a.version&&expression==a.expression&&hair==a.hair&&top==a.top&&bottom==a.bottom&&shoes==a.shoes&&headAccessory==a.headAccessory&&bag==a.bag&&skinColor==a.skinColor&&hairColor==a.hairColor&&topColor==a.topColor&&bottomColor==a.bottomColor&&shoeColor==a.shoeColor&&accessoryColor==a.accessoryColor;
        public AppearanceData Copy()=>(AppearanceData)MemberwiseClone();
        public string Get(string slot)=>(string)GetType().GetField(slot).GetValue(this);
        public void Set(string slot,string value)=>GetType().GetField(slot).SetValue(this,value);
        public static AppearanceData Resolve(AppearanceData data,string color="#79CFC5",int outfit=0)
        {
            var result=data?.Copy()??new AppearanceData{top=outfit==1?"varsity":outfit==2?"sport":"hoodie",topColor=color};
            var defaults=new AppearanceData();
            foreach(var slot in AppearanceCatalog.Data.slots)
            {var value=result.Get(slot.id);if(!slot.options.Any(x=>x.id==value))result.Set(slot.id,defaults.Get(slot.id));}
            result.version=1;result.character=result.character=="ds"?"ds":"normal";return result;
        }
        public static AppearanceData Demo(int i)
        {
            switch(i%3)
            {
                case 0:return new AppearanceData{hair="curly",top="hoodie",headAccessory="headphones",topColor="#79CFC5"};
                case 1:return new AppearanceData{hair="short",top="sport",bottom="shorts",shoes="sneakers",headAccessory="cap",skinColor="#B77955",topColor="#F3AFC2",accessoryColor="#9FAFF2"};
                default:return new AppearanceData{hair="tied",top="shirt",bottom="skirt",shoes="boots",bag="sling",topColor="#9FAFF2",skinColor="#DDA77B",accessoryColor="#FFD773"};
            }
        }
    }
    [Serializable] public class AppearanceOption {public string id,label;}
    [Serializable] public class AppearanceSlot {public string id;public AppearanceOption[] options;}
    [Serializable] public class AppearanceCatalog
    {
        public int version;public AppearanceSlot[] slots;
        static AppearanceCatalog data;
        public static AppearanceCatalog Data=>data??(data=JsonUtility.FromJson<AppearanceCatalog>(Resources.Load<TextAsset>("appearance-catalog").text));
    }
}
