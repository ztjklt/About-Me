using System;
using System.IO;
using System.Linq;
using AboutMe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AboutMe.Editor
{
    public static class ProjectTools
    {
        public static void BuildDeliverables(){BuildMac();BuildServer();BuildWeb();Debug.Log("ABOUTME_DELIVERABLES_BUILT");}
        [MenuItem("About Me/1. Generate project scenes")]
        public static void Generate()
        {
            EditorSettings.serializationMode=SerializationMode.ForceText;
            EditorSettings.externalVersionControl="Visible Meta Files";
            PlayerSettings.companyName="About Me Studio";PlayerSettings.productName="About Me";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.runInBackground=true;PlayerSettings.resizableWindow=true;
            PlayerSettings.iOS.microphoneUsageDescription="录制你的自我介绍，用于语音转写和生成 Persona。";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone,"com.aboutme.party");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.allowUnsafeCode=true;
            PlayerSettings.insecureHttpOption=InsecureHttpOption.DevelopmentOnly;
            const string materialPath="Assets/AboutMe/Resources/ToyBaseMaterial.mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(materialPath))AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")),materialPath);
            Directory.CreateDirectory("Assets/AboutMe/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("About Me Bootstrap").AddComponent<GameBootstrap>();
            EditorSceneManager.SaveScene(scene,"Assets/AboutMe/Scenes/Bootstrap.unity");
            // UI phases share the loaded arena. Named scenes are entry points for collaborators.
            foreach(var name in new[]{"CharacterCreation","Room","Arena","Results"})
            {
                var entry=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                new GameObject("About Me Bootstrap").AddComponent<GameBootstrap>();
                EditorSceneManager.SaveScene(entry,"Assets/AboutMe/Scenes/"+name+".unity");
            }
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/AboutMe/Scenes/Bootstrap.unity",true)};
            EditorSceneManager.OpenScene("Assets/AboutMe/Scenes/Bootstrap.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("ABOUTME_PROJECT_GENERATED");
        }
        [MenuItem("About Me/2. Run authoritative rule tests")]
        public static void TestRules()
        {
            int passed=0;
            void Test(string name,Action action){action();passed++;Debug.Log("RULE_PASS "+name);}
            void Assert(bool value,string message){if(!value)throw new Exception(message);}
            Profile P(string id,int n=2)=>new Profile{playerId=id,nickname=id,color="#77C8BD",personas=Enumerable.Range(0,n).Select(i=>new PersonaAsset{assetId=id+i,name="球"+i,templateId="Projectile",visualId="basketball",color="#FFAA55"}).ToArray()};
            Match M(int n=2){var m=new Match("TEST",new MatchRules{eatDuration=.12f,recoverDuration=.1f,revealDuration=.1f,handInterval=.5f,finalTime=1});m.Add(P("a",n));m.Add(P("b",n));m.Phase=MatchPhase.Reveal;for(int i=0;i<6;i++)m.Tick(.02f);return m;}
            void Tick(Match m,float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.02f);i++)m.Tick(.02f);}
            void SetGround(Match m,string owner,Vector3 at){m.Drop(m.Players[owner],99);foreach(var x in m.Items.Where(x=>x.ownerId==owner)){x.position=at;x.velocity=Vector3.zero;}}
            Test("variable inventory and maximum players",()=>{var m=M(9);Assert(m.Remaining("a")==9,"fixed persona count");for(int i=0;i<4;i++)Assert(m.Add(P("c"+i))==false,"mid-match join");m.Phase=MatchPhase.Lobby;for(int i=0;i<4;i++)Assert(m.Add(P("c"+i)),"six players");Assert(!m.Add(P("overflow")),"seventh player allowed");});
            Test("dropped and carried items still count as life",()=>{var m=M();SetGround(m,"b",m.Players["a"].position);m.Interact(m.Players["a"]);Assert(m.Remaining("b")==2&&!m.Players["b"].eliminated,"premature death");});
            Test("public snapshot does not reveal opponent inventory",()=>{var m=M(8);var v=m.View("a");Assert(v.inventory.Length==8&&v.inventory.All(x=>x.ownerId=="a"),"private inventory leaked");Assert(v.ground.Length==0,"inventory exposed as ground");});
            Test("contested pickup has one winner",()=>{var m=M();SetGround(m,"b",m.Players["a"].position);m.Players["b"].position=m.Players["a"].position;var item=m.Items.First(x=>x.ownerId=="b");m.Interact(m.Players["a"]);m.Interact(m.Players["b"]);Assert(m.Players["a"].carrying!=m.Players["b"].carrying,"duplicated pickup");Assert(item.carrierId=="a","pickup stolen");});
            Test("interrupt returns carried item without consumption",()=>{var m=M();SetGround(m,"b",m.Players["a"].position);m.Interact(m.Players["a"]);var item=m.Item(m.Players["a"].carrying);m.Hit(m.Players["a"],m.Players["b"],5,0,false);Tick(m,.2f);Assert(item.state==ItemState.Ground&&m.Remaining("b")==2&&m.Eaten.Count==0,"interrupted item consumed");});
            Test("last consumed persona alone eliminates and settles once",()=>{var m=M(1);SetGround(m,"b",m.Players["a"].position);m.Interact(m.Players["a"]);Tick(m,.2f);Assert(m.Players["b"].eliminated&&m.Winner=="a"&&m.Phase==MatchPhase.Finished,"no correct winner");Tick(m,.3f);Assert(m.Eaten.Count==1,"duplicate result");Assert(m.Players["b"].profile.personas.Length==1,"persistent persona deleted");});
            Test("recover preserves original instance and ownership",()=>{var m=M();SetGround(m,"a",m.Players["a"].position);var id=m.Items[0].id;m.Interact(m.Players["a"]);Tick(m,.14f);Assert(m.Item(id).state==ItemState.Equipped&&m.Remaining("a")==2,"recover failed");});
            Test("equipped item drops before backups",()=>{var m=M();var p=m.Players["a"];string equipped=p.equipped;m.Drop(p,1);Assert(m.Item(equipped).state==ItemState.Ground&&m.Items.Count(x=>x.ownerId=="a"&&x.state==ItemState.Inventory)==1,"wrong drop order");});
            Test("fall with no carried personas does not delete ground life",()=>{var m=M();SetGround(m,"a",Vector3.zero);m.Players["a"].position=new Vector3(16,-10,0);Tick(m,.04f);Assert(m.Players["a"].position.y>0&&m.Remaining("a")==2,"fall bypasses life rule");});
            Test("out of bounds drops remain ground and accessible",()=>{var m=M();SetGround(m,"a",new Vector3(30,-9,0));Tick(m,.02f);Assert(m.Items.Where(x=>x.ownerId=="a").All(x=>x.state==ItemState.Ground&&Mathf.Abs(x.position.x)<=11&&x.position.y>0),"lost ground item");});
            Test("reconnect does not duplicate persona or revive eliminated player",()=>{var m=M();m.Disconnect("a");Assert(!m.Players["a"].connected,"not disconnected");m.Add(P("a",8));Assert(m.Items.Count==4&&m.Players["a"].connected,"reconnect duplicate");});
            Test("input sequence and finite checks reject stale movement",()=>{var m=M();m.Receive("a",new Command{sequence=5,x=1});m.Receive("a",new Command{sequence=4,x=-1});Assert(m.Players["a"].input.x==1,"stale command");m.Receive("a",new Command{sequence=6,x=float.NaN});Assert(m.Players["a"].sequence==5,"nonfinite accepted");});
            Test("reveal is protected and final burst keeps ownership",()=>{var m=M();m.Phase=MatchPhase.Reveal;m.Receive("a",new Command{sequence=0,action="light"});Assert(m.Players["b"].stability==100,"reveal attack");Tick(m,1.2f);Assert(m.Phase==MatchPhase.FinalBurst&&m.Items.All(x=>x.state==ItemState.Ground),"missing final burst");Assert(m.Players.Values.All(x=>!x.eliminated),"burst eliminated players");});
            Test("grabbing item is not direct theft",()=>{var m=M();m.Players["b"].position=m.Players["a"].position+Vector3.forward*1.5f;var item=m.Item(m.Players["b"].equipped);m.Receive("a",new Command{sequence=0,action="grab_item"});Assert(item.ownerId=="b"&&item.state==ItemState.Equipped,"grab stole item");});
            Test("host starts with any two players without ready",()=>{var m=new Match("ROOM",new MatchRules());m.Add(P("host"));m.Add(P("guest"));m.Receive("host",new Command{sequence=0,action="start"});Assert(m.Phase==MatchPhase.Reveal,"two-player room did not start");});
            Debug.Log("ABOUTME_RULE_TESTS_PASSED "+passed);
        }
        [MenuItem("About Me/3. Build macOS demo")]
        public static void BuildMac()
        {
            Generate();TestRules();
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Select(s=>s.path).ToArray(),locationPathName="../builds/mac/About Me.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Mac build failed: "+result.summary.result);
            Debug.Log("ABOUTME_MAC_BUILD_OK");
        }
        [MenuItem("About Me/4. Build WebGL demo")]
        public static void BuildWeb()
        {
            Generate();PlayerSettings.WebGL.template="PROJECT:AboutMe";PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Select(s=>s.path).ToArray(),locationPathName="../builds/web",target=BuildTarget.WebGL,options=BuildOptions.Development});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Web build failed");
            Debug.Log("ABOUTME_WEB_BUILD_OK");
        }
        [MenuItem("About Me/5. Build Linux server (requires module)")]
        public static void BuildServer()
        {
            Generate();
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Select(s=>s.path).ToArray(),locationPathName="../builds/linux/AboutMeServer",target=BuildTarget.StandaloneLinux64,subtarget=(int)StandaloneBuildSubtarget.Server,options=BuildOptions.Development});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Linux server build failed; install Linux Dedicated Server module");
            Debug.Log("ABOUTME_SERVER_BUILD_OK");
        }
        [MenuItem("About Me/6. Export WeChat (requires configured AppID and CDN)")]
        public static void BuildWeChat()
        {
            Generate();
            var appid=Environment.GetEnvironmentVariable("WECHAT_APP_ID");var cdn=Environment.GetEnvironmentVariable("WECHAT_CDN_URL");
            if(string.IsNullOrWhiteSpace(appid)||string.IsNullOrWhiteSpace(cdn))throw new Exception("WECHAT_CONFIGURATION_REQUIRED: set WECHAT_APP_ID and WECHAT_CDN_URL; no test AppID is silently substituted.");
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("WeChatWASM.WXConvertCore")).FirstOrDefault(t=>t!=null);
            if(type==null)throw new Exception("Install the pinned WXSDK first: python3 scripts/install-wechat-sdk.py");
            type.GetMethod("Init").Invoke(null,null);
            var configuration=(UnityEngine.Object)type.GetProperty("config").GetValue(null);
            var serialized=new SerializedObject(configuration);var project=serialized.FindProperty("ProjectConf");
            project.FindPropertyRelative("Appid").stringValue=appid;project.FindPropertyRelative("CDN").stringValue=cdn;
            project.FindPropertyRelative("relativeDST").stringValue="../builds/wechat";
            project.FindPropertyRelative("DST").stringValue=Path.GetFullPath("../builds/wechat");serialized.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
            var result=type.GetMethod("DoExport").Invoke(null,new object[]{true});
            if(result.ToString()!="SUCCEED")throw new Exception("WeChat export failed: "+result);
        }
    }
}
