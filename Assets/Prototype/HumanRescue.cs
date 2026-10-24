using UnityEngine;
namespace IrkutskPrototype
{
    public sealed partial class HumanAgent
    {
        public bool IsIncapacitated { get; private set; }
        public float BleedoutRemaining => IsIncapacitated?Mathf.Max(0,bleedoutAt-Time.time):0;
        public HumanAgent RescuePatient => rescuePatient;
        public float RescueProgress => rescuePatient?Mathf.Clamp01(rescueTime/(Role==HumanRole.Medic?3:6)):0;
        public HumanAgent Rescuer => rescueOwner && rescueOwner.enabled && rescueOwner.gameObject.activeInHierarchy
            && !rescueOwner.Downed && !rescueOwner.IsIncapacitated && !rescueOwner.IsDetained && Time.time>=rescueOwner.stunnedUntil && Time.time<rescueLease?rescueOwner:null;
        HumanAgent rescuePatient,rescueOwner,knownCasualty;
        BoxCollider casualtyBody;
        Vector3 casualtyPoint,rescueGoal;
        Vector3[] rescuePath;
        float bleedoutAt,incapDamage,rescueLease,rescueTime,rescuePlanAt,casualtyUntil,rescueArriveAt;
        int revivals;
        bool fatalPlayerCaused,rescueUsedThisTick;

