using UnityEngine;
namespace IrkutskPrototype
{
    public sealed partial class HumanAgent
    {
        Transform soloCapturePatient;
        public bool HasCaptureMedic => CaptureMedic;
        HumanAgent CaptureMedic
        {
            get
            {
                HumanAgent nearest=null; float best=12;
                foreach(var person in Game.Humans)
                {
                    if(!person || person.Role!=HumanRole.Medic || !person.enabled || !person.gameObject.activeInHierarchy
                        || person.Downed || person.IsIncapacitated || person.IsDetained || person.IsStunned || person.RescuePatient) continue;
                    float distance=Vector3.Distance(transform.position,person.transform.position);
                    if(distance>best || !Sight.VisibleBody(ActorPosture.Eye(transform),person.transform)) continue;
                    best=distance; nearest=person;
                }
                return nearest;
            }
        }
        bool AvailableCaptureTarget()
        {
            if(!CurrentTarget || !targetVisible || !Game.Alive(CurrentTarget)) return false;
            var person=CurrentTarget.GetComponent<HumanAgent>(); if(person && person.IsPatrol) return false;
            return !Game.IsHero(CurrentTarget) || (!Game.IsDriving && !Game.HeroCaptureImmune);
        }
        bool CanSoldierRestrain()
        {
            if(Role!=HumanRole.Soldier || !AvailableCaptureTarget()) return false;
            // Finish an existing contact rather than changing the captor halfway through a QTE.
            return Game.IsBindingHero(this) || (soloCapturePatient==CurrentTarget && bindTime>0) || !CaptureMedic;
        }
        bool ShouldGuardCapture()
        {
            if(Role!=HumanRole.Soldier || Game.IsBindingHero(this) || (soloCapturePatient==CurrentTarget && bindTime>0) || !AvailableCaptureTarget()) return false;
            var medic=CaptureMedic; if(!medic) return false;
            // Keep normal combat against active threats until the medic commits to a capture.
            return !Game.Threat(CurrentTarget) || medic.dartPatient==CurrentTarget || Game.IsBindingHero(medic);
        }
        void RestrainSoloTarget()
        {
            soloCapturePatient=CurrentTarget; RestrainTarget(CurrentTarget);
            if(!Game.Alive(soloCapturePatient)) soloCapturePatient=null;
        }
        void GuardCapture()
        {
            var medic=CaptureMedic; if(!medic) return;
            ReleaseCover(); SetTask(PatrolTask.Hold,transform.position); State="Прикрываю задержание";
            FaceTarget();
            // Follow the doctor, leaving space for the binding; do not shoot the capture patient.
            MoveTo(medic.transform.position,3.5f);            Transform danger=null; float best=Sight.HumanRange;
            void Consider(Transform target)
            {
                if(target==CurrentTarget || !Game.Alive(target) || !Game.Threat(target)
                    || !Sight.CanSee(transform,target,Sight.HumanRange,Sight.HumanAngle)) return;
                float distance=Vector3.Distance(transform.position,target.position);
                if(distance<=best) { best=distance; danger=target; }
            }
            foreach(var infected in Game.Zombies) if(infected) Consider(infected.transform);
            foreach(var person in Game.Humans) if(person && !person.IsPatrol) Consider(person.transform);
            if(danger && Time.time>=shotAt && NpcTactics.FiringLane(transform,danger))
            { Game.NpcShot(this,danger); shotAt=Time.time+.8f; }
        }
    }
}
