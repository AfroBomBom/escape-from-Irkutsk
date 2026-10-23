using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace IrkutskPrototype
{
    public static class ExpeditionChecks
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Assert(bool value,string text) { if(!value) throw new Exception("CHECK FAILED: "+text); Debug.Log("PASS: "+text); }
        static object Read(object o,string name) => o.GetType().GetField(name,Flags).GetValue(o);
        static void Set(object o,string name,object value) => o.GetType().GetField(name,Flags).SetValue(o,value);
        static void Call(object o,string name,params object[] args) => o.GetType().GetMethod(name,Flags).Invoke(o,args);
        static void Move(EscapeGame g,Vector3 p) { g.Controller.enabled=false; g.Hero.position=p; g.Controller.enabled=true; Physics.SyncTransforms(); }
        public static void Run(EscapeGame g)
        {
            var bag=(List<int>)Read(g,"bag");
            Assert(g.Humans.Count==3 && g.Money==60,"civilian and patrol spawned without merchant");
            g.AcceptQuest(); var medicine=g.ExtraItems.Find(i=>!i.Owned && i.Kind==4);
            Move(g,medicine.transform.position); Call(g,"Interact");
            Assert(bag.Contains(4),"free medicine can be picked up");
            Move(g,g.Citizen.transform.position+Vector3.right);
            Assert(g.DeliverMedicine() && g.Reputation==4 && g.Money==95,"medicine quest pays once");
            Assert(!g.DeliverMedicine() && g.Money==95,"quest reward cannot be farmed");
            Move(g,new Vector3(-8,0,-1.7f));
            Assert(!g.LootStore(),"locked store cannot be looted");
            Set(g,"breakProgress",3f); Assert(g.UnlockStore(),"shutter opens after break-in");
            Set(g,"breakProgress",0f); Move(g,new Vector3(-8,0,2));
            bag.Add(0); bag.Add(1);
            Assert(!g.LootStore() && g.Money==95,"full inventory blocks theft without charging"); bag.Clear();
            Assert(g.LootStore() && g.Reputation==-6 && g.Money==85,"theft immediately applies fine and reputation loss"); bag.Clear();
            var armor=g.ExtraItems.Find(i=>i.Kind==5); Move(g,armor.transform.position); Call(g,"Interact");
            Move(g,g.Car.position+Vector3.right*3); Call(g,"Interact");
            Assert(g.ArmorInstalled && g.ArmorPoints==50,"optional plate installs on car");
            float carHP=(float)Read(g,"durability"); Call(g,"DamageCar",20f);
            Assert(g.ArmorPoints==30 && (float)Read(g,"durability")==carHP,"armor absorbs damage before chassis");
            Move(g,g.Medic.transform.position+Vector3.right*4);
            g.Medic.transform.forward=Vector3.right; Set(g,"scanProgress",1f); Call(g,"TickExpedition");
            Assert(!g.Scanned && (float)Read(g,"scanProgress")==0,"scanner progress resets beyond range");
            Move(g,g.Medic.transform.position+Vector3.right*2);
            g.Medic.transform.forward=(g.Hero.position-g.Medic.transform.position).normalized;
            Set(g,"scanProgress",2f); Call(g,"TickExpedition");
            Assert(g.Scanned && !g.PatrolHostile,"healthy scan does not authorize lethal force");
            g.Dart(); float stunned=(float)Read(g,"heroStunUntil");
            Assert(stunned>Time.time,"medical dart temporarily stuns hero");
            g.Dart(); Assert((float)Read(g,"heroStunUntil")==stunned,"dart immunity prevents an immediate stun chain");
            Set(g,"heroStunUntil",0f);
            Move(g,g.Medic.transform.position+Vector3.right*.8f);
            Set(g,"detainProgress",3f); Call(g,"TickExpedition");
            Assert(g.IsOver,"close patrol completes detention"); Set(g,"detained",false);
            Set(g,"<Scanned>k__BackingField",false); Set(g,"infection",50f); Set(g,"scanProgress",2f); Call(g,"TickExpedition");
            Assert(g.Scanned && g.PatrolHostile,"infected scan authorizes soldier fire");
            Set(g,"infection",0f); Set(g,"<Scanned>k__BackingField",false); Set(g,"<PatrolHostile>k__BackingField",false);
            Move(g,g.Soldier.transform.position+Vector3.right*3); g.ReportGunfire(g.Hero.position);
            g.Soldier.transform.forward=(g.Hero.position-g.Soldier.transform.position).normalized; g.ReportGunfire(g.Hero.position);
            Assert(g.PatrolHostile,"visible armed resistance alerts patrol");
            Set(g,"<PatrolHostile>k__BackingField",false); Set(g,"detainProgress",0f); Set(g,"scanProgress",0f);
            var victim=UnityEngine.Object.Instantiate(g.Citizen.gameObject); victim.transform.position=new Vector3(28,0,-22);
            int rep=g.Reputation; victim.GetComponent<HumanAgent>().Hit(150);
            Assert(g.Reputation==rep-25 && g.Money==25,"civilian kill applies reputation and money penalty");
            victim.GetComponent<HumanAgent>().Hit(150);
            Assert(g.Reputation==rep-25,"dead civilian cannot generate repeated penalties"); UnityEngine.Object.Destroy(victim);
            Set(g,"<PatrolHostile>k__BackingField",false);
            Move(g,new Vector3(-10,0,-10));
        }
    }
}
