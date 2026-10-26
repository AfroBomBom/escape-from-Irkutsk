using UnityEngine;
namespace IrkutskPrototype
{
    public sealed partial class HumanAgent
    {
        public float KnownHeroDamage { get; private set; }
        public float OwnConfirmedHeroDamage { get; private set; }
        public float EstimatedHeroHealth => Mathf.Max(0,100-KnownHeroDamage);
        public float HeroDamageMemoryRemaining => KnownHeroDamage>0?Mathf.Max(0,10-(Time.time-heroHealthSeenAt)):0;
        public bool HasHeroHealthAdvantage => KnownHeroDamage>=40 && Health>=50 && Health-EstimatedHeroHealth>=25;
        public bool PressingWoundedHero => Role!=HumanRole.Civilian && Role!=HumanRole.Medic && CurrentTarget==Game.Hero
            && targetVisible && HasHeroHealthAdvantage && !Game.IsDriving && (Role==HumanRole.Marauder || Game.Threat(CurrentTarget));
        float heroHealthSeenAt=-1000;
        bool CanObserveHeroHealth()
        {
            return Game && !Game.IsOver && !Game.IsDriving && Game.Hero && Game.Hero.gameObject.activeInHierarchy
                && isActiveAndEnabled && !Downed && !IsIncapacitated && !IsDetained
                && Sight.CanSee(transform,Game.Hero,Sight.HumanRange,Sight.HumanAngle);
        }
        void ObserveHeroDamageMemory()
        {
            // Expire before reacquiring sight, including after an NPC was disabled.
            if(Time.time-heroHealthSeenAt>=10) { KnownHeroDamage=0; OwnConfirmedHeroDamage=0; }
            if(CanObserveHeroHealth()) heroHealthSeenAt=Time.time;
        }
        internal void ObserveConfirmedHeroHit(HumanAgent shooter,float actualDamage)
        {
            ObserveHeroDamageMemory();
            if(actualDamage<=0 || !shooter || !CanObserveHeroHealth() || Role==HumanRole.Civilian) return;
            bool own=shooter==this;
            bool ally=(IsPatrol && shooter.IsPatrol) || (Role==HumanRole.Marauder && shooter.Role==HumanRole.Marauder);
            if(!own && (!ally || !Sight.CanSee(transform,shooter.transform,Sight.HumanRange,Sight.HumanAngle))) return;
            KnownHeroDamage=Mathf.Min(100,KnownHeroDamage+actualDamage);
            if(own) OwnConfirmedHeroDamage=Mathf.Min(100,OwnConfirmedHeroDamage+actualDamage);
        }
        internal void ObserveHeroHealing(float actualRestored)
        {
            ObserveHeroDamageMemory();
            if(actualRestored<=0 || !CanObserveHeroHealth()) return;
            KnownHeroDamage=Mathf.Max(0,KnownHeroDamage-actualRestored);
            OwnConfirmedHeroDamage=Mathf.Max(0,OwnConfirmedHeroDamage-actualRestored);
        }
        bool TryPressWoundedHero()
        {
            if(!PressingWoundedHero || Vector3.Distance(transform.position,lastSeen)<2.5f
                || !NpcTactics.FiringLane(transform,CurrentTarget)) return false;
            float distance=Vector3.Distance(transform.position,lastSeen);
            Vector3 point=transform.position;
            if(distance>3.5f)
            {
                if(TacticalTask==PatrolTask.Press && Time.time<taskUntil && Vector3.Distance(transform.position,taskGoal)>.5f) point=taskGoal;
                else
                {
                    Vector3 delta=lastSeen-transform.position; delta.y=0;
                    point=transform.position+delta.normalized*Mathf.Min(3,distance-3.5f);
                }
                if(!NpcTactics.ClearStep(transform.position,point)) return false;
            }
            ReleaseCover(); SetTask(PatrolTask.Press,point); State="Давлю на раненого противника";
            FaceTarget();
            if(Time.time>=shotAt)
            {
                if(Role==HumanRole.Marauder) Game.ObserveAggression(transform);
                Game.NpcShot(this,CurrentTarget); shotAt=Time.time+(Role==HumanRole.Soldier?.6f:.9f);
            }
            if(distance>3.5f)
            {
                Quaternion facing=transform.rotation; MoveTo(point,.4f,2.7f); transform.rotation=facing;
            }
            return true;
        }
    }
    public sealed partial class EscapeGame
    {
        void ConfirmHeroDamage(HumanAgent shooter,float actualDamage)
        { foreach(var observer in Humans) if(observer) observer.ObserveConfirmedHeroHit(shooter,actualDamage); }
        // Future medicine/animation code calls this on completed healing. No item or key is added yet.
        public float HealHero(float amount)
        {
            if(IsOver || IsDriving || hp<=0 || amount<=0 || float.IsNaN(amount) || float.IsInfinity(amount)) return 0;
            float before=hp; hp=Mathf.Min(100,hp+amount); float restored=hp-before;
            if(restored>0) foreach(var observer in Humans) if(observer) observer.ObserveHeroHealing(restored);
            return restored;
        }
    }
}
