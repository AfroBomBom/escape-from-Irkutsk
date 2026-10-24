using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace IrkutskPrototype
{
    public sealed class MarauderLootChecks : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        EscapeGame game;
        HumanAgent looter;
        Material clothing;
        readonly List<WorldLoot> samples=new List<WorldLoot>();
        readonly List<HumanAgent> actors=new List<HumanAgent>();
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Hidden).SetValue(o,value);
        static object Get(object o,string name)=>o.GetType().GetField(name,Hidden).GetValue(o);
        static object Call(object o,string name)=>o.GetType().GetMethod(name,Hidden).Invoke(o,null);
        static void Check(bool ok,string message)
        { if(!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("PASS: "+message); }
        static void Place(Transform actor,Vector3 point)
        {
            var cc=actor.GetComponent<CharacterController>(); if(cc) cc.enabled=false;
            actor.position=point; actor.rotation=Quaternion.identity; if(cc) cc.enabled=true; Physics.SyncTransforms();
        }
        List<int> Bag => (List<int>)Get(game,"bag");
        HumanAgent Actor(Vector3 point)
        {
            var actor=game.SpawnHuman("Loot check marauder",HumanRole.Marauder,point,clothing);
            actor.enabled=false; Set(actor,"shotAt",Time.time+100); actors.Add(actor); return actor;
        }
        void Reset()
        {
            foreach(var h in game.Humans) { h.enabled=false; h.gameObject.SetActive(false); }
            foreach(var actor in actors) { game.Humans.Remove(actor); Destroy(actor.gameObject); } actors.Clear();
            foreach(var item in samples)
            {
                if(!item) continue;
                var part=item.GetComponent<SparePart>(); if(part) game.Parts.Remove(part);
                var extra=item.GetComponent<ExpeditionPickup>(); if(extra) game.ExtraItems.Remove(extra);
                Destroy(item.gameObject);
            }
            samples.Clear();
            foreach(var item in new List<WorldLoot>(WorldLoot.Active)) if(item) item.gameObject.SetActive(false);
            Bag.Clear(); Set(game,"shopUnlocked",false); Set(game,"shopLooted",false);
            Set(game,"<PatrolHostile>k__BackingField",false); Place(game.Hero,new Vector3(-28,0,-22));
            looter=Actor(new Vector3(20,0,0));
        }
        WorldLoot Part(int kind,Vector3 point)
        { var item=game.SpawnLoosePart(kind,point).GetComponent<WorldLoot>(); samples.Add(item); return item; }
        WorldLoot Extra(int kind,Vector3 point,bool owned=false)
        { var item=game.SpawnExtra("Loot check item "+kind,kind,point,clothing,owned).GetComponent<WorldLoot>(); samples.Add(item); return item; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-marauder-loot-check")<0) return;
            Application.runInBackground=true; new GameObject("Stage five looting verification").AddComponent<MarauderLootChecks>();
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
            Debug.Log("IRKUTSK_MARAUDER_LOOT_OK"); Application.Quit(0);
        }
        IEnumerator Run(EscapeGame instance)
        {
            game=instance; game.enabled=false; clothing=game.Soldier.GetComponentInChildren<Renderer>().sharedMaterial;
            foreach(var z in game.Zombies) { z.enabled=false; z.gameObject.SetActive(false); }
            Reset(); var source=Part(0,new Vector3(20,.45f,-2)); yield return null; looter.enabled=true; Call(looter,"Update");
            Check(!looter.LootTarget,"marauder cannot discover loot behind their vision cone");
            looter.transform.forward=Vector3.back; Set(looter,"lootScanAt",0f); Call(looter,"Update");
            Check(looter.LootTarget==source && source.ClaimedBy==looter,"turning toward visible loot records and reserves it");
            looter.enabled=false; Check(!source.ClaimedBy,"disabled looter releases item reservation");

            Reset(); source=Part(0,new Vector3(20,.45f,9)); yield return null; looter.enabled=true; Call(looter,"Update");
            Check(!looter.LootTarget,"loot outside eight metre detection range is unknown");
            Reset(); source=Part(0,new Vector3(20,.45f,3));
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer=8; wall.transform.position=new Vector3(20,1,1.5f); wall.transform.localScale=new Vector3(4,2,.3f);
            Physics.SyncTransforms(); yield return null; looter.enabled=true; Call(looter,"Update");
            Check(!looter.LootTarget,"solid wall blocks discovery of nearby loot");
            Destroy(wall); yield return null; looter.transform.forward=Vector3.forward; Set(looter,"lootScanAt",0f); Call(looter,"Update");
            Check(looter.LootTarget==source,"loot becomes discoverable after obstruction is removed");

            Reset(); source=Part(0,new Vector3(20,.45f,6)); yield return null; looter.enabled=true; Call(looter,"Update");
            Vector3 remembered=(Vector3)Get(looter,"lootGoal"); source.transform.position=new Vector3(26,.45f,-8); Physics.SyncTransforms();
            yield return new WaitForSeconds(.25f);
            Check((Vector3)Get(looter,"lootGoal")==remembered && looter.transform.position.z>0,"unseen relocation does not move the remembered loot destination");
            Place(looter.transform,remembered); Set(looter,"lootScanAt",Time.time+1); Call(looter,"Update");
            Check(!looter.LootTarget && source.Available && looter.LootInventory.Count==0,"arrival at stale sighting cannot collect remotely moved loot");

            Reset(); source=Part(0,new Vector3(20,.45f,1)); yield return null; looter.enabled=true;
            yield return new WaitForSeconds(.3f);
            Check(looter.LootProgress>0 && source.Available && looter.LootInventory.Count==0,"collecting has a visible delay before the item transfers");
            Place(game.Hero,new Vector3(20,0,3)); Call(looter,"Update");
            Check(looter.CurrentTarget==game.Hero && !looter.LootTarget && !source.ClaimedBy && looter.LootProgress==0,"visible enemy interrupts collecting and frees item");
            Place(game.Hero,new Vector3(-28,0,-22)); Set(looter,"<CurrentTarget>k__BackingField",null); Set(looter,"searchUntil",0f);
            yield return new WaitForSeconds(.3f); looter.Stun(1); Call(looter,"Update");
            Check(!looter.LootTarget && source.Available && looter.LootProgress==0,"stun cancels work without consuming the source");
            yield return new WaitForSeconds(2.6f);
            Check(looter.LootInventory.Count==1 && !source.gameObject.activeSelf && source.CarriedBy==looter,"marauder resumes looting after danger and stun end");

            Reset(); source=Part(0,new Vector3(20,.45f,1)); yield return null; looter.enabled=true;
            yield return new WaitForSeconds(.25f); looter.HearNoise(new Vector3(20,0,-3),8,game.Hero,NoiseKind.Work); Call(looter,"Update");
            Check(looter.HasSoundClue && !looter.LootTarget && source.Available,"investigating a heard threat takes priority over collecting");

            Reset(); source=Part(0,new Vector3(20,.45f,1)); var other=Actor(new Vector3(20.7f,0,0)); yield return null;
            looter.enabled=true; other.enabled=true; Call(looter,"Update"); Call(other,"Update");
            Check(source.ClaimedBy==looter && !other.LootTarget,"two marauders cannot reserve the same item");
            Bag.Add(4); Bag.Add(5); Place(game.Hero,source.transform.position); Call(game,"Interact");
            Check(source.Available && Bag.Count==2,"full hero backpack does not consume a reserved item");
            Bag.Clear(); Call(game,"Interact"); Call(looter,"Update"); Call(other,"Update");
            Check(Bag.Count==1 && Bag[0]==0 && source.ConsumedByHero && !source.CarriedBy && !looter.LootTarget,"hero can take a reserved item and NPC does not duplicate it");
            Check(!source.TakeForHero() && looter.LootInventory.Count==0 && other.LootInventory.Count==0,"consumed source cannot transfer a second time");

            Reset(); var a=Part(0,new Vector3(20,.45f,1)); var b=Extra(4,new Vector3(21,.4f,1)); var c=Extra(5,new Vector3(19,.4f,1)); var fourth=Part(2,new Vector3(25,.45f,6));
            yield return null; looter.enabled=true;
            yield return new WaitForSeconds(4.8f);
            Check(looter.LootInventory.Count==3 && fourth.Available,"marauder backpack stops at three collected items");
            Check(!looter.LootTarget && looter.State=="Охраняю добычу","full marauder stops collecting and guards their haul at home");
            int rep=game.Reputation,money=game.Money,partCount=game.Parts.Count;
            looter.Hit(200);
            Check(looter.Downed && looter.LootInventory.Count==0 && a.Available && b.Available && c.Available,"death drops every carried original item and clears inventory");
            Check(a.Kind==0 && b.Kind==4 && c.Kind==5 && !a.CarriedBy && !b.CarriedBy,"dropped gear keeps original item types without carrier references");
            Check(game.Reputation==rep && game.Money==money,"marauder death and NPC looting do not penalize hero money or reputation");
            looter.Hit(200); Check(game.Parts.Count==partCount && a.Available,"repeated damage to corpse cannot create duplicate drops");
            foreach(var item in new[]{a,b,c}) Check(NpcTactics.Walkable(new Vector3(item.transform.position.x,0,item.transform.position.z)),"dropped "+item.Kind+" occupies reachable free ground");

            Reset(); source=Extra(4,new Vector3(20,.4f,1),true); yield return null; looter.enabled=true; Call(looter,"Update");
            Check(!source.Available && !looter.LootTarget,"locked store ownership prevents NPC collection even if stock is made active");
            Set(game,"shopUnlocked",true); Set(looter,"lootScanAt",0f);
            rep=game.Reputation; money=game.Money;
            yield return new WaitForSeconds(1.6f);
            Check(looter.LootInventory.Count==1 && game.Reputation==rep && game.Money==money,"NPC can loot opened stock without charging hero a theft penalty");
            looter.Hit(200,false); Check(source.Available && source.Owned,"foreign ownership label persists when stolen goods drop");
            Place(game.Hero,source.transform.position); Call(game,"Interact");
            Check(Bag.Contains(4) && game.Reputation==rep-10 && game.Money==money,"hero theft of dropped foreign goods follows existing reputation rule");

            Reset(); source=Extra(4,new Vector3(20,.4f,1)); yield return null; looter.enabled=true; yield return new WaitForSeconds(1.6f);
            looter.Detain();
            Check(looter.IsDetained && source.Available && looter.LootInventory.Count==0,"detention releases carried quest medicine instead of removing it from the run");
            Place(game.Hero,source.transform.position); Call(game,"Interact");
            game.AcceptQuest(); Place(game.Hero,game.Citizen.transform.position);
            Check(game.DeliverMedicine(),"medicine recovered from marauder still completes civilian quest");

            Reset(); yield return null; looter.enabled=true;
            var parts=new WorldLoot[4];
            for(int i=0;i<4;i++) parts[i]=Part(i,new Vector3(20+(i%2)*.4f,.45f,1+(i/2)*.3f));
            for(int i=0;i<3;i++) Check(parts[i].TryReserve(looter) && parts[i].TakeForMarauder(looter),"essential car part "+i+" can enter marauder inventory");
            Check(!parts[3].TryReserve(looter),"capacity is enforced by item transfer as well as AI choice");
            looter.Hit(200,false);
            var installed=(bool[])Get(game,"installed"); for(int i=0;i<4;i++) installed[i]=false;
            foreach(var part in parts)
            {
                Place(game.Hero,part.transform.position); Call(game,"Interact");
                Check(Bag.Contains(part.Kind),"hero recovers car component "+part.Kind+" through ordinary interaction");
                Place(game.Hero,game.Car.position+Vector3.right*3); Call(game,"Interact");
            }
            Check((int)Call(game,"InstalledCount")==4 && Bag.Count==0,"all stolen and recovered car parts remain usable for repair");
            Call(game,"Interact"); Check(game.IsDriving,"looting competition preserves the ability to finish the car and escape");
        }
    }
}
