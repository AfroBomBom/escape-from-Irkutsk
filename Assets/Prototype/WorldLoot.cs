using System.Collections.Generic;
using UnityEngine;
namespace IrkutskPrototype
{
    [DisallowMultipleComponent]
    public sealed class WorldLoot : MonoBehaviour
    {
        public static readonly HashSet<WorldLoot> Active=new HashSet<WorldLoot>();
        public HumanAgent CarriedBy { get; private set; }
        public bool ConsumedByHero { get; private set; }
        HumanAgent claimant;
        float claimUntil;
        Vector3 pickupOrigin;
        public int Kind => GetComponent<SparePart>() is SparePart part?part.Kind:GetComponent<ExpeditionPickup>().Kind;
        public bool Owned => GetComponent<ExpeditionPickup>() is ExpeditionPickup item && item.Owned;
        public bool Available => gameObject.activeInHierarchy && !CarriedBy && !ConsumedByHero
            && EscapeGame.Instance && EscapeGame.Instance.CanTakeWorldLoot(this);
        public HumanAgent ClaimedBy => claimant && claimant.enabled && claimant.gameObject.activeInHierarchy
            && !claimant.Downed && !claimant.IsDetained && Time.time<claimUntil?claimant:null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry()=>Active.Clear();
        void OnEnable()=>Active.Add(this);
        void OnDisable() { Active.Remove(this); claimant=null; claimUntil=0; }
        public bool TryReserve(HumanAgent actor)
        {
            if(!Available || !actor || !actor.enabled || actor.Role!=HumanRole.Marauder || actor.Downed || actor.IsDetained
                || actor.LootInventory.Count>=3 || (ClaimedBy && ClaimedBy!=actor)) return false;
            claimant=actor; claimUntil=Time.time+.75f; return true;
        }
        public void Release(HumanAgent actor) { if(claimant==actor) { claimant=null; claimUntil=0; } }
        public bool TakeForHero()
        {
            if(!Available || !Sight.Clear(ActorPosture.Eye(EscapeGame.Instance.Hero),transform.position+Vector3.up*.15f)) return false;
            ConsumedByHero=true; gameObject.SetActive(false); return true;
        }
        public bool TakeForMarauder(HumanAgent actor)
        {
            if(!Available || ClaimedBy!=actor || actor.CurrentTarget || Vector3.Distance(actor.transform.position,transform.position)>1.7f
                || !Sight.Clear(ActorPosture.Eye(actor.transform),transform.position+Vector3.up*.15f) || !actor.StashLoot(this)) return false;
            pickupOrigin=transform.position; CarriedBy=actor; gameObject.SetActive(false); return true;
        }
        public bool Drop(HumanAgent actor,int index)
        {
            if(CarriedBy!=actor) return false;
            Vector3 center=actor.transform.position; center.y=0;
            bool found=false; Vector3 chosen=center;
            for(int ring=1;ring<=6 && !found;ring++)
                for(int step=0;step<12;step++)
                {
                    Vector3 point=center+Quaternion.Euler(0,step*30+index*75,0)*Vector3.forward*(.7f*ring);
                    if(!NpcTactics.Walkable(point)) continue;
                    chosen=point; found=true; break;
                }
            if(!found)
            {
                chosen=pickupOrigin; chosen.y=0; found=NpcTactics.Walkable(chosen);
                for(int x=-28;x<=28 && !found;x+=2)
                    for(int z=-22;z<=22;z+=2)
                        if(NpcTactics.Walkable(new Vector3(x,0,z))) { chosen=new Vector3(x,0,z); found=true; break; }
            }
            if(!found) return false;
            transform.position=chosen+Vector3.up*.45f; CarriedBy=null; claimant=null; claimUntil=0;
            gameObject.SetActive(true); return true;
        }
    }
    public sealed partial class EscapeGame
    {
        public bool CanTakeWorldLoot(WorldLoot item)=>item.Kind>=0 && item.Kind<=5 && (!item.Owned || shopUnlocked);
        public SparePart SpawnLoosePart(int kind,Vector3 position)
        {
            if(kind<0 || kind>3) throw new System.ArgumentOutOfRangeException(nameof(kind));
            var root=Box(partNames[kind],position,new Vector3(.9f,.9f,.9f),amber,false);
            var part=root.AddComponent<SparePart>(); part.Kind=kind; Parts.Add(part);
            Label(partNames[kind],position+Vector3.up*1.3f,Color.white,.18f).transform.SetParent(root.transform,true);
            if(Fog) Fog.RegisterDynamic(root); return part;
        }
    }
}
