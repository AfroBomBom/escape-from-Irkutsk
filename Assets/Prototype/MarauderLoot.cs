using System.Collections.Generic;
using UnityEngine;
namespace IrkutskPrototype
{
    public sealed partial class HumanAgent
    {
        public IReadOnlyList<WorldLoot> LootInventory => lootInventory;
        public WorldLoot LootTarget => lootTarget;
        public float LootProgress => Mathf.Clamp01(lootWork/1.2f);
        readonly List<WorldLoot> lootInventory=new List<WorldLoot>(3);
        sealed class LootMemory { public WorldLoot Item; public Vector3 Point; public float Until,RejectedUntil; }
        readonly List<LootMemory> lootMemory=new List<LootMemory>(8);
        WorldLoot lootTarget;
        Vector3 lootPoint,lootGoal;
        float lootScanAt,lootWork,lootProgressAt,lootDistance,lootArriveUntil,lootInspectUntil;
        int lootArea=-1;
        bool lootUsedThisTick;
        // Authored search areas, independent of the presence or location of actual loot.
        static readonly Vector3[] LootAreas={new Vector3(16,0,-7),new Vector3(20,0,7),new Vector3(0,0,14),new Vector3(-16,0,2),new Vector3(-16,0,-12)};
        internal bool StashLoot(WorldLoot item)
        {
            if(Role!=HumanRole.Marauder || Downed || IsDetained || lootInventory.Count>=3 || lootInventory.Contains(item)) return false;
            lootInventory.Add(item); return true;
        }
        void CancelLoot()
        {
            if(lootTarget) lootTarget.Release(this);
            lootTarget=null; lootWork=0;
        }
        void DropLoot()
        {
            CancelLoot();
            for(int i=lootInventory.Count-1;i>=0;i--)
                if(!lootInventory[i] || lootInventory[i].Drop(this,i)) lootInventory.RemoveAt(i);
            if(Game.Fog) Game.Fog.Refresh();
        }
        void ObserveLoot()
        {
            if(Time.time<lootScanAt) return;
            lootScanAt=Time.time+.35f;
            lootMemory.RemoveAll(m=>!m.Item || Time.time>m.Until);
            foreach(var item in WorldLoot.Active)
            {
                if(!item || !item.Available || !Sight.InCone(transform.position,transform.forward,item.transform.position,8,Sight.HumanAngle)
                    || !Sight.Clear(ActorPosture.Eye(transform),item.transform.position+Vector3.up*.15f)) continue;
                var memory=lootMemory.Find(m=>m.Item==item);
                if(memory==null) { if(lootMemory.Count>=8) continue; memory=new LootMemory { Item=item }; lootMemory.Add(memory); }
                memory.Point=item.transform.position; memory.Until=Time.time+20;
            }
        }
        bool ChooseLoot()
        {
            LootMemory best=null; float score=float.MaxValue;
            foreach(var memory in lootMemory)
            {
                var item=memory.Item;
                if(!item || !item.Available || Time.time<memory.RejectedUntil || (item.ClaimedBy && item.ClaimedBy!=this)) continue;
                Vector3 point=memory.Point; point.y=0;
                float distance=Vector3.Distance(transform.position,point);
                if(distance>=score || !NpcTactics.Walkable(point)) continue;
                if(!NpcTactics.ClearStep(transform.position,point) && GridRoute.Find(transform.position,point).Count==0) { memory.RejectedUntil=Time.time+6; continue; }
                best=memory; score=distance;
            }
            if(best==null || !best.Item.TryReserve(this)) return false;
            lootTarget=best.Item; lootPoint=best.Point; lootGoal=lootPoint; lootGoal.y=0;
            lootDistance=score; lootProgressAt=Time.time; lootArriveUntil=Time.time+8; lootWork=0; return true;
        }
        void RejectLoot()
        {
            var memory=lootMemory.Find(m=>m.Item==lootTarget);
            if(memory!=null) memory.RejectedUntil=Time.time+6;
            CancelLoot();
        }
        void ScavengeLoot()
        {
            lootUsedThisTick=true; ObserveLoot();
            if(lootInventory.Count>=3)
            {
                CancelLoot(); State="Рюкзак полон — к своей точке";
                if(Vector3.Distance(transform.position,home)>1) MoveTo(home,.8f,1.6f);
                else { State="Охраняю добычу"; transform.Rotate(0,35*Time.deltaTime,0); }
                return;
            }
            if(lootTarget && !lootTarget.TryReserve(this)) CancelLoot();
            if(!lootTarget && !ChooseLoot()) { ExploreLootArea(); return; }
            var sighting=lootMemory.Find(m=>m.Item==lootTarget);
            if(sighting!=null && Time.time<sighting.Until) { lootPoint=sighting.Point; lootGoal=lootPoint; lootGoal.y=0; }
            else { RejectLoot(); ExploreLootArea(); return; }
            float distance=Vector3.Distance(transform.position,lootGoal);
            if(distance<lootDistance-.15f) { lootDistance=distance; lootProgressAt=Time.time; }
            if(Time.time>lootArriveUntil || (distance>1.5f && Time.time-lootProgressAt>1.5f)) { RejectLoot(); ExploreLootArea(); return; }
            State="Ищу добычу: "+lootTarget.name;
            if(distance>1.5f) { lootWork=0; MoveTo(lootGoal,1.25f,1.7f); return; }
            // Arrival validates the snapshot; unseen relocation never updates the destination.
            if(Vector3.Distance(lootTarget.transform.position,lootPoint)>.6f
                || !Sight.Clear(ActorPosture.Eye(transform),lootTarget.transform.position+Vector3.up*.15f)) { RejectLoot(); return; }
            State="Собираю: "+lootTarget.name; lootWork+=Time.deltaTime;
            if(lootWork<1.2f) return;
            var taken=lootTarget;
            if(taken.TakeForMarauder(this))
            {
                lootMemory.RemoveAll(m=>m.Item==taken);
                Game.Noise(transform.position,2,transform,NoiseKind.Work);
            }
            CancelLoot();
        }
        void ExploreLootArea()
        {
            State="Обыскиваю дворы";
            if(lootArea<0)
            {
                float nearest=float.MaxValue;
                for(int i=0;i<LootAreas.Length;i++)
                { float d=Vector3.Distance(transform.position,LootAreas[i]); if(d<nearest) { nearest=d; lootArea=i; } }
                lootArriveUntil=Time.time+16;
            }
            Vector3 goal=LootAreas[lootArea];
            if(Vector3.Distance(transform.position,goal)<1.2f || Time.time>lootArriveUntil)
            {
                if(lootInspectUntil<=0) lootInspectUntil=Time.time+2;
                if(Time.time<lootInspectUntil) { State="Осматриваю двор"; transform.Rotate(0,90*Time.deltaTime,0); return; }
                lootArea=(lootArea+1)%LootAreas.Length; lootInspectUntil=0; lootArriveUntil=Time.time+16;
            }
            MoveTo(LootAreas[lootArea],1,1.7f);
        }
    }
}
