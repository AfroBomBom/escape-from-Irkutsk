using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace IrkutskPrototype
{
    public sealed class RescueChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        EscapeGame game;
        HumanAgent soldier,medic;
        Material clothing;
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Hidden).SetValue(o,value);
        static object Call(object o,string name)=>o.GetType().GetMethod(name,Hidden).Invoke(o,null);
        static void Check(bool ok,string message)
        { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void PlaceHero(EscapeGame game,Vector3 p)
        { game.Controller.enabled=false; game.Hero.position=p; game.Controller.enabled=true; Physics.SyncTransforms(); }
        HumanAgent Spawn(HumanRole role,Vector3 point)
        {
            var actor=game.SpawnHuman("Rescue test "+role,role,point,clothing);
            actor.enabled=false; Set(actor,"shotAt",Time.time+100); Set(actor,"healAt",Time.time+100); return actor;
        }
        void Pair(Vector3? medicPoint=null,Vector3? soldierPoint=null)
        {
            foreach(var actor in game.Humans.ToArray())
            {
                actor.enabled=false; actor.gameObject.SetActive(false);
                if(actor.name.StartsWith("Rescue test")) { game.Humans.Remove(actor); Destroy(actor.gameObject); }
            }
            soldier=Spawn(HumanRole.Soldier,soldierPoint??new Vector3(20,0,0));
            medic=Spawn(HumanRole.Medic,medicPoint??new Vector3(20,0,-4));
            game.Soldier=soldier; game.Medic=medic; soldier.enabled=true;
            PlaceHero(game,new Vector3(-28,0,-22)); Set(game,"<PatrolHostile>k__BackingField",false);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-rescue-check")<0) return;
            Application.runInBackground=true; new GameObject("Stage four rescue verification").AddComponent<RescueChecks>();
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
            Debug.Log("IRKUTSK_RESCUE_OK"); Application.Quit(0);
        }
        IEnumerator Run(EscapeGame instance)
        {
            game=instance; game.enabled=false;
            clothing=game.Soldier.GetComponentInChildren<Renderer>().sharedMaterial;
            foreach(var z in game.Zombies) { z.enabled=false; z.gameObject.SetActive(false); }
            Pair(); yield return null;
            int rep=game.Reputation, money=game.Money, shots=game.SoldierTracers;
            soldier.Hit(202,false);
            Check(soldier.IsIncapacitated && !soldier.Downed && soldier.Health==0,"moderate lethal hit creates a living incapacitated patrol member");
            Check(game.Reputation==rep && game.Money==money,"incapacitation does not charge a kill penalty");
            Check(!soldier.GetComponent<CharacterController>().enabled && soldier.GetComponent<BoxCollider>().enabled
                && soldier.GetComponent<ActorPosture>().IsIncapacitated,"incapacitated actor has a low fallen body with a physical hitbox");
            Vector3 origin=soldier.transform.position;
            soldier.MoveTo(new Vector3(24,0,4)); soldier.AlertTo(game.Hero); Call(soldier,"Update");
            Check(soldier.transform.position==origin && !soldier.CurrentTarget && game.SoldierTracers==shots,"incapacitated actor cannot move, acquire reports or fire");
            Check(!game.Alive(soldier.transform),"armed NPC target selection excludes incapacitated enemies");
            medic.enabled=true;
            yield return new WaitForSeconds(.5f);
            Check(!medic.RescuePatient,"fresh damage prevents an immediate rescue attempt");
            Check(soldier.BleedoutRemaining<30 && soldier.BleedoutRemaining>28,"bleeding countdown advances in real time");
            yield return new WaitForSeconds(2.8f);
            Check(soldier.IsIncapacitated && medic.RescuePatient==soldier && medic.RescueProgress>0,"medic approaches casualty and starts timed first aid in real frames");
            Check(medic.GetComponent<ActorPosture>().IsCrouched,"medic stays crouched while providing first aid");
            yield return new WaitForSeconds(3.2f);
            Check(!soldier.IsIncapacitated && !soldier.Downed && soldier.Health==40,"completed medic rescue restores forty health");
            Check(soldier.GetComponent<CharacterController>().enabled && !soldier.GetComponent<BoxCollider>().enabled
                && !soldier.GetComponent<ActorPosture>().IsIncapacitated,"revival restores movement capsule and removes the fallen body hitbox");
            Check(!soldier.Rescuer && !medic.RescuePatient && soldier.BleedoutRemaining==0,"completed rescue clears ownership, progress and bleeding timer");
            yield return new WaitForSeconds(1);
            Check(soldier.State!="Тяжело ранен" && soldier.TacticalTask!=PatrolTask.Rescue,"revived patrol member resumes ordinary behavior");
            soldier.Hit(82,false);
            Check(soldier.Downed && !soldier.IsIncapacitated,"a second fatal wound after revival is final");

            Pair(); yield return null; soldier.Hit(202,false);
            PlaceHero(game,new Vector3(20,0,5)); Set(game,"<PatrolHostile>k__BackingField",true); medic.AlertTo(game.Hero); medic.enabled=true;
            yield return new WaitForSeconds(1.6f);
            Check(soldier.IsIncapacitated && !medic.RescuePatient,"medic refuses rescue in exposed space under a known enemy");
            PlaceHero(game,new Vector3(-28,0,-22)); Set(medic,"<CurrentTarget>k__BackingField",null); Set(medic,"searchUntil",0f);
            yield return new WaitForSeconds(2.5f);
            Check(medic.RescuePatient==soldier && medic.RescueProgress>0,"rescue starts after the danger clears");
            medic.Hit(1,false); Call(medic,"Update");
            Check(!medic.RescuePatient && !soldier.Rescuer && medic.RescueProgress==0,"damage interrupts treatment and resets its progress and reservation");
            yield return new WaitForSeconds(1.6f);
            Check(medic.RescuePatient==soldier,"medic can retry after a safe interval");
            PlaceHero(game,medic.transform.position+Vector3.forward*3); Set(game,"<PatrolHostile>k__BackingField",true); Call(medic,"Update");
            Check(!medic.RescuePatient && soldier.IsIncapacitated,"new visible threat cancels ongoing first aid before completion");
            PlaceHero(game,new Vector3(-28,0,-22)); Set(medic,"<CurrentTarget>k__BackingField",null); Set(medic,"searchUntil",0f);
            yield return new WaitForSeconds(1.6f);
            medic.Stun(2); Call(medic,"Update");
            Check(!medic.RescuePatient && !soldier.Rescuer,"stunning rescuer immediately releases the casualty");

            Pair(new Vector3(20,0,-2)); yield return null; soldier.Hit(202,false);
            yield return new WaitForSeconds(1.3f);
            medic.enabled=true; Call(medic,"Update");
            var other=Spawn(HumanRole.Medic,new Vector3(22,0,-2)); other.enabled=true;
            Check(soldier.Rescuer==medic && !(bool)Call(other,"CanRescue"),"two medics cannot claim the same casualty");
            medic.enabled=false;
            Check(!soldier.Rescuer && (bool)Call(other,"CanRescue"),"disabling rescuer allows another medic to take over");
            other.enabled=false;

            Pair(new Vector3(20,0,-2)); yield return null; soldier.Hit(202,false);
            yield return new WaitForSeconds(1.3f); medic.enabled=true; Call(medic,"Update");
            var guard=Spawn(HumanRole.Soldier,new Vector3(24,0,-2)); guard.enabled=true; Call(guard,"Update");
            Check(guard.TacticalTask==PatrolTask.GuardRescue && soldier.Rescuer==medic,"third soldier guards ongoing medic rescue instead of stealing it");
            Vector3 guardPoint=guard.transform.position;
            yield return new WaitForSeconds(.3f);
            Check(Vector3.Distance(guardPoint,guard.transform.position)<.1f,"rescue guard holds position and observes the area");

            Pair(new Vector3(20,0,0),new Vector3(20,0,-3)); yield return null;
            medic.Hit(126,false); soldier.enabled=true;
            PlaceHero(game,new Vector3(20,0,1)); Set(game,"scanProgress",0f); Set(game,"<Scanned>k__BackingField",false); Call(game,"TickExpedition");
            Check((float)typeof(EscapeGame).GetField("scanProgress",Hidden).GetValue(game)==0,"incapacitated medic cannot scan the hero");
            PlaceHero(game,new Vector3(-28,0,-22));
            yield return new WaitForSeconds(2.6f);
            Check(medic.IsIncapacitated && soldier.RescuePatient==medic && soldier.RescueProgress>0,"soldier provides slower first aid to an incapacitated medic");
            yield return new WaitForSeconds(6);
            Check(!medic.IsIncapacitated && medic.Health==25,"completed soldier first aid restores twenty-five health");

            Pair(new Vector3(20,0,-1.5f)); yield return null; soldier.Hit(202,false);
            yield return new WaitForSeconds(1.3f);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer=8;
            wall.transform.position=new Vector3(20,1,-.75f); wall.transform.localScale=new Vector3(4,2,.2f); Physics.SyncTransforms();
            Set(medic,"rescuePatient",soldier); Set(medic,"rescueGoal",medic.transform.position); Set(medic,"rescuePath",new[]{medic.transform.position});
            Set(medic,"rescueArriveAt",Time.time+10); Call(medic,"RescuePartner");
            Check(medic.RescueProgress==0 && !medic.RescuePatient && soldier.IsIncapacitated,"first aid cannot progress through a wall");
            Destroy(wall); yield return null;
            int before=game.Reputation; soldier.Hit(40,true);
            Check(soldier.Downed && !soldier.IsIncapacitated && game.Reputation==before-15,"finishing a casualty applies the kill penalty once");
            before=game.Reputation; soldier.Hit(100,true);
            Check(game.Reputation==before,"dead casualty cannot generate repeated penalties");

            Pair(); yield return null; before=game.Reputation; soldier.Hit(202,true);
            Check(game.Reputation==before,"player-caused incapacitation defers the kill penalty until actual death");
            Set(soldier,"bleedoutAt",Time.time-.01f); Call(soldier,"Update");
            Check(soldier.Downed && game.Reputation==before-15,"bleed-out caused by player wound becomes a single attributed death");
            Check(!(bool)Call(medic,"CanRescue"),"dead patrol members cannot be revived");
            Pair(); yield return null; soldier.Hit(500,false);
            Check(soldier.Downed && !soldier.IsIncapacitated,"large overkill remains immediately fatal");

            Pair(new Vector3(20,0,-4),new Vector3(20,0,-.97f)); yield return null;
            var cover=Instantiate(Resources.Load<GameObject>("Cover/LowCover"),new Vector3(20,0,0),Quaternion.identity); Physics.SyncTransforms();
            soldier.Hit(202,false); PlaceHero(game,new Vector3(20,0,5)); Set(game,"<PatrolHostile>k__BackingField",true);
            medic.AlertTo(game.Hero); medic.enabled=true;
            yield return new WaitForSeconds(6.5f);
            Check(!soldier.IsIncapacitated && !soldier.Downed,"medic completes a rescue behind protective low cover despite a visible distant threat");
            Destroy(cover);
        }
    }
}
