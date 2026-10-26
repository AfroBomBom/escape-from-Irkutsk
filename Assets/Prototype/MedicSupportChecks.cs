using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace IrkutskPrototype
{
    public sealed class MedicSupportChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        EscapeGame game; HumanAgent soldier,medic; Material clothing;
        static void Set(object o,string key,object value)=>o.GetType().GetField(key,Hidden).SetValue(o,value);
        static object Get(object o,string key)=>o.GetType().GetField(key,Hidden).GetValue(o);
        static object Call(object o,string key)=>o.GetType().GetMethod(key,Hidden).Invoke(o,null);
        static void Check(bool ok,string message)
        { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Place(Transform actor,Vector3 point)
        { var cc=actor.GetComponent<CharacterController>(); cc.enabled=false; actor.position=point; cc.enabled=true; actor.rotation=Quaternion.identity; Physics.SyncTransforms(); }
        void Pair(Vector3 doctor,Vector3 guard)
        {
            foreach(var dart in UnityEngine.Object.FindObjectsByType<HealingDart>()) Destroy(dart.gameObject);
            foreach(var actor in game.Humans.ToArray())
            {
                actor.enabled=false; actor.gameObject.SetActive(false);
                if(actor.name.StartsWith("Support check")) { game.Humans.Remove(actor); Destroy(actor.gameObject); }
            }
            soldier=game.SpawnHuman("Support check soldier",HumanRole.Soldier,guard,clothing);
            medic=game.SpawnHuman("Support check medic",HumanRole.Medic,doctor,clothing);
            soldier.enabled=false; medic.enabled=false; game.Soldier=soldier; game.Medic=medic;
            Set(soldier,"shotAt",Time.time+100); Set(medic,"shotAt",Time.time+100); Set(medic,"healAt",Time.time+100);
            Place(game.Hero,new Vector3(-28,0,-22)); Set(game,"<PatrolHostile>k__BackingField",false);
        }
        void Engage(Vector3 enemy)
        {
            Place(game.Hero,enemy); Set(game,"<PatrolHostile>k__BackingField",true);
            soldier.enabled=true; medic.enabled=true; soldier.AlertTo(game.Hero); medic.AlertTo(game.Hero);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-medic-support-check")<0) return;
            Application.runInBackground=true; new GameObject("Medic rear position and healing verification").AddComponent<MedicSupportChecks>();
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
            Debug.Log("IRKUTSK_MEDIC_SUPPORT_OK"); Application.Quit(0);
        }
        IEnumerator Run(EscapeGame instance)
        {
            game=instance; game.enabled=false; clothing=game.Soldier.GetComponentInChildren<Renderer>().sharedMaterial;
            foreach(var z in game.Zombies) z.gameObject.SetActive(false);
            Pair(new Vector3(20,0,2),new Vector3(20,0,0)); yield return null; Engage(new Vector3(20,0,7));
            Call(soldier,"Update"); Call(medic,"Update");
            Check(medic.TacticalTask==PatrolTask.FollowRear,"frontline medic immediately chooses to move behind soldier");
            yield return new WaitForSeconds(3);
            Vector3 away=(soldier.transform.position-game.Hero.position).normalized;
            Check(Vector3.Dot(medic.transform.position-soldier.transform.position,away)>1,"medic physically reaches the rear instead of closing to four metres");
            Check(NpcTactics.FiringLane(soldier.transform,game.Hero),"medic correction leaves soldier firing lane clear");

            Pair(new Vector3(22,0,-1),new Vector3(20,0,0)); yield return null;
            Set(soldier,"<Health>k__BackingField",25f); Engage(new Vector3(20,0,5)); Call(soldier,"Update"); Call(medic,"Update");
            Check(soldier.TacticalTask==PatrolTask.Retreat && medic.TacticalTask==PatrolTask.FollowRear,"soldier withdrawal makes medic follow instead of holding in front");
            float soldierDistance=Vector3.Distance(soldier.transform.position,game.Hero.position), medicDistance=Vector3.Distance(medic.transform.position,game.Hero.position);
            yield return new WaitForSeconds(1.2f);
            Check(Vector3.Distance(soldier.transform.position,game.Hero.position)>soldierDistance+1
                && Vector3.Distance(medic.transform.position,game.Hero.position)>medicDistance+1,"both patrol members actually withdraw from the threat");

            Pair(new Vector3(22,0,-1),new Vector3(20,0,0)); yield return null;
            Set(soldier,"<Health>k__BackingField",25f); Engage(new Vector3(20,0,5));
            Place(game.Hero,new Vector3(-28,0,-22)); Call(medic,"Update");
            Check(((Vector3)Get(medic,"medicRearGoal")).z<0,"rear formation uses remembered enemy position rather than hidden live target");
            soldier.enabled=false; Call(medic,"Update");
            Check(medic.TacticalTask!=PatrolTask.FollowRear,"unavailable soldier releases medic to independent behavior");

            Pair(new Vector3(22,0,-1),new Vector3(20,0,0)); yield return null; Engage(new Vector3(20,0,7.8f));
            var cover=Instantiate(Resources.Load<GameObject>("Cover/LowCover"),new Vector3(20,0,4),Quaternion.identity); Physics.SyncTransforms();
            Call(medic,"Update"); Check(!medic.SelectedCover,"medic does not select cover ahead of the combat leader");
            Destroy(cover); yield return null;

            Pair(new Vector3(20,0,0),new Vector3(20,0,5)); yield return null;
            Set(soldier,"<Health>k__BackingField",50f); Set(medic,"healAt",0f);
            int fired=game.HealingDartsFired,hits=game.HealingDartsHit; float enemyCooldown=(float)Get(medic,"shotAt");
            Call(medic,"TrySafeHealing");
            Check(game.HealingDartsFired==fired+1 && soldier.Health==50,"ranged healing launches a projectile without instant remote healing");
            Check(UnityEngine.Object.FindObjectsByType<HealingDart>().Length>0,"healing dart exists as a visible moving projectile");
            yield return new WaitForSeconds(.4f);
            Check(soldier.Health==65 && game.HealingDartsHit==hits+1,"healing dart hit restores fifteen health at five metres");
            Call(medic,"TrySafeHealing"); Check(game.HealingDartsFired==fired+1,"eight second healing cooldown prevents immediate repeat shots");
            Check((float)Get(medic,"shotAt")==enemyCooldown,"healing cooldown is separate from enemy tranquilizer fire");
            Set(medic,"healAt",0f); Set(soldier,"<Health>k__BackingField",95f); Call(medic,"TrySafeHealing");
            yield return new WaitForSeconds(.4f); Check(soldier.Health==100,"healing clamps soldier health to maximum");

            Pair(new Vector3(20,0,0),new Vector3(20,0,7.5f)); yield return null;
            Set(soldier,"<Health>k__BackingField",50f); Set(medic,"healAt",0f); fired=game.HealingDartsFired; Call(medic,"TrySafeHealing");
            Check(game.HealingDartsFired==fired,"soldier beyond six metre healing range receives no dart");

            Pair(new Vector3(20,0,0),new Vector3(20,0,5)); yield return null;
            Set(soldier,"<Health>k__BackingField",50f); Set(medic,"healAt",0f); fired=game.HealingDartsFired;
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer=8; wall.transform.position=new Vector3(20,1,2.5f); wall.transform.localScale=new Vector3(3,2,.3f); Physics.SyncTransforms();
            Call(medic,"TrySafeHealing"); Check(game.HealingDartsFired==fired,"existing solid wall prevents attempted healing shot");
            Destroy(wall); yield return null; Call(medic,"TrySafeHealing");
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer=8; wall.transform.position=new Vector3(20,1,2.5f); wall.transform.localScale=new Vector3(3,2,.3f); Physics.SyncTransforms();
            yield return new WaitForSeconds(.4f); Check(soldier.Health==50,"new obstacle intercepts an already launched healing dart");
            Destroy(wall); yield return null;

            Set(medic,"healAt",0f); Call(medic,"TrySafeHealing");
            var blocker=game.SpawnHuman("Support check blocker",HumanRole.Civilian,new Vector3(20,0,2.5f),clothing); blocker.enabled=false; Physics.SyncTransforms();
            yield return new WaitForSeconds(.4f);
            Check(soldier.Health==50 && blocker.Health==100,"another character blocks the dart without being healed or damaged");

            Pair(new Vector3(20,0,0),new Vector3(20,0,5)); yield return null;
            Set(soldier,"<Health>k__BackingField",50f); Set(medic,"healAt",0f); Call(medic,"TrySafeHealing"); soldier.Hit(102,false);
            yield return new WaitForSeconds(.4f);
            Check(soldier.IsIncapacitated && soldier.Health==0,"healing dart cannot replace first aid or revive an incapacitated soldier");
        }
    }
}
