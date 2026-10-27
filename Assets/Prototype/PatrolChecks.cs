using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace IrkutskPrototype
{
    public static class PatrolChecks
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool ok,string message) { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
        static void Call(object o,string name)=>o.GetType().GetMethod(name,Flags).Invoke(o,null);
        static void Move(Transform t,Vector3 p)
        { var cc=t.GetComponent<CharacterController>(); cc.enabled=false; t.position=p; cc.enabled=true; Physics.SyncTransforms(); }
        public static void Run(EscapeGame g)
        {
            var actors=new List<Transform>{g.Hero}; foreach(var h in g.Humans) actors.Add(h.transform); foreach(var z in g.Zombies) actors.Add(z.transform);
            var positions=new List<Vector3>(); var rotations=new List<Quaternion>();
            foreach(var t in actors) { positions.Add(t.position); rotations.Add(t.rotation); }
            g.Hero.rotation=Quaternion.identity; Move(g.Hero,new Vector3(0,0,-10)); g.Fog.Refresh();
            Check(g.Fog.IsVisible(new Vector3(0,0,-11)) && !g.Fog.IsVisible(new Vector3(0,0,-13)),"small personal circle reveals nearby rear ground only");
            foreach(var z in g.Zombies) z.gameObject.SetActive(false);
            var marauder=g.Humans.Find(h=>h.Role==HumanRole.Marauder); marauder.gameObject.SetActive(false);
            Move(g.Hero,new Vector3(25,0,-20));
            Move(g.Medic.transform,new Vector3(0,0,0)); Move(g.Soldier.transform,new Vector3(0,0,-2)); Move(g.Citizen.transform,new Vector3(0,0,2));
            g.Medic.transform.rotation=Quaternion.identity; g.Soldier.transform.rotation=Quaternion.identity;
            Set(g.Medic,"<CurrentTarget>k__BackingField",g.Citizen.transform); Set(g.Medic,"scanTime",2f);
            int rep=g.Reputation, money=g.Money, tracers=g.SoldierTracers;
            Call(g.Medic,"Update");
            Check(g.Checked(g.Citizen.transform) && !g.Threat(g.Citizen.transform),"patrol scans a peaceful NPC without declaring threat");
            Check(g.SoldierTracers==tracers,"peaceful scanning does not fire lethal shots");
            Move(g.Soldier.transform,new Vector3(0,0,1));
            Set(g.Soldier,"<CurrentTarget>k__BackingField",g.Citizen.transform); Set(g.Soldier,"bindTime",3f);
            Call(g.Soldier,"Update"); Check(g.Citizen.IsDetained,"healthy NPC can be sent to quarantine");
            Set(g.Citizen,"<IsDetained>k__BackingField",false); g.Citizen.gameObject.SetActive(false);
            var infected=g.Zombies[0]; infected.gameObject.SetActive(true); Move(infected.transform,new Vector3(0,0,2));
            Set(g.Medic,"<CurrentTarget>k__BackingField",infected.transform); Set(g.Medic,"scanTime",2f);
            Call(g.Medic,"Update"); Check(g.Checked(infected.transform) && g.Threat(infected.transform),"scan confirms infected NPC as threat");
            Set(g.Soldier,"shotAt",0f); g.Soldier.transform.rotation=Quaternion.identity;
            bool medicEnabled=g.Medic.enabled; g.Medic.enabled=true; Call(g.Soldier,"Update"); g.Medic.enabled=medicEnabled; Check(g.SoldierTracers>tracers,"soldier attacks confirmed infected instead of only hero");
            var victim=UnityEngine.Object.Instantiate(g.Citizen.gameObject); victim.SetActive(true);
            victim.GetComponent<HumanAgent>().Hit(200,false,infected.transform);
            Check(g.Reputation==rep && g.Money==money,"NPC fights do not charge hero for deaths"); UnityEngine.Object.Destroy(victim);
            marauder.gameObject.SetActive(true); Move(marauder.transform,new Vector3(1,0,2));
            g.ObserveAggression(marauder.transform); Check(g.Threat(marauder.transform),"witnessed marauder aggression authorizes patrol response");
            Set(infected,"health",100f); Set(infected,"stunnedUntil",0f); Set(infected,"alertUntil",0f); Set(infected,"investigateAt",0f); Set(infected,"suspicion",0f);
            foreach(var z in g.Zombies) z.gameObject.SetActive(false); marauder.gameObject.SetActive(false);
            Move(g.Hero,new Vector3(-28,0,-22));
            Move(g.Soldier.transform,new Vector3(25,0,-20)); g.Soldier.transform.rotation=Quaternion.Euler(0,180,0);
            Set(g.Soldier,"<CurrentTarget>k__BackingField",null); Set(g.Soldier,"searchUntil",0f); Set(g.Soldier,"pauseUntil",0f);
            Vector3 before=g.Soldier.transform.position; Call(g.Soldier,"Update");
            Check(Vector3.Distance(before,g.Soldier.transform.position)>.001f,"patrol moves along route when no target is visible");
            var path=GridRoute.Find(new Vector3(4,0,3),new Vector3(4,0,11));
            Check(path.Count>0,"patrol route can go around a building");
            Move(infected.transform,new Vector3(4,0,3)); Set(infected,"suspicion",0f); Set(infected,"investigateAt",0f);
            infected.Hear(new Vector3(4,0,11),14);
            Check((float)typeof(Infected).GetField("suspicion",Flags).GetValue(infected)==0,"walls attenuate infected hearing");
            for(int i=0;i<actors.Count;i++) { actors[i].gameObject.SetActive(true); Move(actors[i],positions[i]); actors[i].rotation=rotations[i]; }
            foreach(var h in g.Humans) { Set(h,"<CurrentTarget>k__BackingField",null); Set(h,"scanTime",0f); Set(h,"bindTime",0f); }
        }
    }
}
