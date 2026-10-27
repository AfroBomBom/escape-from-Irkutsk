using UnityEngine;
namespace IrkutskPrototype
{
    public enum NoiseKind { Unknown, Movement, Work, Impact, Engine, Gunshot }

    public sealed partial class HumanAgent
    {
        Vector3 soundPoint,checkedSoundPoint;
        float heardAt=-10,soundSuspicion,soundUntil,soundReadyAt,inspectUntil,noiseAcceptedAt=-10,checkedSoundUntil,reportAt;
        float gunfireSearchUntil;
        public bool HasSoundClue=>Time.time<soundUntil;
        public Vector3 LastHeardPosition=>soundPoint;

        public void HearNoise(Vector3 origin,float radius,Transform source=null,NoiseKind kind=NoiseKind.Unknown)
        {
            if(!enabled || !gameObject.activeInHierarchy || Downed || IsIncapacitated || IsDetained || Role==HumanRole.Civilian || source==transform || radius<=0) return;
            var person=source?source.GetComponent<HumanAgent>():null;
            if(IsPatrol && person && person.IsPatrol) return;
            if(CurrentTarget && targetVisible) return;
            if(kind!=NoiseKind.Gunshot && (Time.time-noiseAcceptedAt<.25f || (HasSoundClue && Time.time<gunfireSearchUntil))) return;
            float reach=Mathf.Min(radius,14);
            bool muffled=Physics.Linecast(transform.position+Vector3.up,origin+Vector3.up,EscapeGame.SolidMask);
            if(muffled) reach*=.4f;
            Vector3 delta=origin-transform.position; delta.y=0;
            if(delta.magnitude>reach) return;
            Vector3 estimate=EstimateSound(origin);
            if(kind!=NoiseKind.Gunshot && Time.time<checkedSoundUntil && Vector3.Distance(estimate,checkedSoundPoint)<3) return;
            noiseAcceptedAt=Time.time;
            bool sameArea=Vector3.Distance(estimate,soundPoint)<3;
            if(Time.time-heardAt>2.5f || !sameArea) soundSuspicion=0;
            bool ongoing=HasSoundClue && sameArea;
            soundPoint=estimate; heardAt=Time.time;
            soundSuspicion=Mathf.Min(2,soundSuspicion+(kind==NoiseKind.Gunshot || radius>=6?1:.55f));
            if(soundSuspicion<1) return;
            if(kind==NoiseKind.Gunshot) { gunfireSearchUntil=Time.time+20; inspectUntil=0; }
            soundUntil=kind==NoiseKind.Gunshot?gunfireSearchUntil:Time.time+8;
            if(!ongoing) { soundReadyAt=Time.time+.4f; inspectUntil=0; }
        }
        // A passing bullet is audible locally even when its muzzle is beyond hearing range.
        public void HearPassingShot(Vector3 origin,Vector3 end)
        {
            if(!IsPatrol || !enabled || !gameObject.activeInHierarchy || Downed || IsIncapacitated || IsDetained) return;
            if(CurrentTarget && targetVisible) return;
            Vector3 flight=end-origin; float length=flight.magnitude; if(length<.01f) return;
            Vector3 direction=flight/length, ear=ActorPosture.Eye(transform);
            float along=Vector3.Dot(ear-origin,direction);
            if(along<=0 || along>length) return;
            Vector3 nearest=origin+direction*along;
            if((nearest-ear).sqrMagnitude>16 || !Sight.Clear(nearest,ear)) return;
            if(CurrentTarget && Game.Threat(CurrentTarget)) return;
            CurrentTarget=null;
            soundPoint=EstimateSound(origin); heardAt=noiseAcceptedAt=Time.time; soundSuspicion=2;
            gunfireSearchUntil=soundUntil=Time.time+20; soundReadyAt=Time.time+.2f; inspectUntil=0;
            routeAt=0; route.Clear(); State="Слышу пролёт пули";
        }
        Vector3 EstimateSound(Vector3 origin)
        {
            // Store an approximate place, never a source actor or its hidden position.
            Vector3 point=new Vector3(Mathf.Round(origin.x/2)*2,transform.position.y,Mathf.Round(origin.z/2)*2);
            if(NpcTactics.Walkable(point)) return point;
            for(int i=0;i<8;i++)
            {
                Vector3 nearby=point+Quaternion.Euler(0,i*45,0)*Vector3.forward*1.5f;
                if(NpcTactics.Walkable(nearby)) return nearby;
            }
            return point;
        }
        bool ListeningToWeakSound()=>soundSuspicion>0 && Time.time-heardAt<2.5f;
        void ListenToSound()
        {
            State="Прислушиваюсь";
            Vector3 delta=soundPoint-transform.position; delta.y=0;
            if(delta.sqrMagnitude>.1f) transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(delta),45*Time.deltaTime);
        }
        void InvestigateSound()
        {
            if(Time.time<soundReadyAt) { ListenToSound(); return; }
            if(Vector3.Distance(transform.position,soundPoint)>1.2f)
            { State="Проверяю шум"; MoveTo(soundPoint,1.1f,1.35f); return; }
            State="Осматриваю источник шума";
            if(inspectUntil<=0) inspectUntil=Time.time<gunfireSearchUntil?gunfireSearchUntil:Time.time+2;
            transform.Rotate(0,90*Time.deltaTime,0);
            if(Time.time>=inspectUntil)
            {
                checkedSoundPoint=soundPoint; checkedSoundUntil=Time.time+2.5f;
                ClearSoundClue();
            }
        }
        void ClearSoundClue() { soundUntil=0; gunfireSearchUntil=0; soundSuspicion=0; inspectUntil=0; heardAt=-10; }
        void SawTarget(Transform target)
        {
            ClearSoundClue();
            if(IsPatrol && Time.time>=reportAt)
            { Game.ShareSighting(this,target,target.position); reportAt=Time.time+1.5f; }
        }
        public void ReceiveReport(Transform target,Vector3 reportedPosition)
        {
            if(Downed || IsIncapacitated || IsDetained || !gameObject.activeInHierarchy || !Game.Alive(target)) return;
            // A neutral report must not interrupt a check already in progress.
            if(CurrentTarget && Game.Threat(CurrentTarget) && !Game.Threat(target)) return;
            if(CurrentTarget && targetVisible && (CurrentTarget==target || Game.Threat(CurrentTarget) || !Game.Threat(target))) return;
            CurrentTarget=target; reportedPosition.y=transform.position.y; lastSeen=reportedPosition; searchUntil=Time.time+4;
            scanTime=bindTime=coverTime=0; routeAt=0; route.Clear(); ClearSoundClue();
            State="Получил сообщение";
        }
    }
}
