using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace IrkutskPrototype
{
    public sealed class PatrolTacticsChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Hidden).SetValue(o,value);
        static object Call(object o,string name)=>o.GetType().GetMethod(name,Hidden).Invoke(o,null);
        static void Check(bool ok,string message)
        { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Place(Transform actor,Vector3 p)
        { var cc=actor.GetComponent<CharacterController>(); cc.enabled=false; actor.position=p; cc.enabled=true; actor.rotation=Quaternion.identity; Physics.SyncTransforms(); }
        static void Reset(EscapeGame game)
        {
            foreach(var z in game.Zombies) { z.enabled=false; z.gameObject.SetActive(false); }
            foreach(var h in game.Humans)
            {
                h.enabled=false; h.gameObject.SetActive(false);
                Set(h,"<CurrentTarget>k__BackingField",null); Set(h,"<Health>k__BackingField",100f);
                Set(h,"<TacticalTask>k__BackingField",PatrolTask.None); Set(h,"taskUntil",0f); Set(h,"taskFrame",-1);
                Set(h,"shotAt",Time.time+100); Set(h,"healAt",0f); Set(h,"stunnedUntil",0f); Set(h,"lastDamageAt",-1000f);
                Set(h,"coverSeekAt",0f); Set(h,"tacticsAt",0f);
            }
            Place(game.Hero,new Vector3(-28,0,-22));
            Set(game,"hp",100f); Set(game,"<PatrolHostile>k__BackingField",true);
        }
        static void Human(HumanAgent h,Vector3 p)
        { h.gameObject.SetActive(true); Place(h.transform,p); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-patrol-tactics-check")<0) return;
            Application.runInBackground=true; new GameObject("Stage three patrol tactics verification").AddComponent<PatrolTacticsChecks>();
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
            Debug.Log("IRKUTSK_PATROL_TACTICS_OK"); Application.Quit(0);
        }
        IEnumerator Run(EscapeGame game)
        {
            game.enabled=false; Reset(game);
            var soldier=game.Soldier; var medic=game.Medic;
            Human(soldier,new Vector3(20,0,0)); Place(game.Hero,new Vector3(20,0,5));
            Call(soldier,"Update");
            Check(soldier.TacticalTask==PatrolTask.Hold,"soldier holds an effective firing distance");
            float hold=soldier.TaskHoldRemaining;
            yield return new WaitForSeconds(.12f); Call(soldier,"Update");
            Check(soldier.TaskHoldRemaining<hold && soldier.TacticalTask==PatrolTask.Hold,"decision hold counts down instead of restarting each frame");
            Place(game.Hero,new Vector3(20,0,1.5f)); Call(soldier,"Update");
            Check(soldier.TacticalTask==PatrolTask.Retreat,"close threat immediately interrupts a held task");
            soldier.enabled=true; float distance=Vector3.Distance(soldier.transform.position,game.Hero.position);
            yield return new WaitForSeconds(.5f);
            Check(Vector3.Distance(soldier.transform.position,game.Hero.position)>distance+.5f,"emergency withdrawal actually increases distance");

            Reset(game); Human(soldier,new Vector3(20,0,0)); Human(medic,new Vector3(22,0,0));
            soldier.enabled=true; medic.enabled=true; Place(game.Hero,new Vector3(20,0,7.5f));
            Call(soldier,"Update"); Call(medic,"Update");
            Check(soldier.TacticalTask==PatrolTask.Advance && medic.TacticalTask==PatrolTask.Hold,"one patrol member advances while partner holds");
            Vector3 first=soldier.transform.position; bool concurrent=false;
            for(float until=Time.time+2.5f;Time.time<until;)
            {
                yield return null;
                if((bool)typeof(HumanAgent).GetProperty("ChangingPosition",Hidden).GetValue(soldier)
                    && (bool)typeof(HumanAgent).GetProperty("ChangingPosition",Hidden).GetValue(medic)) concurrent=true;
            }
            Check(!concurrent,"pair never chooses two simultaneous non-emergency movements");
            Check(Vector3.Distance(first,soldier.transform.position)>.3f,"coordinated advance makes progress");
            medic.enabled=false; soldier.enabled=false;
            Place(game.Hero,new Vector3(-28,0,-22)); Call(soldier,"Update");
            Check(soldier.State=="Поиск" && soldier.TacticalTask==PatrolTask.None,"loss of sight switches to snapshot search rather than tactical live tracking");

            Reset(game); Human(soldier,new Vector3(23,0,0)); Human(medic,new Vector3(20,0,0));
            Set(soldier,"<Health>k__BackingField",40f); Place(game.Hero,new Vector3(20,0,6));
            Call(medic,"Update");
            Check(!(bool)Call(medic,"NeedsSupport") && soldier.Health==40,"medic refuses exposed treatment near a known enemy");
            // Expire combat memory explicitly, then test ordinary movement/healing in real frames.
            Place(game.Hero,new Vector3(-28,0,-22)); Set(medic,"<CurrentTarget>k__BackingField",null);
            Set(soldier,"<CurrentTarget>k__BackingField",null); Set(medic,"searchUntil",0f);
            Check((bool)Call(medic,"NeedsSupport"),"medic can assist after danger memory clears");
            medic.enabled=true;
            yield return new WaitForSeconds(.8f);
            Check(soldier.Health>40,"safe nearby treatment restores partner health");

            Reset(game); Human(soldier,new Vector3(20,0,2)); Human(medic,new Vector3(20,0,0));
            Set(soldier,"<Health>k__BackingField",40f);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer=8;
            wall.transform.position=new Vector3(20,1,1); wall.transform.localScale=new Vector3(2,2,.4f); Physics.SyncTransforms();
            Call(medic,"TrySafeHealing");
            Check(soldier.Health==40,"healing cannot pass through a wall");
            Check(!(bool)Call(medic,"NeedsSupport"),"medic refuses an obstructed support path");
            Destroy(wall); yield return null;
            Set(medic,"lastDamageAt",Time.time); Call(medic,"TrySafeHealing");
            Check(soldier.Health==40,"recent damage interrupts treatment even without a currently visible attacker");
            yield return new WaitForSeconds(1.3f); Call(medic,"TrySafeHealing");
            yield return new WaitForSeconds(.25f); // Healing now arrives on projectile impact.
            Check(soldier.Health==55,"treatment resumes after the damage safety interval");

            Reset(game); Human(soldier,new Vector3(20,0,0)); Human(medic,new Vector3(26.5f,0,0));
            Set(soldier,"<Health>k__BackingField",40f); medic.enabled=true; soldier.enabled=true;
            Place(game.Hero,new Vector3(20,0,7.8f)); Call(medic,"Update"); Call(soldier,"Update");
            Check(medic.TacticalTask==PatrolTask.Support && soldier.TacticalTask==PatrolTask.Hold,"soldier guards while medic approaches in a safe gap");
            Place(game.Hero,medic.transform.position+Vector3.forward*2); Call(medic,"Update");
            Check(medic.TacticalTask==PatrolTask.Retreat,"new close danger interrupts support immediately");

            Reset(game); Human(soldier,new Vector3(20,0,-.97f)); soldier.enabled=true;
            Place(game.Hero,new Vector3(20,0,5));
            var cover=Instantiate(Resources.Load<GameObject>("Cover/LowCover"),new Vector3(20,0,0),Quaternion.identity);
            Physics.SyncTransforms(); Call(soldier,"Update");
            Check(soldier.SelectedCover && soldier.TacticalTask==PatrolTask.Cover,"patrol tactics retain registered cover use");
            Place(game.Hero,new Vector3(20,0,-3)); soldier.transform.forward=Vector3.back; Call(soldier,"Update");
            Check(!soldier.SelectedCover && soldier.TacticalTask==PatrolTask.Retreat,"threat behind cover forces immediate release and withdrawal");
            soldier.enabled=false; Check(soldier.SelectedCover==null,"interrupted actor leaves no cover reservation");
            Destroy(cover);
        }
    }
}
