using UnityEngine;
namespace IrkutskPrototype
{
    public sealed partial class HumanAgent
    {
        Vector3 home,fearPoint,retreatGoal;
        float fearUntil,tacticsAt;
        public void HearGunfire(Vector3 origin)
        {
            if(Role!=HumanRole.Civilian || Downed || IsDetained || !gameObject.activeInHierarchy) return;
            float range=Physics.Linecast(transform.position+Vector3.up,origin+Vector3.up,EscapeGame.SolidMask)?3:8;
            if(Vector3.Distance(transform.position,origin)<=range) Frighten(origin);
        }
        void Frighten(Vector3 origin) { fearPoint=origin; fearUntil=Time.time+6; }
        void ObserveCivilian()
        {
            float closest=8;
            void Notice(Transform target)
            {
                if(!target.gameObject.activeInHierarchy || !Sight.CanSee(transform,target.position,8,Sight.HumanAngle)) return;
                float d=Vector3.Distance(transform.position,target.position);
                if(d<closest) { closest=d; Frighten(target.position); }
            }
            foreach(var z in Game.Zombies) if(!z.Downed) Notice(z.transform);
            foreach(var h in Game.Humans) if(h.Role==HumanRole.Marauder && !h.Downed) Notice(h.transform);
        }
        void Flee()
        {
            State="Бегу от опасности";
            if(Time.time>=tacticsAt) { retreatGoal=NpcTactics.RetreatPoint(transform.position,fearPoint); tacticsAt=Time.time+.8f; }
            MoveTo(retreatGoal,.4f);
        }
        void ReturnHome()
        { if(Vector3.Distance(transform.position,home)>1) { State="Возвращаюсь"; MoveTo(home,.8f); } else State="Мирный"; }
        bool NeedsSupport()
        {
            if(Role!=HumanRole.Medic || Game.Soldier.Downed || Game.Soldier.Health>=65) return false;
            if(Vector3.Distance(transform.position,Game.Soldier.transform.position)>12) return false;
            return !targetVisible || !CurrentTarget || !Game.Threat(CurrentTarget) || Vector3.Distance(transform.position,CurrentTarget.position)>7;
        }
        void AssistPartner()
        { State="Лечу напарника"; MoveTo(Game.Soldier.transform.position,2.3f); }
        void MoveCombat(Vector3 goal)
        {
            Quaternion facing=transform.rotation;
            MoveTo(goal,.35f); transform.rotation=facing;
            if(CurrentTarget && targetVisible) FaceTarget();
        }
        void RetreatFighting()
        {
            State="Отступаю к укрытию";
            if(Time.time>=tacticsAt) { retreatGoal=NpcTactics.RetreatPoint(transform.position,lastSeen); tacticsAt=Time.time+.9f; }
            MoveCombat(retreatGoal);
            bool mayFire=CurrentTarget && (Role!=HumanRole.Medic || !Game.IsHero(CurrentTarget) || (!Game.IsDriving && Vector3.Distance(transform.position,CurrentTarget.position)<7));
            if(targetVisible && mayFire && NpcTactics.FiringLane(transform,CurrentTarget) && Time.time>=shotAt)
            {
                if(Role==HumanRole.Marauder) Game.ObserveAggression(transform);
                Game.NpcShot(this,CurrentTarget); shotAt=Time.time+(Role==HumanRole.Soldier?.8f:Role==HumanRole.Medic?5:1.2f);
            }
        }
        bool RepositionIfBlocked()
        {
            if(NpcTactics.FiringLane(transform,CurrentTarget)) return false;
            State="Ищу линию огня";
            if(Time.time>=tacticsAt) { retreatGoal=NpcTactics.FiringStep(transform,CurrentTarget); tacticsAt=Time.time+.6f; }
            MoveCombat(retreatGoal); return true;
        }
        void ObserveMarauder()
        {
            Transform seen=null; float best=float.MaxValue;
            void Consider(Transform candidate)
            {
                if(!Game.Alive(candidate) || !Sight.CanSee(transform,candidate.position,12,Sight.HumanAngle)) return;
                float score=Vector3.Distance(transform.position,candidate.position)-(candidate==CurrentTarget?3:0);
                if(score<best) { best=score; seen=candidate; }
            }
            Consider(Game.IsDriving?Game.Car:Game.Hero);
            foreach(var h in Game.Humans) if(h.IsPatrol) Consider(h.transform);
            foreach(var z in Game.Zombies) Consider(z.transform);
            if(seen) { CurrentTarget=seen; lastSeen=seen.position; searchUntil=Time.time+4; }
            else if(!Game.Alive(CurrentTarget) || Time.time>=searchUntil) CurrentTarget=null;
            targetVisible=seen && CurrentTarget==seen;
        }
        void MarauderFight()
        {
            State="Атака"; FaceTarget();
            if(RepositionIfBlocked()) return;
            if(Time.time>=shotAt) { Game.ObserveAggression(transform); Game.NpcShot(this,CurrentTarget); shotAt=Time.time+1.2f; }
            if(Vector3.Distance(transform.position,CurrentTarget.position)<3)
            { retreatGoal=NpcTactics.RetreatPoint(transform.position,lastSeen); MoveCombat(retreatGoal); }
            else MoveTo(CurrentTarget.position,6);
        }
    }
}
