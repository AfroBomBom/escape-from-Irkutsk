using UnityEngine;
namespace IrkutskPrototype
{
    public sealed partial class HumanAgent
    {
        public CoverSlot SelectedCover { get; private set; }
        CoverSlot rejectedCover;
        float coverSeekAt,coverArriveUntil,nextPeekAt,peekUntil,rejectedUntil;
        bool coverUsedThisTick,peeking;
        Vector3 peekGoal;
        bool CanContinueCover()
        {
            if(!CurrentTarget || Time.time>=searchUntil || (IsPatrol && !Game.Threat(CurrentTarget))) return false;
            if(SelectedCover) return true;
            if(targetVisible || (IsPatrol && !Game.Threat(CurrentTarget))) return false;
            foreach(var slot in CoverSlot.Active)
                if(slot && (!slot.Owner || slot.Owner==this) && Vector3.Distance(transform.position,slot.transform.position)<9 && slot.ProtectsFrom(lastSeen)) return true;
            return false;
        }
        void ContinueCover()
        { if(Role==HumanRole.Marauder && TryPressWoundedHero()) return; if(!TryUseRegisteredCover()) Search(); }
        void ReleaseCover()
        {
            if(SelectedCover) SelectedCover.Release(this);
            SelectedCover=null; peeking=false; nextPeekAt=0;
            var posture=GetComponent<ActorPosture>();
            if(posture && posture.IsCrouched && controller && controller.enabled && gameObject.activeInHierarchy && !Downed
                && !(rescueUsedThisTick && rescuePatient)) posture.TrySetCrouched(false);
        }
        void OnDisable()
        { CancelLoot(); CancelRescue(); ReleaseIncomingRescue(); ReleaseCover(); TacticalTask=PatrolTask.None; taskUntil=0; taskFrame=-1; }
        bool TryUseRegisteredCover()
        {
            if(!CurrentTarget) return false;
            Vector3 threat=lastSeen; // Never query the hidden target's current position.
            if(SelectedCover && (!MedicCoverAllowed(SelectedCover) || !SelectedCover.ProtectsFrom(threat) || !SelectedCover.TryReserve(this))) ReleaseCover();
            if(!SelectedCover)
            {
                if(Time.time<coverSeekAt) return false;
                coverSeekAt=Time.time+.8f;
                CoverSlot best=null; float bestDistance=9;
                foreach(var slot in CoverSlot.Active)
                {
                    if(!slot || !MedicCoverAllowed(slot) || (slot==rejectedCover && Time.time<rejectedUntil) || (slot.Owner && slot.Owner!=this) || !slot.ProtectsFrom(threat)) continue;
                    float distance=Vector3.Distance(transform.position,slot.transform.position);
                    if(distance>=bestDistance || (distance>.7f && !NpcTactics.ClearStep(transform.position,slot.transform.position) && GridRoute.Find(transform.position,slot.transform.position).Count==0)) continue;
                    best=slot; bestDistance=distance;
                }
                if(!best || !best.TryReserve(this)) return false;
                SelectedCover=best; coverArriveUntil=Time.time+7; nextPeekAt=0; peeking=false;
            }
            coverUsedThisTick=true;
            var posture=GetComponent<ActorPosture>();
            if(peeking)
            {
                if(!posture.TrySetCrouched(false)) { peeking=false; nextPeekAt=Time.time+2; return true; }
                State="Выглядываю из укрытия"; MoveCombat(peekGoal);
                if(Vector3.Distance(transform.position,peekGoal)<.45f && targetVisible && NpcTactics.FiringLane(transform,CurrentTarget) && Time.time>=shotAt)
                {
                    bool canShoot=Role!=HumanRole.Medic || !Game.IsHero(CurrentTarget) || (!Game.IsDriving && Vector3.Distance(transform.position,CurrentTarget.position)<7);
                    if(canShoot)
                    {
                        if(Role==HumanRole.Marauder) Game.ObserveAggression(transform);
                        Game.NpcShot(this,CurrentTarget); shotAt=Time.time+(Role==HumanRole.Soldier?.8f:Role==HumanRole.Medic?(HasHeroHealthAdvantage?3:5):1.2f);
                    }
                }
                if(Time.time>=peekUntil) { peeking=false; nextPeekAt=Time.time+2; coverArriveUntil=Time.time+5; }
                return true;
            }
            if(Vector3.Distance(transform.position,SelectedCover.transform.position)>.45f)
            {
                if(Time.time>coverArriveUntil)
                { rejectedCover=SelectedCover; rejectedUntil=Time.time+3; ReleaseCover(); return false; }
                posture.TrySetCrouched(false); State="Иду к укрытию"; MoveCombat(SelectedCover.transform.position); return true;
            }
            posture.TrySetCrouched(SelectedCover.Cover.Low); State="В укрытии";
            if(nextPeekAt<=0) nextPeekAt=Time.time+1;
            if(Time.time>=nextPeekAt)
            {
                if(IsPatrol && PartnerMoving()) { nextPeekAt=Time.time+.3f; return true; }
                bool found=false;
                foreach(bool left in new[]{true,false})
                {
                    Vector3 point=SelectedCover.Peek(left);
                    if(!NpcTactics.ClearStep(transform.position,point) || !Sight.Clear(point+Vector3.up*1.3f,threat+Vector3.up*1.5f)) continue;
                    peekGoal=point; found=true; break;
                }
                if(found) { peeking=true; peekUntil=Time.time+1.8f; }
                else nextPeekAt=Time.time+1;
            }
            return true;
        }
    }
}
