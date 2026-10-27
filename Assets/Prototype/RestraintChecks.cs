using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace IrkutskPrototype
{
    public sealed class RestraintChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        EscapeGame game; HumanAgent medic; Material material;
        static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,Hidden).SetValue(obj,value);
        static object Get(object obj,string field)=>obj.GetType().GetField(field,Hidden).GetValue(obj);
        static object Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,Hidden).Invoke(obj,args);
        static void Check(bool ok,string message) { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Place(Transform actor,Vector3 point)
        { var cc=actor.GetComponent<CharacterController>(); if(cc) cc.enabled=false; actor.position=point; if(cc) cc.enabled=true; Physics.SyncTransforms(); }
        void Reset(float distance=1.3f)
        {
            game.enabled=false;
            foreach(var h in game.Humans) { h.enabled=false; h.gameObject.SetActive(false); }
            foreach(var z in game.Zombies) { z.enabled=false; z.gameObject.SetActive(false); }
            Set(game,"restrainingMedic",null); Set(game,"<HeroStruggling>k__BackingField",false);
            foreach(var field in new[]{"heroStunUntil","restraintGraceUntil","detainProgress","scanProgress","infection"}) Set(game,field,0f);
            Set(game,"hp",100f); Set(game,"detained",false); Set(game,"dead",false); Set(game,"finished",false); Set(game,"driving",false);
            Set(game,"<Scanned>k__BackingField",false); Set(game,"<PatrolHostile>k__BackingField",true);
            Place(game.Hero,new Vector3(20,0,3));
            medic=game.SpawnHuman("Restraint check medic",HumanRole.Medic,new Vector3(20,0,3-distance),material);
            game.Medic=medic; medic.transform.forward=Vector3.forward; Set(medic,"shotAt",Time.time+100);
            ((List<int>)Get(game,"bag")).Clear();
            InputSystem.QueueStateEvent(Mouse.current,new MouseState());
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); InputSystem.Update();
        }
        void BeginStruggle()
        {
            game.Dart(medic); Check(game.TryBindHero(medic),"nearby medic starts binding after dart");
            Set(game,"heroStunUntil",Time.time-.01f); Call(game,"TickHeroRestraint");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-restraint-check")<0) return;
            Application.runInBackground=true; new GameObject("Restraint QTE verification").AddComponent<RestraintChecks>();
        }
        IEnumerator Start()
        {
            yield return null; yield return null;
            game=EscapeGame.Instance; material=game.Medic.GetComponentInChildren<Renderer>().sharedMaterial;
            if(Mouse.current==null) InputSystem.AddDevice<Mouse>(); if(Keyboard.current==null) InputSystem.AddDevice<Keyboard>();
            var tests=Run();
            while(true)
            {
                object wait;
                try { if(!tests.MoveNext()) break; wait=tests.Current; }
                catch(Exception ex) { Debug.LogException(ex); Application.Quit(1); yield break; }
                yield return wait;
            }
            Debug.Log("IRKUTSK_RESTRAINT_OK"); Application.Quit(0);
        }
        IEnumerator Run()
        {
            Reset(3); yield return null;
            for(int i=0;i<8 && (float)Get(game,"heroStunUntil")<=0;i++) game.NpcShot(medic,game.Hero);
            Check((float)Get(game,"heroStunUntil")>Time.time && Get(medic,"dartPatient") as Transform==game.Hero,"physical medic dart assigns hero as restraint patient without HP damage");
            Check((float)Get(game,"hp")==100,"tranquilizer remains non lethal");
            float original=medic.transform.position.z,until=Time.time+3;
            while(!game.HeroStruggling && Time.time<until) { Call(game,"TickExpedition"); yield return null; }
            Check(medic.transform.position.z>original+.5f && game.HeroStruggling,"hostile medic closes after dart and starts struggle when sedation expires");
            Check(!game.IsOver && game.EscapePower>0 && game.EscapePower<1,"sedation expiry starts contest instead of immediate detention");

            Vector3 heroPosition=game.Hero.position; int ammo=(int)Get(game,"ammo"); float attack=(float)Get(game,"attackAt"); float power=game.EscapePower;
            InputSystem.QueueStateEvent(Mouse.current,new MouseState().WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W,Key.Q)); InputSystem.Update(); Call(game,"Update");
            Check(game.EscapePower>power,"left mouse press increases escape power through game input");
            Check(game.Hero.position==heroPosition && (int)Get(game,"ammo")==ammo && (float)Get(game,"attackAt")==attack,"struggle blocks walking shooting and punching while consuming left clicks");
            float afterPress=game.EscapePower; yield return null;
            InputSystem.QueueStateEvent(Mouse.current,new MouseState().WithButton(MouseButton.Left)); InputSystem.Update(); Call(game,"Update");
            Check(game.EscapePower<=afterPress,"holding left button does not generate repeated escape clicks");
            InputSystem.QueueStateEvent(Mouse.current,new MouseState()); InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); InputSystem.Update();
            for(int i=0;i<20 && game.HeroStruggling;i++) Call(game,"AdvanceHeroStruggle",.2f,true);
            Check(!game.HeroStruggling && !game.IsOver && game.HeroCaptureImmune,"sufficient clicking wins contest and grants escape grace");
            Check(!game.TryBindHero(medic),"escape grace prevents immediate repeat binding");
            game.Dart(medic); Check((float)Get(game,"heroStunUntil")==0,"escape grace also prevents immediate tranquilizer chain");
            Set(game,"restraintGraceUntil",Time.time-.1f); game.Dart(medic);
            Check((float)Get(game,"heroStunUntil")>Time.time,"tranquilizer works again after escape grace");

            Reset(); yield return null; BeginStruggle(); Set(game,"strugglePower",.98f); attack=(float)Get(game,"attackAt");
            InputSystem.QueueStateEvent(Mouse.current,new MouseState().WithButton(MouseButton.Left)); InputSystem.Update(); Call(game,"Update");
            Check(!game.HeroStruggling && !game.IsOver && (float)Get(game,"attackAt")==attack,"winning click is consumed without punching doctor on release frame");
            InputSystem.QueueStateEvent(Mouse.current,new MouseState()); InputSystem.Update();

            Reset(); yield return null; BeginStruggle(); ((List<int>)Get(game,"bag")).Add(1);
            until=Time.time+4;
            while(!game.IsOver && Time.time<until) { Call(game,"Update"); yield return null; }
            Check(game.IsOver && !game.HeroStruggling && ((List<int>)Get(game,"bag")).Count==0,"no clicks loses in live frames and confiscates loot");

            Reset(); yield return null; BeginStruggle(); Set(game,"strugglePower",.7f); Set(game,"struggleUntil",Time.time-.1f); Call(game,"AdvanceHeroStruggle",0f,false);
            Check(!game.IsOver && !game.HeroStruggling,"positive player advantage wins when contest deadline expires");
            Reset(); yield return null; BeginStruggle(); Set(game,"strugglePower",.3f); Set(game,"struggleUntil",Time.time-.1f); Call(game,"AdvanceHeroStruggle",0f,false);
            Check(game.IsOver,"doctor advantage at deadline completes detention");

            Reset(); yield return null; BeginStruggle(); medic.Stun(1); Call(game,"TickHeroRestraint");
            Check(!game.HeroStruggling && !game.IsOver,"stunning captor interrupts the contest");
            Reset(); yield return null; BeginStruggle(); medic.Hit(200); Call(game,"TickHeroRestraint");
            Check(!game.HeroStruggling && !game.IsOver,"incapacitated captor cannot continue binding");
            Reset(); yield return null; BeginStruggle(); Place(medic.transform,new Vector3(20,0,-2)); Call(game,"TickHeroRestraint");
            Check(!game.HeroStruggling && !game.IsOver,"loss of contact cancels struggle without remote detention");
            Reset(); yield return null; BeginStruggle();
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer=8; wall.transform.position=new Vector3(20,1.5f,2.35f); wall.transform.localScale=new Vector3(3,3,.2f); Physics.SyncTransforms(); Call(game,"TickHeroRestraint");
            Check(!game.HeroStruggling && !game.IsOver,"intervening wall stops a remote binding"); wall.SetActive(false); Destroy(wall);

            Reset(); yield return null; game.Dart(medic); Check(game.TryBindHero(medic),"medic can bind still sedated hero"); Set(game,"heroStunUntil",Time.time+5); Set(game,"detainProgress",3f); Call(game,"TickHeroRestraint");
            Check(game.IsOver && !game.HeroStruggling,"binding completed before sedation expires requires no contest");

            Reset(); Place(game.Hero,new Vector3(-20,0,-20));
            var marauder=game.SpawnHuman("Restraint check marauder",HumanRole.Marauder,new Vector3(20,0,3),material); marauder.enabled=false; medic.enabled=false; yield return null; medic.enabled=true; Physics.SyncTransforms();
            for(int i=0;i<8 && !marauder.IsStunned;i++) game.NpcShot(medic,marauder.transform);
            Check(marauder.IsStunned && Get(medic,"dartPatient") as Transform==marauder.transform,"actual dart hit on marauder assigns capture rather than continued ranged combat: stun="+marauder.IsStunned+" patient="+(Get(medic,"dartPatient") as Transform)+" doctor="+medic.transform.position+" target="+marauder.transform.position+" cc="+marauder.GetComponent<CharacterController>().enabled);
            Set(medic,"bindTime",3f); Call(medic,"Update");
            Check(marauder.IsDetained && !game.Alive(marauder.transform),"medic binds hostile human after dart");

            Reset(); Place(game.Hero,new Vector3(-20,0,-20)); var infected=game.Zombies[0]; infected.gameObject.SetActive(true); infected.enabled=false; Place(infected.transform,new Vector3(20,0,3)); medic.enabled=false; yield return null; medic.enabled=true; Physics.SyncTransforms();
            for(int i=0;i<8 && Get(medic,"dartPatient") as Transform!=infected.transform;i++) game.NpcShot(medic,infected.transform);
            Check(Get(medic,"dartPatient") as Transform==infected.transform,"actual dart hit on infected assigns restraint task");
            int rep=game.Reputation; Set(medic,"bindTime",3f); Call(medic,"Update");
            Check(infected.IsDetained && !infected.Downed && !game.Alive(infected.transform) && game.Reputation==rep,"infected can be bound alive without kill penalty");
            Vector3 bound=infected.transform.position; infected.Behavior.Tick();
            Check(infected.transform.position==bound && !infected.CurrentTarget,"bound infected cannot chase or attack");

            Reset(5); yield return null; game.Dart(medic); wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer=8; wall.transform.position=new Vector3(20,1.5f,0); wall.transform.localScale=new Vector3(6,3,.3f); Physics.SyncTransforms();
            Vector3 start=medic.transform.position; Call(medic,"Update");
            Check(!game.TryBindHero(medic) && medic.State!="Связываю героя" && !game.HeroStruggling,"unseen distant patient cannot be bound through a wall");
            wall.SetActive(false); Destroy(wall);
        }
    }
}
