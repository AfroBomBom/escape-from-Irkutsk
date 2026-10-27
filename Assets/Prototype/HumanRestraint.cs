using UnityEngine;
namespace IrkutskPrototype
{
    public sealed partial class HumanAgent
    {
        Transform dartPatient;
        float dartPursuitUntil;
        public bool IsStunned => Time.time<stunnedUntil;
        public void TrackDartPatient(Transform patient)
        {
            if(Role!=HumanRole.Medic || !patient || !Game.Alive(patient)) return;
            dartPatient=patient; dartPursuitUntil=Time.time+12; bindTime=0;
            CurrentTarget=patient; lastSeen=patient.position; searchUntil=Time.time+4;
            routeAt=0; route.Clear(); ReleaseCover();
        }
        public void CancelDartRestraint() { dartPatient=null; bindTime=0; }
        bool CanRestrainDartPatient()
        {
            if(Role!=HumanRole.Medic || !dartPatient) return false;
            if((Time.time>=dartPursuitUntil && bindTime<=0 && !Game.IsBindingHero(this)) || !Game.Alive(dartPatient)
                || (Game.IsHero(dartPatient) && (Game.IsDriving || Game.HeroCaptureImmune)))
            { CancelDartRestraint(); return false; }
            // Never follow the current position of an unseen patient through a wall.
            if(!Sight.CanSee(transform,dartPatient,Sight.HumanRange,Sight.HumanAngle))
            { CancelDartRestraint(); return false; }
            return true;
        }
        void RestrainDartPatient() => RestrainTarget(dartPatient);
        void RestrainTarget(Transform patient)
        {
            CurrentTarget=patient; FaceTarget(); ReleaseCover();
            State="К задержанию";
            if(Vector3.Distance(transform.position,patient.position)>1.5f)
            { bindTime=0; MoveTo(patient.position,1.1f,2.7f); return; }
            State="Связываю";
            if(Game.IsHero(patient))
            {
                if(Game.TryBindHero(this)) State=Game.HeroStruggling?"Борюсь: связываю":"Связываю героя";
                return;
            }
            var heldPerson=patient.GetComponent<HumanAgent>(); if(heldPerson) heldPerson.Stun(.15f);
            var heldInfected=patient.GetComponent<Infected>(); if(heldInfected) heldInfected.HoldForRestraint();
            bindTime+=Time.deltaTime;
            if(bindTime<3) return;
            var person=patient.GetComponent<HumanAgent>(); if(person) person.Detain();
            var infected=patient.GetComponent<Infected>(); if(infected) infected.Restrain();
            CancelDartRestraint(); CurrentTarget=null;
        }
    }
}
