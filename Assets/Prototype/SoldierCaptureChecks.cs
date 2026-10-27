using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace IrkutskPrototype
{
    public sealed class SoldierCaptureChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        EscapeGame game; HumanAgent soldier,medic; Material material;
        static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Hidden).SetValue(obj,value);
        static object Get(object obj,string name)=>obj.GetType().GetField(name,Hidden).GetValue(obj);
        static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Hidden).Invoke(obj,args);
        static void Check(bool ok,string message) { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Place(Transform actor,Vector3 point)
        { var cc=actor.GetComponent<CharacterController>(); if(cc) cc.enabled=false; actor.position=point; if(cc) cc.enabled=true; actor.forward=Vector3.forward; Physics.SyncTransforms(); }
        void Reset(float distance=5,bool pair=false)
        {
            game.enabled=false;
            foreach(var h in game.Humans) { h.enabled=false; h.gameObject.SetActive(false); }
            foreach(var z in game.Zombies) { z.enabled=false; z.gameObject.SetActive(false); }
            Set(game,"restrainingMedic",null); Set(game,"<HeroStruggling>k__BackingField",false); Set(game,"restraintInputConsumed",false);
            foreach(var name in new[]{"heroStunUntil","restraintGraceUntil","detainProgress","scanProgress","infection"}) Set(game,name,0f);
            foreach(var name in new[]{"detained","dead","finished","driving"}) Set(game,name,false);
            Set(game,"hp",100f); Set(game,"<Scanned>k__BackingField",false); Set(game,"<PatrolHostile>k__BackingField",false);
            ((List<int>)Get(game,"bag")).Clear();
            soldier=game.SpawnHuman("Capture check soldier",HumanRole.Soldier,new Vector3(20,0,0),material);
            medic=game.SpawnHuman("Capture check medic",HumanRole.Medic,new Vector3(22,0,0),material);
            medic.enabled=pair; medic.gameObject.SetActive(pair);
            game.Soldier=soldier; game.Medic=medic; Place(game.Hero,new Vector3(20,0,distance));
            Set(soldier,"shotAt",Time.time+100); Set(medic,"shotAt",0f);
            InputSystem.QueueStateEvent(Mouse.current,new MouseState()); InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); InputSystem.Update();
        }
        void StartSoldierStruggle()
        { Call(soldier,"Update"); Call(game,"TickHeroRestraint"); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-soldier-capture-check")<0) return;
            Application.runInBackground=true; new GameObject("Soldier capture verification").AddComponent<SoldierCaptureChecks>();
        }
        IEnumerator Start()
        {
            yield return null; yield return null;
            game=EscapeGame.Instance; material=game.Soldier.GetComponentInChildren<Renderer>().sharedMaterial;
            if(Mouse.current==null) InputSystem.AddDevice<Mouse>(); if(Keyboard.current==null) InputSystem.AddDevice<Keyboard>();
            var tests=Run();
            while(true)
            {
                object wait;
                try { if(!tests.MoveNext()) break; wait=tests.Current; }
                catch(Exception ex) { Debug.LogException(ex); Application.Quit(1); yield break; }
                yield return wait;
            }
            Debug.Log("IRKUTSK_SOLDIER_CAPTURE_OK"); Application.Quit(0);
        }
        IEnumerator Run()
        {
            Reset(); float until=Time.time+3; Vector3 start=soldier.transform.position;
            while(!game.HeroStruggling && Time.time<until) { Call(game,"TickExpedition"); yield return null; }
            Check(soldier.transform.position.z>start.z+2 && game.HeroStruggling && !game.Scanned,"solo soldier approaches and binds unscanned awake hero in live frames");
            Check(game.IsBindingHero(soldier) && game.HeroCaptorName=="Солдат","struggle identifies actual soldier captor");
            Check(game.StruggleResistance>.2f && game.StruggleClickPower<.08f,"soldier has stronger resistance and weaker player click than medic");
            Set(game,"strugglePower",.5f); for(int i=0;i<10;i++) Call(game,"AdvanceHeroStruggle",.2f,true);
            float soldierPower=game.EscapePower;
            Check(soldierPower>.5f && soldierPower<.7f,"five clicks per second makes only slow progress against soldier");
            for(int i=0;i<30 && game.HeroStruggling;i++) Call(game,"AdvanceHeroStruggle",.1f,true);
            Check(!game.HeroStruggling && !game.IsOver && game.HeroCaptureImmune,"fast clicking can still defeat soldier and grant escape grace");
            Check(!game.TryBindHero(soldier),"solo soldier respects escape grace");

            Reset(1.3f,true); yield return null;
            Check(soldier.HasCaptureMedic && !game.TryBindHero(soldier),"nearby available medic prevents soldier starting hero capture");
            Place(medic.transform,new Vector3(20.7f,0,.2f)); game.Dart(medic); Check(game.TryBindHero(medic),"medic owns capture while soldier is present");
            Set(game,"heroStunUntil",Time.time-.01f); Call(game,"TickHeroRestraint");
            Check(game.HeroCaptorName=="Врач" && game.StruggleResistance==.2f && game.StruggleClickPower==.08f,"medic keeps original easier struggle difficulty");
            Set(game,"strugglePower",.5f); for(int i=0;i<10;i++) Call(game,"AdvanceHeroStruggle",.2f,true);
            Check(game.EscapePower>soldierPower+.2f,"identical click rhythm produces more escape progress against medic");

            Reset(5,true); until=Time.time+7;
            while(!game.HeroStruggling && !game.IsOver && Time.time<until) { Call(game,"TickExpedition"); yield return null; }
            Check(game.HeroStruggling && game.IsBindingHero(medic) && !game.IsBindingHero(soldier),"paired patrol lets medic scan dart approach and own capture");
            Check(soldier.State=="Прикрываю задержание" && !game.PatrolHostile,"paired soldier covers capture without attacking healthy hero");

            Reset(4,true); Set(game,"<PatrolHostile>k__BackingField",true); game.Dart(medic); soldier.AlertTo(game.Hero);
            var threat=game.Zombies[0]; threat.gameObject.SetActive(true); threat.enabled=false; Place(threat.transform,new Vector3(22,0,3)); game.RegisterThreat(threat.transform); Set(soldier,"shotAt",0f);
            int shots=game.SoldierTracers; Call(soldier,"Update");
            Check(soldier.State=="Прикрываю задержание" && game.SoldierTracers>shots && Vector3.Dot(game.LastSoldierDirection,(ActorPosture.Chest(threat.transform)-ActorPosture.Muzzle(soldier.transform)).normalized)>.95f && (float)Get(game,"hp")==100,"guard soldier fires at another known threat without shooting capture patient");

            Reset(1.3f,true); medic.gameObject.SetActive(false); yield return null; StartSoldierStruggle();
            Check(game.HeroStruggling && game.IsBindingHero(soldier),"soldier takes over when medic is inactive");
            medic.gameObject.SetActive(true); medic.enabled=true; Call(soldier,"Update");
            Check(game.IsBindingHero(soldier) && game.HeroCaptorName=="Солдат" && !game.TryBindHero(medic),"medic arrival does not swap active captor or start a second struggle");

            Reset(1.3f,true); Place(medic.transform,new Vector3(40,0,0)); yield return null; StartSoldierStruggle();
            Check(!soldier.HasCaptureMedic && game.HeroStruggling,"medic beyond twelve metres does not block solo soldier capture");
            Reset(1.3f,true); medic.Stun(3); StartSoldierStruggle();
            Check(!soldier.HasCaptureMedic && game.IsBindingHero(soldier),"stunned doctor is unavailable so soldier captures alone");
            Reset(1.3f,true); Place(medic.transform,new Vector3(18,0,0));
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer=8; wall.transform.position=new Vector3(19,1.5f,0); wall.transform.localScale=new Vector3(.3f,3,6); Physics.SyncTransforms(); StartSoldierStruggle();
            Check(!soldier.HasCaptureMedic && game.IsBindingHero(soldier),"doctor separated by wall is unavailable for capture"); wall.SetActive(false); Destroy(wall);

            Reset(1.3f); yield return null; StartSoldierStruggle(); ((List<int>)Get(game,"bag")).Add(1); until=Time.time+3;
            while(!game.IsOver && Time.time<until) { Call(game,"Update"); yield return null; }
            Check(game.IsOver && ((List<int>)Get(game,"bag")).Count==0,"losing soldier struggle detains hero and confiscates loot");

            foreach(var role in new[]{HumanRole.Civilian,HumanRole.Marauder})
            {
                Reset(); Place(game.Hero,new Vector3(-20,0,-20));
                var target=game.SpawnHuman("Capture check target",role,new Vector3(20,0,1.3f),material); target.enabled=false;
                if(role==HumanRole.Marauder) game.RegisterThreat(target.transform);
                Call(soldier,"Update"); Set(soldier,"bindTime",3f); Call(soldier,"Update");
                Check(target.IsDetained && !game.Checked(target.transform) && target.Health==100,"solo soldier binds unscanned "+role+" without killing");
            }
            Reset(); Place(game.Hero,new Vector3(-20,0,-20)); var infected=game.Zombies[0]; infected.gameObject.SetActive(true); infected.enabled=false; Place(infected.transform,new Vector3(20,0,1.3f)); game.RegisterThreat(infected.transform);
            Call(soldier,"Update"); Set(soldier,"bindTime",3f); Call(soldier,"Update");
            Check(infected.IsDetained && !infected.Downed,"solo soldier binds infected alive without scan or dart");

            Reset(); Place(game.Hero,new Vector3(-20,0,-20)); medic.gameObject.SetActive(true); medic.enabled=true;
            var civilian=game.SpawnHuman("Capture check shared patient",HumanRole.Civilian,new Vector3(20,0,1.3f),material); civilian.enabled=false;
            Call(soldier,"Update"); Set(soldier,"bindTime",3f); Call(soldier,"Update");
            Check(!civilian.IsDetained && soldier.State=="Прикрываю задержание","soldier leaves nearby doctor's unscanned NPC for doctor to bind");

            Reset(9); Call(soldier,"Update");
            Check(!game.IsBindingHero(soldier) && !soldier.CurrentTarget,"solo arrest does not extend soldier vision beyond eight metres");
            Reset(-3); Call(soldier,"Update");
            Check(!game.IsBindingHero(soldier) && !soldier.CurrentTarget,"solo arrest does not discover targets behind soldier");
            Reset(3); wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer=8; wall.transform.position=new Vector3(20,1.5f,1.5f); wall.transform.localScale=new Vector3(6,3,.3f); Physics.SyncTransforms(); Call(soldier,"Update");
            Check(!game.HeroStruggling && !game.TryBindHero(soldier),"walls block solo arrest contact"); wall.SetActive(false); Destroy(wall);
        }
    }
}
