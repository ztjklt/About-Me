using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AboutMe.Editor
{
    public static class SampleProfileAudit
    {
        [MenuItem("About Me/Test local sample profile")]
        public static void Run()
        {
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("catalog").text);
            var sample=GameNetwork.SampleProfile("audit","样例玩家");
            var expected=new[]{"Projectile","Disc","Melee","HeavyWeapon","Utility","Deployable","Summon","SpecialAbility"};
            if(sample.personas.Length!=expected.Length||!sample.personas.Select(x=>x.templateId).SequenceEqual(expected))
                throw new Exception("SAMPLE_PROFILE_FAILED expected eight base templates");
            if(sample.personas.Any(x=>x.source!="明确标注的测试样例"||!catalog.items.Any(c=>c.templateId==x.templateId&&c.visualId==x.visualId)))
                throw new Exception("SAMPLE_PROFILE_FAILED sample is not labelled or listed in the catalog");
            var solo=new LocalMatchSession(sample);
            try
            {
                if(solo.Match.Players.Count!=LocalMatchSession.RecommendedNpcCount+1||solo.Match.Items.Count!=8*(LocalMatchSession.RecommendedNpcCount+1)||solo.Current.inventory.Length!=8)
                    throw new Exception("SAMPLE_PROFILE_FAILED local match inventory");
            }
            finally { solo.Close(); }
            Debug.Log("ABOUTME_SAMPLE_PROFILE_PASS eight labelled items, eight templates, solo match inventory");
        }
    }
}
