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
        public static void BuildPersonaVisuals(){PersonaVisualTools.BuildPrefabs();}
        public static void ValidatePersonaVisuals(){PersonaVisualTools.Validate();}
        public static void BuildDeliverables(){BuildMac();BuildServer();BuildWeb();Debug.Log("ABOUTME_DELIVERABLES_BUILT");}
        [MenuItem("About Me/1. Generate project scenes")]
        public static void Generate()
        {
            EditorSettings.serializationMode=SerializationMode.ForceText;
            EditorSettings.externalVersionControl="Visible Meta Files";
            PlayerSettings.companyName="About Me Studio";PlayerSettings.productName="About Me";
            PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.runInBackground=true;PlayerSettings.resizableWindow=true;
            PlayerSettings.iOS.microphoneUsageDescription="录制你的自我介绍，用于语音转写和生成 Persona。";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone,"com.aboutme.party");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.allowUnsafeCode=true;
            PlayerSettings.insecureHttpOption=InsecureHttpOption.DevelopmentOnly;
            StartMenuAssetImporter.ApplyAll();
            const string materialPath="Assets/AboutMe/Resources/ToyBaseMaterial.mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(materialPath))AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")),materialPath);
            PersonaVisualTools.BuildPrefabs();
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
            Test("sample items retain only a verified selected skin",()=>{
                var sample=P("cosmetic-sample",1);sample.characterSkin="claude-girl";
                var locked=new Match("LOCKED",new MatchRules());Assert(locked.Add(sample),"sample join failed");Assert(locked.View(sample.playerId).players.Single().characterSkin=="normal","unverified Claude skin shown");
                sample=P("trusted-sample",1);sample.dsGirlUnlocked=true;sample.claudeGirlUnlocked=true;sample.characterSkin="claude-girl";
                var trusted=new Match("TRUSTED",new MatchRules());Assert(trusted.Add(sample),"trusted sample join failed");Assert(trusted.View(sample.playerId).players.Single().characterSkin=="claude-girl","verified Claude skin lost");
            });
            Test("dropped and carried items still count as life",()=>{var m=M();SetGround(m,"b",m.Players["a"].position);m.Interact(m.Players["a"]);Assert(m.Remaining("b")==2&&!m.Players["b"].eliminated,"premature death");});
            Test("public snapshot does not reveal opponent inventory",()=>{var m=M(8);var v=m.View("a");Assert(v.inventory.Length==8&&v.inventory.All(x=>x.ownerId=="a"),"private inventory leaked");Assert(v.ground.Length==0,"inventory exposed as ground");});
            Test("snapshots never expose introduction source",()=>{
                var m=M();foreach(var item in m.Items)item.asset.source="PRIVATE_INTRO_FRAGMENT";
                m.Phase=MatchPhase.Reveal;var reveal=m.View("a");
                Assert(reveal.revealed.Length>0,"reveal items missing");
                Assert(!JsonUtility.ToJson(reveal).Contains("\"source\""),"reveal serialized a private field");
                m.Drop(m.Players["b"],1);var view=m.View("a");
                Assert(view.ground.Length>0&&view.inventory.Length>0,"snapshot items missing");
                var json=JsonUtility.ToJson(view);
                Assert(!json.Contains("PRIVATE_INTRO_FRAGMENT"),"serialized snapshot leaked source");
                Assert(!json.Contains("\"source\""),"serialized snapshot exposed a private field");
                var carried=m.Items.First(x=>x.state==ItemState.Ground);carried.state=ItemState.Carried;carried.carrierId="a";
                Assert(!JsonUtility.ToJson(m.View("b")).Contains("\"source\""),"carried item serialized a private field");
                Assert(m.Items.All(x=>x.asset.source=="PRIVATE_INTRO_FRAGMENT"),"projection changed authority state");
            });
            Test("contested pickup has one winner",()=>{var m=M();SetGround(m,"b",m.Players["a"].position);m.Players["b"].position=m.Players["a"].position;var item=m.Items.First(x=>x.ownerId=="b");m.Interact(m.Players["a"]);m.Interact(m.Players["b"]);Assert(m.Players["a"].carrying!=m.Players["b"].carrying,"duplicated pickup");Assert(item.carrierId=="a","pickup stolen");});
            Test("interrupt returns carried item without consumption",()=>{var m=M();SetGround(m,"b",m.Players["a"].position);m.Interact(m.Players["a"]);var item=m.Item(m.Players["a"].carrying);m.Hit(m.Players["a"],m.Players["b"],5,0,false);Tick(m,.2f);Assert(item.state==ItemState.Ground&&m.Remaining("b")==2&&m.Eaten.Count==0,"interrupted item consumed");});
            Test("last consumed persona alone eliminates and settles once",()=>{var m=M(1);SetGround(m,"b",m.Players["a"].position);m.Interact(m.Players["a"]);Tick(m,.2f);Assert(m.Players["b"].eliminated&&m.Winner=="a"&&m.Phase==MatchPhase.Finished,"no correct winner");Tick(m,.3f);Assert(m.Eaten.Count==1,"duplicate result");Assert(m.Players["b"].profile.personas.Length==1,"persistent persona deleted");});
            Test("recover preserves original instance and ownership",()=>{var m=M();SetGround(m,"a",m.Players["a"].position);var id=m.Items[0].id;m.Interact(m.Players["a"]);Tick(m,.14f);Assert(m.Item(id).state==ItemState.Equipped&&m.Remaining("a")==2,"recover failed");});
            Test("equipped item drops before backups",()=>{var m=M();var p=m.Players["a"];string equipped=p.equipped;m.Drop(p,1);Assert(m.Item(equipped).state==ItemState.Ground&&m.Items.Count(x=>x.ownerId=="a"&&x.state==ItemState.Inventory)==1,"wrong drop order");});
            Test("fall with no carried personas does not delete ground life",()=>{var m=M();SetGround(m,"a",Vector3.zero);m.Players["a"].position=new Vector3(45,-10,0);Tick(m,.04f);Assert(m.Players["a"].position.y>0&&m.Remaining("a")==2,"fall bypasses life rule");});
            Test("out of bounds drops remain ground and accessible",()=>{var m=M();SetGround(m,"a",new Vector3(45,-9,0));Tick(m,.02f);Assert(m.Items.Where(x=>x.ownerId=="a").All(x=>x.state==ItemState.Ground&&Mathf.Abs(x.position.x)<=11&&x.position.y>0),"lost ground item");});
            Test("reconnect does not duplicate persona or revive eliminated player",()=>{var m=M();m.Disconnect("a");Assert(!m.Players["a"].connected,"not disconnected");m.Add(P("a",8));Assert(m.Items.Count==4&&m.Players["a"].connected,"reconnect duplicate");});
            Test("input sequence and finite checks reject stale movement",()=>{var m=M();m.Receive("a",new Command{sequence=5,x=1});m.Receive("a",new Command{sequence=4,x=-1});Assert(m.Players["a"].input.x==1,"stale command");m.Receive("a",new Command{sequence=6,x=float.NaN});Assert(m.Players["a"].sequence==5,"nonfinite accepted");});
            Test("reveal protected, timeout settles without burst",()=>{var m=M();m.Phase=MatchPhase.Reveal;m.Receive("a",new Command{sequence=0,action="use_item"});Assert(m.Players["b"].stability==100,"reveal attack");Tick(m,1.2f);Assert(m.Phase==MatchPhase.Finished&&m.EndReason=="timeout","missing timeout");Assert(m.Players.Values.All(x=>!x.eliminated),"timeout eliminated players");});
            Test("grabbing item is not direct theft",()=>{var m=M();m.Players["b"].position=m.Players["a"].position+Vector3.forward*1.5f;var item=m.Item(m.Players["b"].equipped);m.Receive("a",new Command{sequence=0,action="grab_item"});Assert(item.ownerId=="b"&&item.state==ItemState.Equipped,"grab stole item");});
            Test("host starts with any two players without ready",()=>{var m=new Match("ROOM",new MatchRules());m.Add(P("host"));m.Add(P("guest"));m.Receive("host",new Command{sequence=0,action="start"});Assert(m.Phase==MatchPhase.Reveal,"two-player room did not start");});
            Match CampusMatch()
            {
                var m=new Match("CAMPUS",new MatchRules{revealDuration=.01f,handInterval=10000,finalTime=20000});
                m.Add(P("a",4));m.Add(P("b",4));m.Phase=MatchPhase.Reveal;m.Tick(.02f);return m;
            }
            Test("campus stairs reach the connected upper deck",()=>{
                var m=CampusMatch();var p=m.Players["a"];p.position=new Vector3(-21,1,-19);
                for(int i=0;i<110;i++){p.input.z=1;p.lastInput=m.Time;m.Tick(.02f);}
                Assert(p.position.z> -10&&Mathf.Abs(p.position.y-5.5f)<.1f,"stairs not walkable: "+p.position);
                Assert(m.Items.Count(x=>x.ownerId=="a"&&x.state==ItemState.Ground)==0,"stairs caused fall drop");
                Assert(Mathf.Abs(Match.Floor(new Vector3(-27.75f,5.5f,23))-4.5f)<.01f&&Mathf.Abs(Match.Floor(new Vector3(0,5.5f,22))-4.5f)<.01f&&Mathf.Abs(Match.Floor(new Vector3(27.75f,5.5f,23))-4.5f)<.01f,"upper U is disconnected");
            });
            Test("upper fall clears stability and drops exactly one without teleport",()=>{
                var m=CampusMatch();var p=m.Players["a"];p.position=new Vector3(-25.6f,5.5f,3);p.input.x=1;p.lastInput=m.Time;
                Tick(m,.85f);
                Assert(p.position.x< -20&&Mathf.Abs(p.position.z-3)<.1f&&Mathf.Abs(p.position.y-1)<.05f,"fall teleported or failed to land: "+p.position);
                Assert(p.stability==0,"fall retained stability");
                Assert(m.Items.Count(x=>x.ownerId=="a"&&x.state==ItemState.Ground)==1,"fall did not drop exactly one");
                Tick(m,.25f);Assert(m.Items.Count(x=>x.ownerId=="a"&&x.state==ItemState.Ground)==1,"landing repeated drop");
            });
            Test("walking downstairs and normal jumps do not trigger upper fall",()=>{
                var m=CampusMatch();var p=m.Players["a"];p.position=new Vector3(21,5.5f,-9);p.fallOriginHeight=4.5f;
                for(int i=0;i<100;i++){p.input.z=-1;p.lastInput=m.Time;m.Tick(.02f);}
                Assert(p.position.y<1.1f&&p.stability==100,"stairs descent counted as fall");
                p.input.z=0;p.verticalSpeed=m.Rules.jumpSpeed;Tick(m,1);
                Assert(m.Items.Count(x=>x.ownerId=="a"&&x.state==ItemState.Ground)==0,"normal jump dropped persona");
            });
            Test("upper gallery does not snap ground players upwards",()=>{
                Assert(Match.Floor(new Vector3(-27.75f,1,3))==0,"ground actor snapped onto deck");
                Assert(CampusGeometry.Blocked(new Vector3(-31.4f,5.5f,10)),"closed club room can be entered");
                Assert(CampusGeometry.Blocked(new Vector3(-25.45f,5.5f,9)),"rail does not block");
                Assert(!CampusGeometry.Blocked(new Vector3(-25.45f,5.5f,3)),"designated upper fall opening blocked");
            });
            Test("public dynamic props are only boxes and bins",()=>Assert(CampusMatch().Props.All(x=>x.visual=="box"||x.visual=="trash"),"unexpected public prop"));
            PersonaVisualTools.Validate();
            Test("hand telegraphs upstairs fist and applies contact damage once",()=>{
                var m=CampusMatch();m.Rules.handInterval=1;
                // Enter a fresh reveal so the interval is scheduled from the chosen rule.
                m.Phase=MatchPhase.Reveal;m.Tick(.02f);var p=m.Players["a"];p.position=new Vector3(-27.75f,5.5f,3);
                Tick(m,1.02f);Assert(m.View("a").handFist,"upper hand did not select fist");
                Assert(m.Items.Count(x=>x.ownerId=="a"&&x.state==ItemState.Ground)==0,"hand damaged before contact");
                Tick(m,.7f);Assert(m.Items.Count(x=>x.ownerId=="a"&&x.state==ItemState.Ground)==1,"hand contact did not drop one");
            });
            Test("moving out of telegraphed hand contact avoids the strike",()=>{
                var m=CampusMatch();m.Rules.handInterval=1;m.Phase=MatchPhase.Reveal;m.Tick(.02f);Tick(m,1.02f);
                m.Players["a"].position=new Vector3(0,1,0);Tick(m,.7f);
                Assert(m.Items.All(x=>x.state!=ItemState.Ground),"hand hit outside its contact area");
            });
            Match MechanicMatch(string template,string visual)
            {
                var m=CampusMatch();var item=m.Item(m.Players["a"].equipped);item.asset.templateId=template;item.asset.visualId=visual;
                m.Players["a"].position=new Vector3(0,1,0);m.Players["a"].yaw=0;m.Players["b"].position=new Vector3(0,1,4);return m;
            }
            void Heavy(Match m,string player="a")
            {
                var p=m.Players[player];int seq=p.sequence+1;m.Receive(player,new Command{sequence=seq,action="use_item",yaw=p.yaw});Tick(m,.24f);
            }
            Test("ricochet original life reflects with a bounded server count",()=>{
                var m=MechanicMatch("Ricochet","basketball-cc0");var item=m.Item(m.Players["a"].equipped);Heavy(m);
                Assert(item.state==ItemState.Thrown&&item.throwBounces==2&&m.Players["a"].equipped=="","ricochet did not throw original life");
                item.position=new Vector3(39.9f,1,0);item.velocity=Vector3.right*12;Tick(m,.02f);Assert(item.velocity.x<0&&item.throwBounces==1&&item.ownerId=="a","ricochet did not reflect once");
            });
            Test("school disc original life throws, hits once and remains grounded",()=>{
                var m=MechanicMatch("Disc","school-disc");var item=m.Item(m.Players["a"].equipped);var source=item.asset;
                source.emblemPath="/school-emblems/test.png";source.emblemCrop="0.06,0.06,0.88,0.88";source.generatedBy="DeepSeek Flash Vision";
                m.Players["b"].position=new Vector3(0,1,4.5f);m.Players["b"].stability=20;Heavy(m);
                Assert(item.state==ItemState.Thrown&&item.velocity.z>0&&m.Players["a"].equipped=="","school disc did not launch original life");
                var visible=m.View("b").ground.Single(x=>x.id==item.id).asset;
                Assert(visible.emblemPath==source.emblemPath&&visible.emblemCrop==source.emblemCrop&&visible.generatedBy==source.generatedBy,"emblem metadata lost");
                Tick(m,1.5f);Assert(m.Players["b"].impactId>0&&m.Items.Any(x=>x.ownerId=="b"&&x.state==ItemState.Ground),"school disc missed or did not drop target life");
                Assert(item.state==ItemState.Ground&&m.Remaining("a")==4,"school disc lost original life");
                int impacts=m.Players["b"].impactId;Tick(m,.4f);Assert(m.Players["b"].impactId==impacts&&item.state==ItemState.Ground,"landed disc hit again or recalled");
            });
            Test("all Disc visuals land without automatic return",()=>{
                var m=MechanicMatch("Disc","photo-frame");m.Players["b"].position=new Vector3(20,1,0);var item=m.Item(m.Players["a"].equipped);Heavy(m);
                Assert(item.state==ItemState.Thrown&&m.Players["a"].equipped=="","non-school Disc did not throw original life");
                Tick(m,2);Assert(item.state==ItemState.Ground&&m.Players["a"].equipped==""&&m.Remaining("a")==4,"non-school Disc recalled or consumed its life");
            });
            Test("school discs contain only the emblem or school fallback",()=>{
                foreach(var school in new[]{"清华大学","北京大学","中山大学"})
                {
                    var disc=ToyFactory.Item("school-disc","#77C8BD",null,school+"校徽圆盘","","","DeepSeek Flash Vision");
                    var labels=disc.GetComponentsInChildren<TextMesh>(true).Select(x=>x.text).ToArray();
                    Assert(!labels.Contains("DeepSeek 生成")&&labels.Any(x=>x.Contains(school.Substring(0,2))),school+" has unwanted text or lacks a fallback");
                    UnityEngine.Object.DestroyImmediate(disc);
                }
            });
            Test("tether pulls only a forward in-range target",()=>{
                var m=MechanicMatch("Tether","flashlight");Heavy(m);Assert(m.Players["b"].impulse.z<0,"tether did not pull target");
                var blocked=MechanicMatch("Tether","flashlight");blocked.Players["b"].position=new Vector3(0,1,-4);Heavy(blocked);Assert(blocked.Players["b"].impulse.sqrMagnitude<.01f,"tether pulled target behind owner");
            });
            Test("cushion consumes one heavy hit and rebounds attacker",()=>{
                var m=MechanicMatch("Cushion","pillow-a");m.Players["b"].position=new Vector3(0,1,-2);Heavy(m);var before=m.Players["a"].stability;m.Hit(m.Players["a"],m.Players["b"],80,8,true);
                Assert(m.Players["a"].stability==before&&m.Players["a"].cushionUntil==0&&m.Players["b"].impulse.z<0,"cushion did not absorb and rebound");
            });
            Test("barricade blocks movement and breaks after two heavy strikes",()=>{
                var m=MechanicMatch("Barricade","book-stack");Heavy(m);var barrier=m.Effects.Single(x=>x.kind=="Barricade");var p=m.Players["b"];p.position=barrier.position+Vector3.back*1.5f;p.position.y=1;p.input.z=1;p.input.yaw=0;p.lastInput=m.Time;Tick(m,.25f);
                Assert(Vector3.Distance(p.position,barrier.position)>=barrier.radius+.3f,"barricade did not block movement");
                p.position=barrier.position+Vector3.back*1.2f;p.position.y=1;p.input=new Command();p.yaw=0;p.nextAttack=0;m.Item(p.equipped).asset.templateId="HeavyWeapon";Heavy(m,"b");Assert(barrier.health==1,"first heavy did not damage barricade");
                p.nextAttack=0;p.chargingSince=-1;Heavy(m,"b");Assert(!m.Effects.Contains(barrier),"barricade did not break after two heavy strikes health="+barrier.health+" pending="+p.pendingAction+" kind="+p.actionKind+" stagger="+p.staggerUntil+" time="+m.Time+" charging="+p.chargingSince);
            });
            MenuRuleTests.Run();
            Debug.Log("ABOUTME_RULE_TESTS_PASSED "+passed+" + menu/NPC suite");
        }
        static void PrepareBuildScenes()
        {
            if(Environment.GetEnvironmentVariable("ABOUTME_EXISTING_SCENES")!="1"){Generate();return;}
            if(EditorBuildSettings.scenes.Length!=5||EditorBuildSettings.scenes.Any(s=>!s.enabled||!File.Exists(s.path)))
                throw new Exception("Frozen release requires all five generated scenes");
        }
        [MenuItem("About Me/3. Build macOS demo")]
        public static void BuildMac()
        {
            PrepareBuildScenes();TestRules();
            bool isolated=Environment.GetEnvironmentVariable("ABOUTME_QA_ISOLATED")=="1";
            string originalProduct=PlayerSettings.productName;
            string originalIdentifier=PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Standalone);
            try
            {
                if(isolated)
                {
                    PlayerSettings.productName="About Me Integration QA";
                    PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone,"com.aboutme.party.integrationqa");
                }
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Select(s=>s.path).ToArray(),locationPathName=Environment.GetEnvironmentVariable("ABOUTME_MAC_OUTPUT")??"../builds/mac/About Me.app",target=BuildTarget.StandaloneOSX,options=Environment.GetEnvironmentVariable("ABOUTME_RELEASE_BUILD")=="1"?BuildOptions.None:BuildOptions.Development});
                if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Mac build failed: "+result.summary.result);
            }
            finally
            {
                if(isolated)
                {
                    PlayerSettings.productName=originalProduct;
                    PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone,originalIdentifier);
                }
            }
            Debug.Log("ABOUTME_MAC_BUILD_OK");
        }
        [MenuItem("About Me/4. Build WebGL demo")]
        public static void BuildWeb()
        {
            PrepareBuildScenes();PlayerSettings.WebGL.template="PROJECT:AboutMe";PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Select(s=>s.path).ToArray(),locationPathName=Environment.GetEnvironmentVariable("ABOUTME_WEB_OUTPUT")??"../builds/web",target=BuildTarget.WebGL,options=Environment.GetEnvironmentVariable("ABOUTME_RELEASE_BUILD")=="1"?BuildOptions.None:BuildOptions.Development});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Web build failed");
            Debug.Log("ABOUTME_WEB_BUILD_OK");
        }
        [MenuItem("About Me/5. Build Linux server (requires module)")]
        public static void BuildServer()
        {
            Generate();
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Select(s=>s.path).ToArray(),locationPathName=Environment.GetEnvironmentVariable("ABOUTME_SERVER_OUTPUT")??"../builds/linux/AboutMeServer",target=BuildTarget.StandaloneLinux64,subtarget=(int)StandaloneBuildSubtarget.Server,options=BuildOptions.Development});
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
