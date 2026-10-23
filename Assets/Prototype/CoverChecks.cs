using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace IrkutskPrototype
{
    public sealed class CoverChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Hidden).SetValue(o,value);
        static object Get(object o,string name)=>o.GetType().GetField(name,Hidden).GetValue(o);
        static void Call(object o,string name)=>o.GetType().GetMethod(name,Hidden).Invoke(o,null);
        static void Check(bool ok,string message)
        { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Place(Transform actor,Vector3 point)
        { var cc=actor.GetComponent<CharacterController>(); cc.enabled=false; actor.position=point; cc.enabled=true; actor.rotation=Quaternion.identity; Physics.SyncTransforms(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-cover-check")<0 && Array.IndexOf(Environment.GetCommandLineArgs(),"-cover-sandbox-check")<0) return;
            if(UnityEngine.Object.FindAnyObjectByType<CoverChecks>()) return;
            Application.runInBackground=true;
            var root=new GameObject("Stage two cover verification"); root.AddComponent<CoverChecks>();
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-cover-sandbox-check")>=0) DontDestroyOnLoad(root);
        }
        IEnumerator Start()
        {
            yield return null; yield return null;
            var tests=Array.IndexOf(Environment.GetCommandLineArgs(),"-cover-sandbox-check")>=0?RunSandbox(EscapeGame.Instance):Run(EscapeGame.Instance);
            while(true)
            {
                object wait;
                try { if(!tests.MoveNext()) break; wait=tests.Current; }
                catch(Exception e) { Debug.LogException(e); Application.Quit(1); yield break; }
                yield return wait;
            }
            Debug.Log("IRKUTSK_COVER_OK"); Application.Quit(0);
        }
        IEnumerator RunSandbox(EscapeGame game)
        {
            yield return new WaitForSeconds(.2f);
            Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="CoverSandbox","sandbox command loads the separate cover scene");
            Check(UnityEngine.Object.FindObjectsByType<CoverObstacle>().Length==2,"sandbox spawns exactly its two sample obstacles");
            Check(game && game.Hero.GetComponent<ActorPosture>() && !game.Soldier.enabled,"sandbox initializes playable crouching hero and optional NPC combat");
            var previous=game;
            UnityEngine.SceneManagement.SceneManager.LoadScene("CoverSandbox");
            yield return null; yield return null;
            Check(EscapeGame.Instance && EscapeGame.Instance!=previous && UnityEngine.Object.FindObjectsByType<CoverObstacle>().Length==2,"sandbox scene restart creates fresh game without duplicating sample obstacles");
        }
        static void KeyState(params Key[] keys)
        { InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys)); InputSystem.Update(); }
        static void Capture(EscapeGame game,string name)
        {
            var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-cover-capture-dir");
            if(index<0) return;
            var camera=UnityEngine.Object.FindAnyObjectByType<Camera>();
            if(game.Soldier.Caption) game.Soldier.Caption.transform.rotation=Quaternion.Euler(65,0,0);
            camera.transform.position=game.Hero.position+new Vector3(0,16,-7.5f); camera.transform.rotation=Quaternion.Euler(65,0,0); camera.orthographicSize=8;
            var target=new RenderTexture(1280,720,24); target.Create();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination=target });
            var previous=RenderTexture.active; RenderTexture.active=target;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(args[index+1],name+".png"),image.EncodeToPNG());
            RenderTexture.active=previous; target.Release(); Destroy(target); Destroy(image);
        }
        IEnumerator Run(EscapeGame game)
        {
            game.enabled=false;
            foreach(var z in game.Zombies) { z.enabled=false; z.gameObject.SetActive(false); }
            foreach(var h in game.Humans) { h.enabled=false; h.gameObject.SetActive(false); }
            Check(UnityEngine.Object.FindObjectsByType<CoverObstacle>().Length==0,"main scene contains no newly placed cover obstacles");
            Check(Application.CanStreamedLevelBeLoaded("CoverSandbox"),"optional cover sandbox is included as a separate scene");
            var lowPrefab=Resources.Load<GameObject>("Cover/LowCover"); var highPrefab=Resources.Load<GameObject>("Cover/HighCover");
            Check(lowPrefab && highPrefab,"low and high cover prefabs are available for later placement");
            var low=Instantiate(lowPrefab,new Vector3(20,0,0),Quaternion.identity);
            var high=Instantiate(highPrefab,Vector3.zero,Quaternion.identity);
            var soldier=game.Soldier; soldier.gameObject.SetActive(true);
            Place(soldier.transform,new Vector3(20,0,5)); soldier.transform.forward=Vector3.back;
            Place(game.Hero,new Vector3(20,0,-.97f));
            var posture=game.Hero.GetComponent<ActorPosture>(); posture.TrySetCrouched(false); game.Fog.Refresh();
            Check(!Sight.Clear(ActorPosture.Eye(soldier.transform),ActorPosture.Chest(game.Hero)) && Sight.CanSee(soldier.transform,game.Hero,12,110),"low obstacle hides torso while standing head remains visible");
            Capture(game,"cover-standing");
            Check(posture.TrySetCrouched(true) && Mathf.Abs(game.Controller.height-1)<.01f && Mathf.Abs(game.Controller.center.y-.5f)<.01f,"crouch reduces actual collider height while keeping feet grounded");
            Check(game.Hero.Find("Posture visuals").localScale.y<.6f,"crouch lowers visible character geometry as well as collider");
            Check(!Sight.CanSee(soldier.transform,game.Hero,12,110) && !game.SelectPatrolTarget(soldier),"crouching behind low cover hides hero from NPC perception");
            game.Fog.Refresh();
            Check(!game.Fog.IsVisible(soldier.transform) && !soldier.GetComponentInChildren<Renderer>().enabled,"crouched player fog is blocked by low obstacle");
            Capture(game,"cover-crouching");
            posture.TrySetCrouched(false); Place(game.Hero,new Vector3(20,0,5)); game.Hero.forward=Vector3.back;
            Place(soldier.transform,new Vector3(20,0,-.97f)); soldier.GetComponent<ActorPosture>().TrySetCrouched(true); game.Fog.Refresh();
            Check(!game.Fog.IsVisible(soldier.transform) && !soldier.GetComponentInChildren<Renderer>().enabled,"crouched enemy behind low cover is hidden by player fog");
            soldier.GetComponent<ActorPosture>().TrySetCrouched(false); game.Fog.Refresh();
            Check(game.Fog.IsVisible(soldier.transform) && soldier.GetComponentInChildren<Renderer>().enabled,"enemy standing above low cover becomes visible again");
            Place(game.Hero,new Vector3(20,0,-.97f)); posture.TrySetCrouched(true);
            Place(soldier.transform,new Vector3(20,0,5)); soldier.transform.forward=Vector3.back;
            Set(game,"hp",100f); var savedRandom=UnityEngine.Random.state; UnityEngine.Random.InitState(32);
            for(int i=0;i<8;i++) game.SoldierShot(soldier);
            Check((float)Get(game,"hp")==100,"low cover physically stops bullets aimed at crouched hero");
            posture.TrySetCrouched(false); game.Fog.Refresh();
            Check(game.Fog.IsVisible(soldier.transform) && soldier.GetComponentInChildren<Renderer>().enabled,"standing restores elevated player visibility over low cover");
            for(int i=0;i<8;i++) game.SoldierShot(soldier);
            Check((float)Get(game,"hp")<100,"exposed standing hero can be hit above low cover");
            UnityEngine.Random.state=savedRandom;
            posture.TrySetCrouched(true); game.Hero.forward=Vector3.forward;
            Set(game,"<PatrolHostile>k__BackingField",false); Set(game,"shotAt",0f); Set(game,"ammo",6);
            float health=soldier.Health; Call(game,"Shoot");
            Check(soldier.Health==health && !game.PatrolHostile,"crouched shot hits obstacle and does not give patrol visual identification");
            posture.TrySetCrouched(false); Set(game,"shotAt",0f); Call(game,"Shoot");
            Check(soldier.Health<health,"standing pistol muzzle clears low obstacle and can hit target");

            Place(game.Hero,new Vector3(0,0,-.97f)); Place(soldier.transform,new Vector3(0,0,5)); soldier.transform.forward=Vector3.back;
            Check(!Sight.CanSee(soldier.transform,game.Hero,12,110),"high cover hides standing character too");
            Set(game,"hp",100f); game.SoldierShot(soldier);
            Check((float)Get(game,"hp")==100,"high cover physically blocks standing-target fire");
            Place(game.Hero,new Vector3(20,0,-.97f)); posture.TrySetCrouched(true);
            var roof=new GameObject("Test ceiling"); roof.layer=8; roof.transform.position=game.Hero.position+Vector3.up*1.45f;
            var roofBox=roof.AddComponent<BoxCollider>(); roofBox.size=new Vector3(1,.2f,1); Physics.SyncTransforms();
            Check(!posture.TrySetCrouched(false) && posture.IsCrouched,"standing up is rejected beneath a low ceiling");
            roofBox.enabled=false; Destroy(roof); Physics.SyncTransforms();
            Check(posture.TrySetCrouched(false),"standing becomes possible after overhead obstruction is removed");

            if(Keyboard.current==null) InputSystem.AddDevice<Keyboard>();
            Set(game,"heroStunUntil",0f); KeyState(); KeyState(Key.C); Call(game,"Update");
            Check(posture.IsCrouched,"C key toggles player crouch through ordinary gameplay update");
            KeyState(Key.C); Call(game,"Update"); Check(posture.IsCrouched,"holding C does not repeatedly toggle posture");
            KeyState(); KeyState(Key.C); Call(game,"Update");
            Check(!posture.IsCrouched,"second C press restores standing posture"); KeyState();

            var slots=low.GetComponentsInChildren<CoverSlot>(); var back=Array.Find(slots,s=>s.transform.position.z<0);
            Check(back.ProtectsFrom(new Vector3(20,0,5)) && !Array.Find(slots,s=>s!=back).ProtectsFrom(new Vector3(20,0,5)),"cover selection distinguishes protected side from exposed side");
            var marauder=game.Humans.Find(h=>h.Role==HumanRole.Marauder);
            soldier.enabled=true; marauder.gameObject.SetActive(true); marauder.enabled=true;
            Check(back.TryReserve(soldier) && !back.TryReserve(marauder),"two NPCs cannot reserve the same cover slot");
            back.Release(soldier); Check(back.TryReserve(marauder),"released slot can be reserved by another NPC");
            marauder.enabled=false; Check(!back.Owner && back.TryReserve(soldier),"disabled owner relinquishes its cover reservation");
            low.SetActive(false); Check(!back.Owner,"disabling cover releases reservations");
            low.SetActive(true); marauder.gameObject.SetActive(false);

            soldier.enabled=false; Place(soldier.transform,new Vector3(20,0,-.97f));
            soldier.GetComponent<ActorPosture>().TrySetCrouched(false);
            Place(game.Hero,new Vector3(20,0,5)); posture.TrySetCrouched(false); Set(game,"hp",100f);
            Set(soldier,"<Health>k__BackingField",100f); Set(soldier,"<CurrentTarget>k__BackingField",null);
            Set(soldier,"targetVisible",false); Set(soldier,"coverSeekAt",0f); Set(game,"<PatrolHostile>k__BackingField",true);
            soldier.enabled=true; soldier.AlertTo(game.Hero);
            yield return new WaitForSeconds(.15f);
            Check(soldier.SelectedCover==back && back.Owner==soldier && soldier.GetComponent<ActorPosture>().IsCrouched,"live NPC selects protecting slot and crouches behind low cover");
            int shots=game.SoldierTracers; bool peeked=false,returned=false;
            float deadline=Time.time+8;
            while(Time.time<deadline && !returned)
            {
                if(soldier.SelectedCover && !soldier.GetComponent<ActorPosture>().IsCrouched && Vector3.Distance(soldier.transform.position,back.transform.position)>.7f) peeked=true;
                if(peeked && game.SoldierTracers>shots && soldier.GetComponent<ActorPosture>().IsCrouched) returned=true;
                yield return null;
            }
            Debug.Log("COVER_AI_STATE: "+soldier.State+" target="+soldier.CurrentTarget+" slot="+soldier.SelectedCover+" pos="+soldier.transform.position);
            Check(peeked && game.SoldierTracers>shots,"live NPC leaves hiding position and fires from a peek point");
            Check(returned && soldier.SelectedCover==back,"live NPC returns and crouches after peeking");
            soldier.enabled=false; Check(!back.Owner,"disabling covering NPC frees its selected slot");
            soldier.enabled=true; soldier.AlertTo(game.Hero);
            yield return new WaitForSeconds(.15f);
            soldier.Hit(500);
            Check(soldier.Downed && !back.Owner,"death releases a selected cover slot immediately");
            soldier.gameObject.SetActive(false);
            foreach(var actor in new[]{game.Medic,marauder})
            {
                actor.gameObject.SetActive(true); actor.enabled=true; Place(actor.transform,new Vector3(20,0,-.97f));
                Set(actor,"<CurrentTarget>k__BackingField",null); Set(actor,"targetVisible",false); Set(actor,"coverSeekAt",0f);
                actor.GetComponent<ActorPosture>().TrySetCrouched(false); actor.AlertTo(game.Hero);
                yield return new WaitForSeconds(.15f);
                Check(actor.SelectedCover==back && actor.GetComponent<ActorPosture>().IsCrouched,actor.Role+" can select and crouch in a registered cover slot");
                actor.enabled=false; actor.gameObject.SetActive(false);
            }
        }
    }
}
