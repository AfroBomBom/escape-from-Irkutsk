using UnityEngine;
namespace IrkutskPrototype
{
    public enum PatrolTask { None, Hold, Advance, Retreat, Reposition, Cover, Support }
    public sealed partial class HumanAgent
    {
        public PatrolTask TacticalTask { get; private set; }
        public float TaskHoldRemaining => Mathf.Max(0,taskUntil-Time.time);
        float taskUntil,lastDamageAt=-1000,taskProgressAt,taskDistance;
        int taskFrame=-1;
        Vector3 taskGoal;
        Transform taskTarget;

        HumanAgent Partner
        {
            get
            {
                var other=Role==HumanRole.Soldier?Game.Medic:Game.Soldier;
                return other && other.enabled && other.gameObject.activeInHierarchy && !other.Downed && !other.IsDetained
                    && Time.time>=other.stunnedUntil && Vector3.Distance(transform.position,other.transform.position)<=12?other:null;
            }
        }
        bool ChangingPosition => TacticalTask==PatrolTask.Advance || TacticalTask==PatrolTask.Retreat || TacticalTask==PatrolTask.Reposition
            || (TacticalTask==PatrolTask.Support && Vector3.Distance(transform.position,taskGoal)>2.5f)
            || (TacticalTask==PatrolTask.Cover && (peeking || (SelectedCover && Vector3.Distance(transform.position,SelectedCover.transform.position)>.5f)));
        bool PartnerMoving()
        {
            var partner=Partner;
            return partner && Time.frameCount-partner.taskFrame<=1 && partner.ChangingPosition;
        }
        void SetTask(PatrolTask task,Vector3 goal)
        {
            if(TacticalTask!=task || taskTarget!=CurrentTarget || Vector3.Distance(taskGoal,goal)>.5f)
            {
                taskUntil=Time.time+.9f; taskProgressAt=Time.time; taskDistance=Vector3.Distance(transform.position,goal);
            }
            TacticalTask=task; taskGoal=goal; taskTarget=CurrentTarget; taskFrame=Time.frameCount;
        }
        bool KnownDangerAt(Vector3 point)
        {
            if(!CurrentTarget || Time.time>=searchUntil || !Game.Threat(CurrentTarget)) return false;
            // Reports and sight supply snapshots. Do not read an unseen enemy's live position.
            return Vector3.Distance(point,lastSeen)<7 && Sight.Clear(lastSeen+Vector3.up*1.3f,point+Vector3.up*1.1f);
        }
        bool SafeToTreat(HumanAgent patient)
        {
            if(Time.time-lastDamageAt<1.25f || Time.time-patient.lastDamageAt<1.25f) return false;
            if(KnownDangerAt(transform.position) || KnownDangerAt(patient.transform.position)) return false;
            var partner=Partner;
            return !partner || (!partner.KnownDangerAt(transform.position) && !partner.KnownDangerAt(patient.transform.position));
        }
        bool SafeSupportPath(Vector3 destination)
        {
            if(!NpcTactics.ClearStep(transform.position,destination)) return false;
            var partner=Partner;
            for(int i=1;i<=4;i++)
            {
                Vector3 sample=Vector3.Lerp(transform.position,destination,i/4f);
                if(KnownDangerAt(sample) || (partner && partner.KnownDangerAt(sample))) return false;
            }
            return true;
        }
        void TrySafeHealing()
        {
            if(Role!=HumanRole.Medic || Time.time<healAt) return;
            bool self=Health<100 && SafeToTreat(this);
            var soldier=Game.Soldier;
            bool other=soldier && soldier.gameObject.activeInHierarchy && !soldier.Downed && !soldier.IsDetained && soldier.Health<100
                && Vector3.Distance(transform.position,soldier.transform.position)<3 && SafeToTreat(soldier)
                && Sight.Clear(ActorPosture.Chest(transform),ActorPosture.Chest(soldier.transform));
            if(!self && !other) return;
            if(self) Health=Mathf.Min(100,Health+15);
            if(other) soldier.Health=Mathf.Min(100,soldier.Health+15);
            healAt=Time.time+8;
        }
        void FireIfSafe()
        {
            if(!CurrentTarget || !targetVisible || Time.time<shotAt || !NpcTactics.FiringLane(transform,CurrentTarget)) return;
            bool hero=Game.IsHero(CurrentTarget);
            if(Role==HumanRole.Medic && hero && (Game.IsDriving || Vector3.Distance(transform.position,lastSeen)>=7)) return;
            Game.NpcShot(this,CurrentTarget); shotAt=Time.time+(Role==HumanRole.Soldier?.8f:hero?5:1.3f);
        }
        void HoldAndCover()
        {
            SetTask(PatrolTask.Hold,transform.position);
            State=Partner?"Прикрываю напарника":"Удерживаю позицию";
            if(targetVisible) FaceTarget();
            FireIfSafe();
        }
        void PatrolCombat()
        {
            if(!CurrentTarget) { TacticalTask=PatrolTask.None; return; }
            float distance=Vector3.Distance(transform.position,lastSeen);
            bool emergency=targetVisible && distance<2.5f;
            bool wounded=Health<(Role==HumanRole.Medic?50:35);
            // Immediate danger overrides both the decision hold and movement coordination.
            if(emergency)
            {
                ReleaseCover(); coverSeekAt=Time.time+1;
                if(TacticalTask!=PatrolTask.Retreat || Time.time>=taskUntil)
                    SetTask(PatrolTask.Retreat,NpcTactics.RetreatPoint(transform.position,lastSeen));
                taskFrame=Time.frameCount; State="Отхожу от угрозы"; MoveCombat(taskGoal); FireIfSafe(); return;
            }
            if(!wounded && PartnerMoving() && (!SelectedCover || (!peeking && Vector3.Distance(transform.position,SelectedCover.transform.position)>.5f)))
            { HoldAndCover(); return; }
            if(TryUseRegisteredCover()) { SetTask(PatrolTask.Cover,SelectedCover.transform.position); return; }
            if(wounded)
            {
                if(TacticalTask!=PatrolTask.Retreat || Time.time>=taskUntil)
                    SetTask(PatrolTask.Retreat,NpcTactics.RetreatPoint(transform.position,lastSeen));
                taskFrame=Time.frameCount; State="Отступаю к укрытию"; MoveCombat(taskGoal); FireIfSafe(); return;
            }
            if(!targetVisible) { TacticalTask=PatrolTask.None; Search(); return; }
            if(!NpcTactics.FiringLane(transform,CurrentTarget))
            {
                if(TacticalTask!=PatrolTask.Reposition || Time.time>=taskUntil || !NpcTactics.ClearStep(transform.position,taskGoal))
                    SetTask(PatrolTask.Reposition,NpcTactics.FiringStep(transform,CurrentTarget));
                taskFrame=Time.frameCount; State="Ищу линию огня"; MoveCombat(taskGoal); return;
            }
            FireIfSafe();
            var partner=Partner;
            // The soldier guards a nearby treatment, even when already healed this frame.
            if(Role==HumanRole.Soldier && partner && partner.Role==HumanRole.Medic
                && (partner.NeedsSupport() || (partner.TacticalTask==PatrolTask.Support && Time.frameCount-partner.taskFrame<=1)))
            { HoldAndCover(); return; }
            bool locked=taskTarget==CurrentTarget && Time.time<taskUntil;
            if(locked && TacticalTask==PatrolTask.Hold) { HoldAndCover(); return; }
            float range=Role==HumanRole.Soldier?6:4;
            if(distance<=range+1) { HoldAndCover(); return; }
            if(!locked || TacticalTask!=PatrolTask.Advance)
            {
                Vector3 toward=lastSeen-transform.position; toward.y=0;
                Vector3 point=transform.position+toward.normalized*Mathf.Min(2,distance-range);
                if(!NpcTactics.ClearStep(transform.position,point)) { HoldAndCover(); return; }
                SetTask(PatrolTask.Advance,point);
            }
            float remaining=Vector3.Distance(transform.position,taskGoal);
            if(remaining<taskDistance-.2f) { taskDistance=remaining; taskProgressAt=Time.time; }
            if(remaining<.45f || Time.time-taskProgressAt>1.3f || !NpcTactics.ClearStep(transform.position,taskGoal))
            { SetTask(PatrolTask.Hold,transform.position); HoldAndCover(); return; }
            taskFrame=Time.frameCount; State="Сближаюсь под прикрытием"; MoveCombat(taskGoal);
        }
    }
}
