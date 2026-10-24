using UnityEngine;
namespace IrkutskPrototype
{
    public sealed partial class HumanAgent
    {
        Vector3 medicRearGoal;
        float medicRearPlanAt;
        HumanAgent CombatLeader()
        {
            if(Role!=HumanRole.Medic || !CurrentTarget || !Game.Threat(CurrentTarget) || Time.time>=searchUntil) return null;
            var leader=Partner;
            return leader && leader.Role==HumanRole.Soldier && leader.CurrentTarget==CurrentTarget?leader:null;
        }
        bool LeaderRetreating(HumanAgent leader)=>leader.TacticalTask==PatrolTask.Retreat || leader.Health<35;
        bool MedicMustFollowRetreat()
        {
            var leader=CombatLeader();
            return leader && LeaderRetreating(leader) && !(targetVisible && Vector3.Distance(transform.position,lastSeen)<2.5f);
        }
        bool MedicCoverAllowed(CoverSlot slot)
        {
            var leader=CombatLeader(); if(!leader) return true;
            Vector3 away=leader.transform.position-lastSeen; away.y=0;
            return away.sqrMagnitude>.01f && Vector3.Dot(slot.transform.position-leader.transform.position,away.normalized)>=1.5f;
        }
        bool MedicRearSupport()
        {
            var leader=CombatLeader(); if(!leader) return false;
            Vector3 away=leader.transform.position-lastSeen; away.y=0;
            if(away.sqrMagnitude<.01f) away=-transform.forward;
            away.Normalize(); Vector3 side=Vector3.Cross(Vector3.up,away);
            Vector3 offset=transform.position-leader.transform.position; offset.y=0;
            float behind=Vector3.Dot(offset,away);
            bool retreat=LeaderRetreating(leader);
            bool correct=behind<1.8f || offset.magnitude>4.5f || (retreat && behind<2.8f);
            if(!correct)
            {
                if(!retreat && TryUseRegisteredCover()) { SetTask(PatrolTask.Cover,SelectedCover.transform.position); return true; }
                SetTask(PatrolTask.Hold,transform.position); State=retreat?"Держу тыл при отходе":"Позади солдата";
                if(targetVisible) FaceTarget(); FireIfSafe(); return true;
            }
            if(!retreat && behind>=-.3f && PartnerMoving()) { HoldAndCover(); return true; }
            ReleaseCover();
            if(Time.time>=medicRearPlanAt || !NpcTactics.ClearStep(transform.position,medicRearGoal))
            {
                medicRearPlanAt=Time.time+.35f;
                float sign=Vector3.Dot(offset,side)>=0?1:-1;
                medicRearGoal=transform.position; float best=float.MaxValue;
                foreach(float depth in new[]{2.8f,3.5f,2f})
                    foreach(float lateral in new[]{1.4f*sign,-1.4f*sign,2.4f*sign,-2.4f*sign})
                    {
                        Vector3 point=leader.transform.position+away*depth+side*lateral;
                        if(!NpcTactics.ClearStep(transform.position,point)) continue;
                        float score=Vector3.Distance(transform.position,point)+Mathf.Abs(depth-2.8f)*2;
                        if(score<best) { best=score; medicRearGoal=point; }
                    }
                // No safe straight correction: avoid advancing and use the existing retreat sampler.
                if(best==float.MaxValue && behind<1.8f) medicRearGoal=NpcTactics.RetreatPoint(transform.position,lastSeen);
            }
            SetTask(PatrolTask.FollowRear,medicRearGoal); State=retreat?"Отхожу за солдатом":"Занимаю тыл солдата";
            Quaternion facing=transform.rotation; MoveTo(medicRearGoal,.45f,2.4f); transform.rotation=facing;
            if(targetVisible) FaceTarget(); FireIfSafe(); return true;
        }
        public bool ReceiveHealingDart(float amount)
        {
            if(Downed || IsIncapacitated || IsDetained || !gameObject.activeInHierarchy || Role!=HumanRole.Soldier) return false;
            float before=Health; Health=Mathf.Min(100,Health+Mathf.Max(0,amount)); return Health>before;
        }
    }
}
