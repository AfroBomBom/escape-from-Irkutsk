using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace IrkutskPrototype
{
    public sealed class PlayerNoiseChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        EscapeGame game; HumanAgent soldier; Infected infected;
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Hidden).SetValue(o,value);
        static object Get(object o,string name)=>o.GetType().GetField(name,Hidden).GetValue(o);
        static void Call(object o,string name)=>o.GetType().GetMethod(name,Hidden).Invoke(o,null);
        static void Check(bool ok,string message) { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Place(Transform actor,Vector3 p)
        { var cc=actor.GetComponent<CharacterController>(); if(cc) cc.enabled=false; actor.position=p; if(cc) cc.enabled=true; actor.forward=Vector3.forward; Physics.SyncTransforms(); }
        void Clear()
        {
            ((IList)Get(game,"playerNoisePulses")).Clear(); Call(game,"RefreshPlayerNoiseIndicator");
            Call(soldier,"ClearSoundClue"); Set(soldier,"noiseAcceptedAt",-10f); Set(soldier,"checkedSoundUntil",0f);
            Set(infected,"suspicion",0f); Set(infected,"lastHeard",-10f); Set(infected,"investigateAt",0f);
            Set(game,"footAt",0f); InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); InputSystem.Update();
        }
        void Keys(params Key[] keys)
        { InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys)); InputSystem.Update(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-player-noise-check")<0) return;
            Application.runInBackground=true; new GameObject("Player noise verification").AddComponent<PlayerNoiseChecks>();
        }
        IEnumerator Start()
        {
            yield return null; yield return null;
            if(Keyboard.current==null) InputSystem.AddDevice<Keyboard>();
            if(Mouse.current==null) InputSystem.AddDevice<Mouse>();
            var tests=Run(EscapeGame.Instance);
            while(true)
            {
                object wait;
                try { if(!tests.MoveNext()) break; wait=tests.Current; }
                catch(Exception ex) { Debug.LogException(ex); Application.Quit(1); yield break; }
                yield return wait;
            }
            Debug.Log("IRKUTSK_PLAYER_NOISE_OK"); Application.Quit(0);
        }
        IEnumerator Run(EscapeGame instance)
        {
            game=instance; game.enabled=false;
            var material=game.Soldier.GetComponentInChildren<Renderer>().sharedMaterial;
            foreach(var h in game.Humans) { h.enabled=false; h.gameObject.SetActive(false); }
            foreach(var z in game.Zombies) { z.enabled=false; z.gameObject.SetActive(false); }
            soldier=game.SpawnHuman("Noise check soldier",HumanRole.Soldier,new Vector3(20,0,13),material);
            var civilian=game.SpawnHuman("Noise check civilian",HumanRole.Civilian,new Vector3(21,0,12),material);
            infected=game.Zombies[0]; infected.gameObject.SetActive(true); infected.enabled=false;
            Place(game.Hero,new Vector3(20,0,0)); Place(infected.transform,new Vector3(20,0,12)); Clear();
            game.Noise(game.Hero.position,14,game.Hero,NoiseKind.Gunshot);
            Check(game.PlayerNoiseRadius==14 && soldier.HasSoundClue && (float)Get(infected,"suspicion")>=1,"same emitted gunshot radius reaches soldier and infected at twelve to thirteen metres");
            Check(!game.PatrolHostile && !soldier.CurrentTarget,"hearing a shot does not identify or automatically make shooter hostile");
            Check((float)Get(civilian,"fearUntil")>Time.time,"civilian also reacts to a player gunshot within its emitted radius");
            var line=(LineRenderer)Get(game,"playerNoiseCircle");
            Check(line.enabled && line.positionCount==64 && Mathf.Abs(Vector3.Distance(line.GetPosition(0),game.Hero.position+Vector3.up*.12f)-14)<.01f,"visible world ring matches active radius around controlled character");
            Check(line.sharedMaterial.shader.name=="Irkutsk/NoiseRing","release build includes dedicated noise overlay shader");
            Check(!line.GetComponent<Collider>(),"noise indicator has no physical collider");

            Clear(); Place(soldier.transform,new Vector3(20,0,15)); Place(infected.transform,new Vector3(20,0,15));
            game.Noise(game.Hero.position,14,game.Hero,NoiseKind.Gunshot);
            Check(!soldier.HasSoundClue && (float)Get(infected,"suspicion")==0,"both listener types ignore hero noise outside its circle");
            Place(soldier.transform,new Vector3(20,0,12)); Place(infected.transform,new Vector3(20,0,12)); Call(game,"RefreshPlayerNoiseIndicator");
            Check(soldier.HasSoundClue && (float)Get(infected,"suspicion")>=1,"entering the active circle delivers the sound to new listeners");

            Clear(); Place(soldier.transform,new Vector3(20,0,6)); Place(infected.transform,new Vector3(20,0,6));
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer=8; wall.transform.position=new Vector3(20,1.5f,3); wall.transform.localScale=new Vector3(8,3,.3f); Physics.SyncTransforms();
            game.Noise(game.Hero.position,14,game.Hero,NoiseKind.Gunshot);
            Check(!soldier.HasSoundClue && (float)Get(infected,"suspicion")==0,"wall attenuates player sound to forty percent equally for both listener types");
            wall.SetActive(false); Destroy(wall); Clear();
            game.Noise(game.Hero.position,14,game.Hero,NoiseKind.Gunshot);
            Check(soldier.HasSoundClue && (float)Get(infected,"suspicion")>=1,"removing wall restores open circle reach");

            Clear(); Place(infected.transform,new Vector3(20,0,1)); Place(soldier.transform,new Vector3(25,0,0));
            game.Noise(game.Hero.position,2.5f,game.Hero,NoiseKind.Movement); float suspicion=(float)Get(infected,"suspicion");
            for(int i=0;i<5;i++) Call(game,"RefreshPlayerNoiseIndicator");
            Check(Mathf.Abs(suspicion-.55f)<.001f && (float)Get(infected,"suspicion")==suspicion && (float)Get(infected,"investigateAt")==0,"one quiet pulse is delivered once and only raises suspicion");
            game.Noise(game.Hero.position,2.5f,game.Hero,NoiseKind.Movement);
            Check((float)Get(infected,"suspicion")>1 && (float)Get(infected,"investigateAt")>Time.time,"repeated steps trigger delayed infected investigation");

            Clear(); Place(infected.transform,new Vector3(20,0,12));
            game.Noise(game.Hero.position,14,soldier.transform,NoiseKind.Gunshot);
            Check((float)Get(infected,"suspicion")==0 && game.PlayerNoiseRadius==0,"NPC sound does not create hero circle and keeps its separate infected hearing rule");

            Clear(); Place(game.Hero,new Vector3(20,0,-10)); Keys(Key.W,Key.LeftCtrl); Call(game,"Walk");
            Check(game.PlayerNoiseRadius==0 && !line.enabled,"quiet Ctrl movement creates no footstep noise or ring");
            Clear(); Keys(Key.W); Call(game,"Walk");
            Check(game.PlayerNoiseRadius==2.5f && line.enabled,"ordinary walking emits a 2.5 metre ring");
            Clear(); Keys(Key.W,Key.LeftShift); Call(game,"Walk");
            Check(game.PlayerNoiseRadius==6 && line.enabled,"sprinting emits a 6 metre ring");
            Clear(); game.Hero.GetComponent<ActorPosture>().TrySetCrouched(true); Keys(Key.W); Call(game,"Walk");
            Check(game.PlayerNoiseRadius==1.25f,"crouched walking emits a 1.25 metre ring");
            game.Hero.GetComponent<ActorPosture>().TrySetCrouched(false);

            Clear(); game.Noise(new Vector3(-999,0,-999),8,game.Hero,NoiseKind.Work);
            Check(game.PlayerNoiseRadius==8 && Vector3.Distance(line.GetPosition(0),game.Hero.position+Vector3.up*.12f)>7.9f,"work sound originates at hero even when action supplied another position");
            Place(game.Hero,new Vector3(22,0,-10)); Call(game,"RefreshPlayerNoiseIndicator");
            Check(Mathf.Abs(Vector3.Distance(line.GetPosition(0),game.Hero.position+Vector3.up*.12f)-8)<.01f,"circle follows hero while pulse remains active");
            Clear(); Set(game,"driving",true); game.Car.position=new Vector3(40,0,0); Physics.SyncTransforms();
            game.Noise(game.Car.position,10,game.Car,NoiseKind.Engine);
            Check(game.PlayerNoiseRadius==10 && Mathf.Abs(Vector3.Distance(line.GetPosition(0),game.Car.position+Vector3.up*.12f)-10)<.01f,"engine noise is emitted around controlled vehicle rather than parked hero");
            Set(game,"driving",false);
            Clear(); Set(game,"ammo",6); Set(game,"shotAt",0f); Set(game,"reloadAt",0f); Call(game,"Shoot");
            game.Noise(game.Hero.position,2.5f,game.Hero,NoiseKind.Movement);
            Check(game.PlayerNoiseRadius==14 && line.enabled,"real gunshot dominates overlapping quieter footsteps");
            yield return new WaitForSeconds(1.4f); Call(game,"RefreshPlayerNoiseIndicator");
            Check(game.PlayerNoiseRadius==0 && !line.enabled,"expired sounds remove the circle instead of leaving a permanent range");
        }
    }
}