        void Incapacitate(bool playerCaused)
        {
            CancelRescue(); ReleaseCover(); Health=0; IsIncapacitated=true;
            State="Тяжело ранен"; TacticalTask=PatrolTask.None; taskFrame=-1;
            CurrentTarget=null; targetVisible=false; route.Clear(); ClearSoundClue();
            bleedoutAt=Time.time+30; incapDamage=0; fatalPlayerCaused=playerCaused;
            float yaw=transform.eulerAngles.y;
            for(int i=0;i<4;i++)
            {
                Quaternion rotation=Quaternion.Euler(0,yaw+i*90,0);
                if(Physics.CheckBox(transform.position+rotation*new Vector3(0,.28f,.05f),new Vector3(.35f,.28f,.975f),rotation,EscapeGame.SolidMask)) continue;
                transform.rotation=rotation; break;
            }
            controller.enabled=false; GetComponent<ActorPosture>().SetIncapacitated(true);
            if(!casualtyBody) { casualtyBody=gameObject.AddComponent<BoxCollider>(); casualtyBody.size=new Vector3(.7f,.56f,1.95f); casualtyBody.center=new Vector3(0,.28f,.05f); }
            casualtyBody.enabled=true;
            // A local cry gives allies a fixed casualty location, not hidden enemy knowledge.
            foreach(var ally in Game.Humans)
            {
                if(ally==this || !ally.IsPatrol || ally.Downed || ally.IsIncapacitated || !ally.enabled || !ally.gameObject.activeInHierarchy) continue;
                float reach=Sight.Clear(transform.position+Vector3.up,ally.transform.position+Vector3.up)?12:5;
                if(Vector3.Distance(transform.position,ally.transform.position)>reach) continue;
                ally.knownCasualty=this; ally.casualtyPoint=transform.position; ally.casualtyUntil=Time.time+30;
            }
        }
        void HitIncapacitated(float damage,bool playerCaused)
        {
            if(damage<=0) return;
            lastDamageAt=Time.time; fatalPlayerCaused=playerCaused;
            incapDamage+=damage*(Role==HumanRole.Soldier?.5f:.8f);
            if(Rescuer) Rescuer.CancelRescue();
            if(incapDamage>=20) Die(playerCaused);
        }
        void TickIncapacitation()
        { if(IsIncapacitated && Game && !Game.IsOver && Time.time>=bleedoutAt) Die(fatalPlayerCaused); }
        void ReleaseIncomingRescue()
        {
            var owner=rescueOwner; rescueOwner=null; rescueLease=0;
            if(owner && owner.rescuePatient==this) owner.CancelRescue();
        }
        void Die(bool playerCaused)
        {
            if(Downed) return;
            CancelRescue(); ReleaseIncomingRescue(); ReleaseCover();
            Downed=true; IsIncapacitated=false; Health=0; TacticalTask=PatrolTask.None; CurrentTarget=null;
            DropLoot();
            if(casualtyBody) casualtyBody.enabled=false;
            GetComponent<ActorPosture>().SetIncapacitated(false);
            controller.enabled=false; transform.rotation=Quaternion.Euler(90,transform.eulerAngles.y,0); transform.position+=Vector3.up*.2f;
            State="Погиб";
            if(playerCaused) Game.HumanKilled(this);
            if(Caption) Caption.gameObject.SetActive(false);
        }
        bool SafeRescuePoint(Vector3 point,HumanAgent patient)
        {
            if(Time.time-lastDamageAt<1.25f || Time.time-patient.lastDamageAt<1.25f) return false;
            bool Exposed(HumanAgent observer)
            {
                return observer.CurrentTarget && Time.time<observer.searchUntil && Game.Threat(observer.CurrentTarget)
                    && Vector3.Distance(point,observer.lastSeen)<12
                    && Sight.Clear(observer.lastSeen+Vector3.up*1.3f,point+Vector3.up*.7f);
            }
            if(Exposed(this)) return false;
            foreach(var ally in Game.Humans)
                if(ally!=this && ally.IsPatrol && !ally.Downed && !ally.IsIncapacitated && ally.enabled && ally.gameObject.activeInHierarchy
                    && Vector3.Distance(transform.position,ally.transform.position)<=12 && Exposed(ally)) return false;
            return true;
        }
        bool PlanRescue(HumanAgent patient)
        {
            Vector3 away=transform.position-patient.transform.position; away.y=0;
            if(away.sqrMagnitude<.01f) away=Vector3.back;
            for(int i=0;i<8;i++)
            {
                Vector3 point=patient.transform.position+Quaternion.Euler(0,(i+1)/2*45*(i%2==0?1:-1),0)*away.normalized*1.2f;
                if(!NpcTactics.Walkable(point) || !Sight.Clear(point+Vector3.up*.7f,ActorPosture.Chest(patient.transform))) continue;
                var path=NpcTactics.ClearStep(transform.position,point)?new[]{point}:GridRoute.Find(transform.position,point).ToArray();
                if(path.Length==0 || !SafeRescuePath(path,patient)) continue;
                rescueGoal=point; rescuePath=path; return true;
            }
            return false;
        }
        bool SafeRescuePath(Vector3[] path,HumanAgent patient)
        {
            Vector3 previous=transform.position;
            foreach(var point in path)
            {
                for(int i=1;i<=4;i++) if(!SafeRescuePoint(Vector3.Lerp(previous,point,i/4f),patient)) return false;
                previous=point;
            }
            return true;
        }
        bool CanRescue()
        {
            if(!IsPatrol || Downed || IsIncapacitated || IsDetained) return false;
            HumanAgent chosen=rescuePatient;
            if(!chosen)
            {
                float nearest=12;
                foreach(var patient in Game.Humans)
                {
                    if(!patient.IsPatrol || !patient.IsIncapacitated || !patient.gameObject.activeInHierarchy || patient.Rescuer) continue;
                    float distance=Vector3.Distance(transform.position,patient.transform.position);
                    bool known=patient==knownCasualty && Time.time<casualtyUntil && Vector3.Distance(patient.transform.position,casualtyPoint)<.5f;
                    bool visible=Sight.CanSee(transform,patient.transform,12,Sight.HumanAngle);
                    if(visible) { knownCasualty=patient; casualtyPoint=patient.transform.position; casualtyUntil=Time.time+patient.BleedoutRemaining; }
                    if(distance<nearest && (known || visible)) { nearest=distance; chosen=patient; }
                }
            }
            if(!chosen || !chosen.IsIncapacitated || !chosen.gameObject.activeInHierarchy || chosen.BleedoutRemaining<=0
                || Vector3.Distance(transform.position,chosen.transform.position)>12 || (chosen.Rescuer && chosen.Rescuer!=this)) return false;
            if(!SafeRescuePoint(transform.position,chosen) || !SafeRescuePoint(chosen.transform.position,chosen)) return false;
            if(!rescuePatient && Time.time<rescuePlanAt) return false;
            // Prefer a nearby medic, but never steal an existing first-aid attempt.
            var medic=Game.Medic;
            if(!rescuePatient && Role==HumanRole.Soldier && medic && medic!=chosen && medic.enabled && medic.gameObject.activeInHierarchy
                && !medic.Downed && !medic.IsIncapacitated && !medic.IsDetained && Time.time>=medic.stunnedUntil
                && (!medic.rescuePatient || medic.rescuePatient==chosen)
                && Vector3.Distance(medic.transform.position,chosen.transform.position)<12
                && Sight.VisibleBody(ActorPosture.Eye(medic.transform),chosen.transform)
                && NpcTactics.ClearStep(medic.transform.position,chosen.transform.position) && medic.SafeRescuePoint(medic.transform.position,chosen)) return false;
            if(rescuePatient!=chosen || rescuePath==null || (Time.time>=rescuePlanAt && Vector3.Distance(transform.position,rescueGoal)>.4f))
            {
                rescuePlanAt=Time.time+.5f;
                if(!PlanRescue(chosen)) return false;
            }
            if(!rescuePatient) rescueArriveAt=Time.time+10;
            if(!SafeRescuePath(rescuePath,chosen) || (Time.time>rescueArriveAt && Vector3.Distance(transform.position,chosen.transform.position)>1.65f)) return false;
            rescuePatient=chosen; chosen.rescueOwner=this; chosen.rescueLease=Time.time+.75f; return true;
        }
        void RescuePartner()
        {
            rescueUsedThisTick=true; ReleaseCover(); SetTask(PatrolTask.Rescue,rescueGoal);
            if(Vector3.Distance(transform.position,rescuePatient.transform.position)>1.65f)
            { rescueTime=0; State="Иду спасать напарника"; MoveTo(rescueGoal,.25f,1.6f); return; }
            if(!Sight.Clear(ActorPosture.Chest(transform),ActorPosture.Chest(rescuePatient.transform))) { CancelRescue(); return; }
            GetComponent<ActorPosture>().TrySetCrouched(true);
            State="Поднимаю напарника"; rescueTime+=Time.deltaTime;
            if(rescueTime<(Role==HumanRole.Medic?3:6)) return;
            var patient=rescuePatient;
            // A new solid obstacle must not trap the revived capsule inside geometry.
            if(Physics.CheckCapsule(patient.transform.position+Vector3.up*.32f,patient.transform.position+Vector3.up*.68f,.3f,EscapeGame.SolidMask))
            { CancelRescue(); return; }
            patient.IsIncapacitated=false; patient.Health=Role==HumanRole.Medic?40:25; patient.revivals++;
            patient.incapDamage=0; patient.bleedoutAt=0; patient.lastDamageAt=Time.time;
            if(patient.casualtyBody) patient.casualtyBody.enabled=false;
            patient.GetComponent<ActorPosture>().SetIncapacitated(false); patient.controller.enabled=true;
            patient.stunnedUntil=Time.time+.8f; patient.State="Прихожу в себя"; patient.routeAt=0;
            healAt=Time.time+8; CancelRescue();
        }
        void CancelRescue()
        {
            if(rescuePatient && rescuePatient.rescueOwner==this) { rescuePatient.rescueOwner=null; rescuePatient.rescueLease=0; }
            rescuePatient=null; rescuePath=null; rescueTime=0; rescueUsedThisTick=false;
        }
        bool ShouldGuardRescue()
        {
            if(Role!=HumanRole.Soldier || Health<35 || (targetVisible && Vector3.Distance(transform.position,lastSeen)<2.5f)) return false;
            foreach(var patient in Game.Humans)
                if(patient.IsIncapacitated && patient.Rescuer && patient.Rescuer!=this && Vector3.Distance(transform.position,patient.transform.position)<12) return true;
            return false;
        }
        void GuardRescue()
        {
            SetTask(PatrolTask.GuardRescue,transform.position); State="Прикрываю спасение";
            if(CurrentTarget && targetVisible && Game.Threat(CurrentTarget)) { FaceTarget(); FireIfSafe(); }
            else transform.Rotate(0,35*Time.deltaTime,0);
        }
    }
}
