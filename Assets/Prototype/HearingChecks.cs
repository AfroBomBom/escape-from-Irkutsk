using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace IrkutskPrototype
{
    public sealed class HearingChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Hidden).SetValue(o,value);
        static object Get(object o,string name)=>o.GetType().GetField(name,Hidden).GetValue(o);
        static void Call(object o,string name)=>o.GetType().GetMethod(name,Hidden).Invoke(o,null);
        static void Check(bool ok,string text)
        { if(!ok) throw new Exception("CHECK FAILED: "+text); Debug.Log("PASS: "+text); }
        static void Place(Transform actor,Vector3 p)
        { var cc=actor.GetComponent<CharacterController>(); cc.enabled=false; actor.position=p; cc.enabled=true; actor.rotation=Quaternion.identity; Physics.SyncTransforms(); }
        static void Isolate(EscapeGame game)
        {
            foreach(var h in game.Humans) { h.enabled=false; h.gameObject.SetActive(false); }
            foreach(var z in game.Zombies) { z.enabled=false; z.gameObject.SetActive(false); }
            Place(game.Hero,new Vector3(-28,0,-22));
            Set(game,"<PatrolHostile>k__BackingField",false);
        }
        static void Listener(HumanAgent h,Vector3 p)
        {
            h.gameObject.SetActive(true); h.enabled=true; Place(h.transform,p);
            Set(h,"<CurrentTarget>k__BackingField",null); Set(h,"targetVisible",false); Set(h,"searchUntil",0f);
            Call(h,"ClearSoundClue"); Set(h,"noiseAcceptedAt",-10f); Set(h,"checkedSoundUntil",0f);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-hearing-check")<0) return;
            Application.runInBackground=true; new GameObject("Stage one hearing checks").AddComponent<HearingChecks>();
        }
        IEnumerator Start()
        {
            yield return null; yield return null;
            var tests=Run(EscapeGame.Instance);
            while(true)
            {
                object wait;
                try { if(!tests.MoveNext()) break; wait=tests.Current; }
                catch(Exception e) { Debug.LogException(e); Application.Quit(1); yield break; }
                yield return wait;
            }
            Debug.Log("IRKUTSK_HEARING_OK"); Application.Quit(0);
        }
        IEnumerator Run(EscapeGame game)
        {
            game.enabled=false; Isolate(game);
            var soldier=game.Soldier; var medic=game.Medic;
            var marauder=game.Humans.Find(h=>h.Role==HumanRole.Marauder);
            Listener(soldier,new Vector3(20,0,0));
            Vector3 noise=new Vector3(20.7f,0,-7.3f);
            Place(game.Hero,noise); game.Noise(noise,14,game.Hero,NoiseKind.Gunshot);
            Vector3 clue=soldier.LastHeardPosition, start=soldier.transform.position;
            Check(soldier.HasSoundClue && !soldier.CurrentTarget && !game.PatrolHostile,"rear gunshot creates a place to investigate without identifying or condemning hero");
            Check(Vector3.Distance(clue,noise)>.1f && Vector3.Distance(clue,noise)<2,"sound position is approximate rather than exact");
            Place(game.Hero,new Vector3(-28,0,-22));
            yield return new WaitForSeconds(.15f);
            Check(Vector3.Distance(start,soldier.transform.position)<.1f,"listener has a reaction delay before moving toward sound");
            yield return new WaitForSeconds(1.1f);
            Check(Vector3.Distance(start,soldier.transform.position)>.5f && !soldier.CurrentTarget,"soldier checks a rear sound without seeing its source");
            Check(soldier.LastHeardPosition==clue && !game.PatrolHostile,"moving unseen source does not update remembered sound position or hostility");
            Set(soldier,"soundUntil",Time.time-.01f); Set(soldier,"heardAt",Time.time-9);
            yield return new WaitForSeconds(.1f);
            Check(!soldier.HasSoundClue && soldier.State=="Обход","expired unconfirmed sound returns soldier to patrol");

            foreach(var listener in new[]{medic,marauder})
            {
                Isolate(game); Listener(listener,new Vector3(20,0,0)); start=listener.transform.position;
                game.Noise(new Vector3(20,0,-4),8,null,NoiseKind.Work);
                yield return new WaitForSeconds(.95f);
                Check(listener.HasSoundClue && !listener.CurrentTarget && Vector3.Distance(start,listener.transform.position)>.3f,listener.Role+" investigates loud work heard behind them");
            }

            Isolate(game); Listener(soldier,new Vector3(20,0,0)); start=soldier.transform.position;
            game.Noise(new Vector3(20,0,-2),2.5f,null,NoiseKind.Movement);
            yield return new WaitForSeconds(.1f);
            Check(!soldier.HasSoundClue && soldier.State=="Прислушиваюсь" && Vector3.Distance(start,soldier.transform.position)<.1f,"single quiet step causes listening without immediate investigation");
            yield return new WaitForSeconds(.2f);
            game.Noise(new Vector3(20,0,-2),2.5f,null,NoiseKind.Movement);
            Check(soldier.HasSoundClue,"repeated nearby steps build enough suspicion to investigate");
            Listener(soldier,new Vector3(20,0,0));
            game.Noise(new Vector3(20,0,-2),2.5f,null,NoiseKind.Movement);
            game.Noise(new Vector3(20,0,-2),14,null,NoiseKind.Gunshot);
            Check(soldier.HasSoundClue,"gunshot is not throttled by a quiet step emitted in the same frame");

            Isolate(game); Listener(soldier,new Vector3(20,0,0)); Place(game.Hero,new Vector3(20,0,-2));
            if(Keyboard.current==null) InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W,Key.LeftCtrl)); InputSystem.Update();
            Call(game,"Walk");
            Check(!soldier.HasSoundClue && (float)Get(soldier,"heardAt")<0,"Ctrl silent movement emits no noise for human listeners");
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); InputSystem.Update();

            Isolate(game); Listener(soldier,new Vector3(20,0,0));
            game.Noise(new Vector3(20,0,-15),14,null,NoiseKind.Gunshot);
            Check(!soldier.HasSoundClue,"gunshot outside fourteen metre hearing range is ignored");
            Listener(soldier,new Vector3(-4,0,-4));
            game.Noise(new Vector3(-4,0,0),6,null,NoiseKind.Work);
            Check(!soldier.HasSoundClue,"crate attenuates moderate noise beyond audible range");
            game.Noise(new Vector3(-4,0,0),14,null,NoiseKind.Gunshot);
            Check(soldier.HasSoundClue && !soldier.CurrentTarget,"nearby loud shot is audible through cover without granting visual target");

            Isolate(game); Listener(soldier,new Vector3(20,0,0));
            game.Noise(soldier.transform.position,12,soldier.transform,NoiseKind.Gunshot);
            game.Noise(new Vector3(20,0,-2),12,medic.transform,NoiseKind.Gunshot);
            Check(!soldier.HasSoundClue,"patrol ignores own and partner weapon sounds as suspicious clues");
            game.Noise(new Vector3(20,0,-2),12,marauder.transform,NoiseKind.Gunshot);
            Check(soldier.HasSoundClue && !soldier.CurrentTarget,"unseen enemy gunfire supplies only a sound clue");

            Isolate(game); Listener(soldier,new Vector3(20,0,0)); noise=new Vector3(20,0,-2);
            game.Noise(noise,8,null,NoiseKind.Work);
            yield return new WaitForSeconds(1.2f);
            Check(soldier.State=="Осматриваю источник шума","listener reaches approximate source and inspects the area");
            yield return new WaitForSeconds(2.1f);
            Check(!soldier.HasSoundClue && soldier.State=="Обход","completed source inspection resumes patrol");
            game.Noise(noise,8,null,NoiseKind.Work);
            Check(!soldier.HasSoundClue,"recently checked place is not immediately checked again for ordinary noise");
            game.Noise(noise,14,null,NoiseKind.Gunshot);
            Check(soldier.HasSoundClue,"fresh gunfire overrides cooldown of an already checked place");

            Isolate(game); Listener(soldier,new Vector3(20,0,0)); Listener(medic,new Vector3(22,0,0));
            medic.transform.rotation=Quaternion.Euler(0,180,0); Place(game.Hero,new Vector3(20,0,5));
            Vector3 seenPosition=game.Hero.position;
            Call(soldier,"Update");
            Check(medic.CurrentTarget==game.Hero && Vector3.Distance((Vector3)Get(medic,"lastSeen"),seenPosition)<.2f,"patrol shares visually observed position with partner facing away");
            Place(game.Hero,new Vector3(-28,0,-22));
            yield return new WaitForSeconds(.15f);
            Check(medic.CurrentTarget==game.Hero && Vector3.Distance((Vector3)Get(medic,"lastSeen"),seenPosition)<.2f,"radio report remains a snapshot after hidden target moves");
            soldier.gameObject.SetActive(false); Set(medic,"searchUntil",Time.time-.01f);
            yield return new WaitForSeconds(.1f);
            Check(!medic.CurrentTarget && medic.State=="Обход","unconfirmed reported target is forgotten after search expires");
            Isolate(game); Listener(soldier,new Vector3(20,0,0)); Listener(medic,new Vector3(-10,0,-10));
            Place(game.Hero,new Vector3(20,0,5)); Call(soldier,"Update");
            Check(!medic.CurrentTarget,"sighting report is not delivered beyond local twelve metre range");

            Isolate(game); Listener(medic,new Vector3(20,0,0));
            game.Citizen.gameObject.SetActive(true); game.Citizen.enabled=false; Place(game.Citizen.transform,new Vector3(20,0,2));
            Call(medic,"Update"); Set(medic,"scanTime",1f);
            medic.ReceiveReport(game.Hero,new Vector3(20,0,-4));
            Check(medic.CurrentTarget==game.Citizen.transform && (float)Get(medic,"scanTime")==1,"neutral radio report does not reset an ongoing visual scan");
            game.RegisterThreat(game.Hero); medic.ReceiveReport(game.Hero,new Vector3(20,0,-4));
            Check(medic.CurrentTarget==game.Hero && (float)Get(medic,"scanTime")==0,"confirmed hostile report can interrupt a peaceful scan");

            Isolate(game); Listener(soldier,new Vector3(20,0,0)); Place(game.Hero,new Vector3(20,0,5));
            Set(game,"<PatrolHostile>k__BackingField",true); soldier.AlertTo(game.Hero); Call(soldier,"Update");
            game.Noise(new Vector3(20,0,-3),8,null,NoiseKind.Work);
            Check(soldier.CurrentTarget==game.Hero && !soldier.HasSoundClue,"irrelevant sound does not interrupt a visible hostile engagement");
        }
    }
}
