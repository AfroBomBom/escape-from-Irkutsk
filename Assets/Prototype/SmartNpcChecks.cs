using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace IrkutskPrototype
{
    public sealed class SmartNpcChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Hidden).SetValue(o,value);
        static object Get(object o,string name)=>o.GetType().GetField(name,Hidden).GetValue(o);
        static void Check(bool ok,string message)
        { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Place(Transform actor,Vector3 p)
        { var cc=actor.GetComponent<CharacterController>(); cc.enabled=false; actor.position=p; cc.enabled=true; actor.rotation=Quaternion.identity; Physics.SyncTransforms(); }
        static void Isolate(EscapeGame game)
        {
            foreach(var h in game.Humans) { h.enabled=false; h.gameObject.SetActive(false); }
            foreach(var z in game.Zombies) { z.enabled=false; z.gameObject.SetActive(false); }
            Place(game.Hero,new Vector3(-28,0,-22));
        }
        static void Human(HumanAgent h,Vector3 p)
        {
            h.gameObject.SetActive(true); h.enabled=true; Place(h.transform,p);
            Set(h,"<CurrentTarget>k__BackingField",null); Set(h,"<Health>k__BackingField",100f);
            Set(h,"shotAt",0f); Set(h,"tacticsAt",0f); Set(h,"stunnedUntil",0f);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-smart-npc-check")<0) return;
            Application.runInBackground=true; new GameObject("Smart NPC verification").AddComponent<SmartNpcChecks>();
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
            Debug.Log("IRKUTSK_SMART_NPCS_OK"); Application.Quit(0);
        }
        IEnumerator Run(EscapeGame game)
        {
            game.enabled=false; Isolate(game);
            var z=game.Zombies[0];
            Human(game.Citizen,new Vector3(20,0,0)); Set(game.Citizen,"home",new Vector3(20,0,0));
            z.gameObject.SetActive(true); Place(z.transform,new Vector3(20,0,4));
            yield return new WaitForSeconds(1.1f);
            Check(game.Citizen.State=="Бегу от опасности" && Vector3.Distance(game.Citizen.transform.position,z.transform.position)>5,"civilian detects infected and moves away without attacking");
            z.gameObject.SetActive(false); Set(game.Citizen,"fearUntil",Time.time-.01f);
            float away=Vector3.Distance(game.Citizen.transform.position,new Vector3(20,0,0));
            yield return new WaitForSeconds(.7f);
            Check(Vector3.Distance(game.Citizen.transform.position,new Vector3(20,0,0))<away,"civilian returns toward home after danger memory expires");
            game.WarnCivilians(new Vector3(20,0,3));
            yield return new WaitForSeconds(.2f);
            Check(game.Citizen.State=="Бегу от опасности","nearby gunfire scares civilian without a visible enemy");

            Isolate(game);
            Human(game.Soldier,new Vector3(20,0,4)); game.Soldier.enabled=false; Set(game.Soldier,"<Health>k__BackingField",40f);
            Human(game.Medic,new Vector3(20,0,0)); Set(game.Medic,"healAt",0f);
            yield return new WaitForSeconds(1.5f);
            Check(Vector3.Distance(game.Medic.transform.position,game.Soldier.transform.position)<3 && game.Soldier.Health>40,"medic approaches injured partner and heals in range");

            Isolate(game);
            Human(game.Soldier,new Vector3(20,0,0)); Human(game.Medic,new Vector3(20,0,2)); game.Medic.enabled=false;
            Place(game.Hero,new Vector3(20,0,5)); Set(game,"<PatrolHostile>k__BackingField",true); game.Soldier.AlertTo(game.Hero);
            int shots=game.SoldierTracers;
            Check(!NpcTactics.FiringLane(game.Soldier.transform,game.Hero),"friendly medic blocks soldier firing lane");
            typeof(HumanAgent).GetMethod("Update",Hidden).Invoke(game.Soldier,null);
            Check(game.SoldierTracers==shots,"soldier holds fire while partner blocks target");
            Vector3 start=game.Soldier.transform.position;
            yield return new WaitForSeconds(1.6f);
            Check(Vector3.Distance(start,game.Soldier.transform.position)>.3f && game.SoldierTracers>shots,"soldier sidesteps around partner then resumes fire");

            Isolate(game);
            var marauder=game.Humans.Find(h=>h.Role==HumanRole.Marauder);
            Human(marauder,new Vector3(20,0,0)); Set(marauder,"<Health>k__BackingField",25f); Set(marauder,"shotAt",Time.time+30);
            Place(game.Hero,new Vector3(20,0,4));
            yield return new WaitForSeconds(1.1f);
            Check(marauder.State=="Отступаю к укрытию" && Vector3.Distance(marauder.transform.position,game.Hero.position)>5,"wounded marauder withdraws instead of charging hero");

            Isolate(game); Set(game,"hp",100f);
            z.gameObject.SetActive(true); z.enabled=true; Place(z.transform,new Vector3(20,0,0)); Place(game.Hero,new Vector3(20,0,1.1f));
            Set(z,"stunnedUntil",0f); Set(z,"biteAt",0f); Set(z,"windupUntil",0f); Set(z,"investigateAt",0f); Set(z,"suspicion",0f);
            yield return new WaitForSeconds(.1f);
            Check((float)Get(game,"hp")==100 && (float)Get(z,"windupUntil")>Time.time,"infected starts visible windup before dealing bite damage");
            Place(game.Hero,new Vector3(20,0,3.5f));
            yield return new WaitForSeconds(.35f);
            Check((float)Get(game,"hp")==100,"leaving bite range cancels attack before impact");
            Place(game.Hero,z.transform.position+Vector3.forward*1.1f);
            yield return new WaitForSeconds(.5f);
            Check((float)Get(game,"hp")<100,"completed infected windup still damages a nearby target");
            Place(game.Hero,new Vector3(-28,0,-22));
            yield return new WaitForSeconds(.15f);
            Check(z.CurrentTarget==game.Hero && Vector3.Distance((Vector3)Get(z,"lastKnown"),game.Hero.position)>20,"infected remembers last sighting without tracking hidden hero position");
            Set(z,"alertUntil",Time.time-.01f);
            yield return new WaitForSeconds(.1f);
            Check(z.CurrentTarget==null,"infected forgets target after search memory expires");
            Place(z.transform,new Vector3(20,0,0));
            z.Hit(0,0,Vector3.zero,new Vector3(20,1.15f,2));
            yield return new WaitForSeconds(1.2f);
            Check((int)Get(z,"searchStep")>0,"infected reaches elevated shot source on the ground and searches its surroundings");

            Isolate(game);
            Human(game.Soldier,new Vector3(22,0,0)); Human(game.Medic,new Vector3(20,0,0));
            z.gameObject.SetActive(true); Place(z.transform,new Vector3(20,0,-4));
            shots=game.SoldierTracers; game.Medic.Hit(1,false,z.transform);
            Check(game.Soldier.CurrentTarget==z.transform,"attacked patrol shares attacker position with nearby partner");
            yield return new WaitForSeconds(1);
            Check(game.SoldierTracers>shots,"partner turns toward reported attacker and enters combat");
        }
    }
}
