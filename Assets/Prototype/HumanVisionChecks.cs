using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace IrkutskPrototype
{
    public sealed class HumanVisionChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Hidden).SetValue(o,value);
        static void Call(object o,string name)=>o.GetType().GetMethod(name,Hidden).Invoke(o,null);
        static void Check(bool ok,string message)
        { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Place(Transform actor,Vector3 point)
        { var cc=actor.GetComponent<CharacterController>(); cc.enabled=false; actor.position=point; cc.enabled=true; actor.rotation=Quaternion.identity; Physics.SyncTransforms(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-human-vision-check")<0) return;
            Application.runInBackground=true; new GameObject("Human vision balance verification").AddComponent<HumanVisionChecks>();
        }
        IEnumerator Start()
        {
            yield return null; yield return null;
            try { Run(EscapeGame.Instance); Debug.Log("IRKUTSK_HUMAN_VISION_OK"); Application.Quit(0); }
            catch(Exception e) { Debug.LogException(e); Application.Quit(1); }
        }
        static void Run(EscapeGame game)
        {
            game.enabled=false; var material=game.Soldier.GetComponentInChildren<Renderer>().sharedMaterial;
            foreach(var h in game.Humans) h.gameObject.SetActive(false);
            foreach(var z in game.Zombies) z.gameObject.SetActive(false);
            var soldier=game.SpawnHuman("Vision check soldier",HumanRole.Soldier,new Vector3(20,0,0),material);
            var medic=game.SpawnHuman("Vision check medic",HumanRole.Medic,new Vector3(21,0,0),material);
            var marauder=game.SpawnHuman("Vision check marauder",HumanRole.Marauder,new Vector3(19,0,0),material);
            game.Soldier=soldier; game.Medic=medic;
            foreach(var h in new[]{soldier,medic,marauder}) { Set(h,"shotAt",Time.time+100); game.Fog.RegisterDynamic(h.gameObject); }
            Set(medic,"healAt",Time.time+100); Set(game,"<PatrolHostile>k__BackingField",false);
            Check(Sight.HumanRange==8 && float.IsPositiveInfinity(Sight.HeroRange) && Sight.InfectedRange==6,"human and infected ranges stay eight and six metres while hero has unlimited range");
            Check(Sight.HumanAngle==110 && Sight.HeroAngle==120 && Sight.ScannerRange==3.2f,"vision sectors and short range scanner keep their existing limits");
            Place(game.Hero,new Vector3(20,0,9)); game.Hero.forward=Vector3.back; game.Fog.Refresh();
            Check(game.Fog.IsVisible(soldier.transform),"hero fog reveals a standing soldier nine metres ahead");
            Check(!game.SelectPatrolTarget(soldier) && !game.SelectPatrolTarget(medic),"soldier and medic cannot acquire hero at nine metres");
            Call(marauder,"ObserveMarauder"); Check(!marauder.CurrentTarget,"marauder cannot acquire hero at nine metres");
            game.ReportGunfire(game.Hero.position); game.ObserveAggression(game.Hero);
            Check(!game.PatrolHostile,"distant visible gunfire recognition does not bypass eight metre sight range");
            soldier.ObserveConfirmedHeroHit(soldier,8); Check(soldier.KnownHeroDamage==0,"damage observer cannot see hero beyond human sight range");
            Place(game.Hero,new Vector3(20,0,7.5f));
            Check(game.SelectPatrolTarget(soldier)==game.Hero && game.SelectPatrolTarget(medic)==game.Hero,"patrol still detects hero inside eight metre range");
            Call(marauder,"ObserveMarauder"); Check(marauder.CurrentTarget==game.Hero,"marauder detects hero inside eight metre range");
            game.ObserveAggression(game.Hero); Check(game.PatrolHostile,"aggression observed inside the sight range still alerts patrol");
            foreach(var h in new[]{soldier,medic,marauder}) { Set(h,"<CurrentTarget>k__BackingField",null); Set(h,"searchUntil",0f); }
            Place(game.Hero,new Vector3(20,0,-2)); Call(marauder,"ObserveMarauder");
            Check(!game.SelectPatrolTarget(soldier) && !marauder.CurrentTarget,"rear targets remain outside human vision cone");
            Place(game.Hero,new Vector3(20,0,7)); soldier.ObserveConfirmedHeroHit(soldier,16);
            Set(game,"hp",40f); Place(game.Hero,new Vector3(20,0,9)); game.HealHero(50);
            Check(soldier.KnownHeroDamage==16,"healing nine metres away is unobserved despite remaining in hero view range");
            Place(game.Hero,new Vector3(20,0,7)); game.HealHero(10);
            Check(soldier.KnownHeroDamage==6,"healing inside eight metres remains visible to damage observer");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer=8; wall.transform.position=new Vector3(20,1,3.5f); wall.transform.localScale=new Vector3(6,2,.3f); Physics.SyncTransforms();
            Check(!game.SelectPatrolTarget(soldier) && !Sight.CanSee(marauder.transform,game.Hero,Sight.HumanRange,Sight.HumanAngle),"walls still block acquisition inside the reduced range");
            wall.SetActive(false); Destroy(wall); Physics.SyncTransforms();
            Place(game.Hero,new Vector3(20,0,9)); soldier.HearNoise(game.Hero.position,14,game.Hero,NoiseKind.Gunshot);
            Check(soldier.HasSoundClue && !game.SelectPatrolTarget(soldier),"hearing a distant shot gives a sound clue without extra vision");
            marauder.gameObject.SetActive(false);
            Place(game.Hero,new Vector3(20,0,7.5f)); Place(medic.transform,new Vector3(20,0,-3));
            soldier.AlertTo(game.Hero); medic.AlertTo(game.Hero); Set(medic,"shotAt",0f);
            int shots=game.SoldierTracers; Call(medic,"Update");
            Check(medic.State=="Позади солдата" && medic.transform.position.z< -2.5f,"medic retains rear formation from partner report when enemy is beyond own sight");
            Check(game.SoldierTracers==shots,"rear medic does not fire at enemy beyond eight metre vision");
            Place(game.Hero,new Vector3(40,0,0)); game.Hero.forward=Vector3.right;
            Place(soldier.transform,new Vector3(110,0,0)); game.Fog.Refresh();
            Check(game.Fog.IsVisible(new Vector3(115,0,0)) && game.Fog.IsExplored(new Vector3(115,0,0)),"fog reveals terrain seventy five metres ahead near map edge");
            Check(game.Fog.IsVisible(soldier.transform) && soldier.GetComponentInChildren<Renderer>().enabled,"distant actors inside sector are visible without a range cutoff");
            Check(!game.Fog.IsVisible(new Vector3(40,0,15)),"unlimited distance preserves the original sector angle");
            game.Hero.forward=Vector3.left; game.Fog.Refresh();
            Check(!game.Fog.IsVisible(soldier.transform) && game.Fog.IsExplored(new Vector3(115,0,0)),"turning away hides distant actors but keeps terrain memory");
            game.Hero.forward=Vector3.right;
            var distantWall=GameObject.CreatePrimitive(PrimitiveType.Cube); distantWall.layer=8;
            distantWall.transform.position=new Vector3(70,1.5f,0); distantWall.transform.localScale=new Vector3(.5f,3,10); Physics.SyncTransforms(); game.Fog.Refresh();
            Check(!game.Fog.IsVisible(new Vector3(115,0,0)) && !game.Fog.IsVisible(soldier.transform),"walls still block terrain and actors with unlimited hero range");
            distantWall.SetActive(false); Destroy(distantWall);
        }
    }
}
