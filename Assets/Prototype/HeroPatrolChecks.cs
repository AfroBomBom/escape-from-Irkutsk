using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace IrkutskPrototype
{
    // Real-frame regression checks; never installed during normal play.
    public sealed class HeroPatrolChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(object o,string field,object value)=>o.GetType().GetField(field,Hidden).SetValue(o,value);
        static object Get(object o,string field)=>o.GetType().GetField(field,Hidden).GetValue(o);
        static void Check(bool ok,string message)
        { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Place(Transform actor,Vector3 point)
        { var cc=actor.GetComponent<CharacterController>(); cc.enabled=false; actor.position=point; cc.enabled=true; actor.rotation=Quaternion.identity; Physics.SyncTransforms(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-hero-patrol-live-check")<0) return;
            Application.runInBackground=true; new GameObject("Hero patrol verification").AddComponent<HeroPatrolChecks>();
        }
        IEnumerator Start()
        {
            yield return null; yield return null;
            var run=Run(EscapeGame.Instance);
            while(true)
            {
                object wait;
                try { if(!run.MoveNext()) break; wait=run.Current; }
                catch(Exception e) { Debug.LogException(e); Application.Quit(1); yield break; }
                yield return wait;
            }
            Debug.Log("IRKUTSK_HERO_PATROL_LIVE_OK"); Application.Quit(0);
        }
        IEnumerator Run(EscapeGame game)
        {
            game.enabled=false;
            foreach(var z in game.Zombies) { z.enabled=false; z.gameObject.SetActive(false); }
            foreach(var h in game.Humans) if(!h.IsPatrol) h.gameObject.SetActive(false);
            Place(game.Medic.transform,new Vector3(18,0,0)); Place(game.Soldier.transform,new Vector3(22,0,0));
            Place(game.Hero,new Vector3(20,0,-5));
            game.Medic.Hit(1);
            int shots=game.SoldierTracers; Set(game.Medic,"shotAt",Time.time+100);
            float firingUntil=Time.time+3;
            while((float)Get(game,"hp")>=100 && Time.time<firingUntil) yield return null;
            Set(game.Medic,"shotAt",0f); yield return new WaitForSeconds(1.5f);
            Check(game.Medic.CurrentTarget==game.Hero && game.Soldier.CurrentTarget==game.Hero,"both patrol members pursue hero after attack from behind");
            Check(game.SoldierTracers>shots && (float)Get(game,"hp")<100,"hostile soldier fires and damages hero in real frames");
            Check((float)Get(game.Medic,"shotAt")>Time.time,"hostile medic fires tranquilizer against hero");
            var distractor=game.Zombies[0];
            distractor.gameObject.SetActive(true);
            Place(distractor.transform,game.Soldier.transform.position+game.Soldier.transform.forward*1.5f);
            game.RegisterThreat(distractor.transform);
            Check(game.SelectPatrolTarget(game.Soldier)==game.Hero,"nearer infected does not interrupt current visible hero engagement");
            distractor.gameObject.SetActive(false);
            Place(game.Hero,new Vector3(-28,0,-22));
            yield return new WaitForSeconds(.2f);
            Check(game.Soldier.State=="Поиск" && game.Soldier.CurrentTarget==game.Hero,"tree searches last known hero position after visual contact is lost");
            yield return new WaitForSeconds(4.2f);
            Check(game.Soldier.CurrentTarget==null && game.Soldier.State=="Обход","tree returns to patrol when target memory expires");
            // Reset hostility and give the medic a healthy person to check.
            Set(game,"<PatrolHostile>k__BackingField",false); Set(game,"<Scanned>k__BackingField",false);
            Set(game,"infection",0f); Set(game,"scanProgress",0f); Set(game,"heroStunUntil",0f);
            foreach(var h in game.Humans) if(h.IsPatrol)
            { Set(h,"<CurrentTarget>k__BackingField",null); Set(h,"shotAt",0f); Set(h,"scanTime",0f); }
            Place(game.Medic.transform,new Vector3(20,0,0)); Place(game.Soldier.transform,new Vector3(22,0,0));
            Place(game.Hero,new Vector3(20,0,2.6f));
            shots=game.SoldierTracers;
            float until=Time.time+2.4f;
            var tick=typeof(EscapeGame).GetMethod("TickExpedition",Hidden);
            while(Time.time<until) { tick.Invoke(game,null); yield return null; }
            Check(game.Scanned && !game.PatrolHostile,"healthy hero receives a peaceful scan");
            Check((float)Get(game.Soldier,"shotAt")==0,"soldier does not shoot healthy peaceful hero");
            yield return new WaitForSeconds(.3f);
            Check((float)Get(game,"heroStunUntil")>0,"medic stuns scanned healthy hero for detention");
            Set(game,"<Scanned>k__BackingField",false); Set(game,"scanProgress",0f); Set(game,"infection",50f);
            game.Medic.CancelDartRestraint(); Set(game,"restrainingMedic",null); Set(game,"<HeroStruggling>k__BackingField",false);
            Set(game,"detainProgress",0f); Set(game,"heroStunUntil",0f);
            Place(game.Medic.transform,new Vector3(20,0,0)); Place(game.Soldier.transform,new Vector3(22,0,0));
            Place(game.Hero,new Vector3(20,0,2.6f));
            shots=game.SoldierTracers;
            until=Time.time+3;
            while(Time.time<until) { tick.Invoke(game,null); yield return null; }
            Check(game.Scanned && game.PatrolHostile && game.SoldierTracers>shots,"infected hero scan transitions to live soldier fire");
        }
    }
}
