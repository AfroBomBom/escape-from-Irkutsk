using UnityEngine;
using static IrkutskPrototype.Behaviors;
namespace IrkutskPrototype
{
    public sealed partial class Infected
    {
        BehaviorNode behavior;
        public BehaviorNode Behavior=>behavior??(behavior=BuildBehavior());
        public Transform CurrentTarget { get; private set; }
        bool targetVisible;
        Vector3 lastKnown;
        float windupUntil,sweepAt;
        int searchStep;
        BehaviorNode BuildBehavior()
        {
            return Select("Заражённый",
                When("Оглушён / недоступен",()=>EscapeGame.Instance==null || EscapeGame.Instance.IsOver || Downed || IsDetained || Time.time<stunnedUntil,Run("Ждать",()=>{})),
                Steps("Найти добычу",Do("Обзор, слух и память",Perceive),
                    Select("Решение",
                        When("Видимая добыча рядом",()=>targetVisible && InBiteRange(),Run("Замах и укус",Bite)),
                        When("Вижу добычу",()=>targetVisible,Run("Преследовать",()=>MoveAlong(destination,2.65f))),
                        When("Помню место",()=>Time.time<alertUntil,Run("Осмотреть место и окрестности",SearchArea)),
                        When("Один подозрительный звук",()=>Time.time-lastHeard<3 && suspicion>0,Run("Прислушаться",Listen)),
                        Run("Бродить",Wander))));
        }
        void Remember(Vector3 position,float seconds)
        { position.y=transform.position.y; lastKnown=destination=position; alertUntil=Time.time+seconds; planAt=0; searchStep=0; sweepAt=0; }
        bool TargetAlive(Transform target)
        {
            if(!target || !target.gameObject.activeInHierarchy) return false;
            var human=target.GetComponent<HumanAgent>();
            return human?!human.Downed:EscapeGame.Instance.IsHero(target);
        }
        void Perceive()
        {
            if(investigateAt>0 && Time.time>=investigateAt)
            { Remember(pendingNoise,4); investigateAt=0; suspicion=0; }
            var game=EscapeGame.Instance; Transform seen=null; float best=float.MaxValue;
            void Consider(Transform target)
            {
                if(!TargetAlive(target) || !Sight.CanSee(transform,target,Sight.InfectedRange,Sight.InfectedAngle)) return;
                float score=Vector3.Distance(transform.position,target.position)-(target==CurrentTarget?1.2f:0);
                if(score<best) { best=score; seen=target; }
            }
            Consider(game.IsDriving?game.Car:game.Hero);
            foreach(var human in game.Humans) Consider(human.transform);
            targetVisible=seen;
            if(seen)
            {
                if(CurrentTarget!=seen) windupUntil=0;
                CurrentTarget=seen; lastKnown=destination=seen.position; alertUntil=Time.time+6; searchStep=0; sweepAt=0;
            }
            else if(!TargetAlive(CurrentTarget) || Time.time>=alertUntil) CurrentTarget=null;
            if(!targetVisible || !InBiteRange()) windupUntil=0;
        }
        bool InBiteRange()=>CurrentTarget && Vector3.Distance(transform.position,CurrentTarget.position)<(CurrentTarget==EscapeGame.Instance.Car?2.6f:1.35f);
        void Bite()
        {
            if(Time.time<biteAt) return;
            Vector3 facing=CurrentTarget.position-transform.position; facing.y=0;
            if(facing.sqrMagnitude>.01f) transform.rotation=Quaternion.LookRotation(facing);
            if(windupUntil<=0) { windupUntil=Time.time+.35f; return; }
            if(Time.time<windupUntil) return;
            var game=EscapeGame.Instance; var human=CurrentTarget.GetComponent<HumanAgent>();
            if(human) { game.ObserveAggression(transform); human.Hit(12,false,transform); }
            else game.Damage(game.IsDriving?7:12,!game.IsDriving);
            windupUntil=0; biteAt=Time.time+1;
        }
        void SearchArea()
        {
            if(searchStep==0 && Vector3.Distance(transform.position,lastKnown)>.7f) { MoveAlong(lastKnown,2.65f); return; }
            if(Time.time>=sweepAt)
            {
                float angle=(GetEntityId().GetHashCode()%7)*40+searchStep*110;
                Vector3 candidate=lastKnown+Quaternion.Euler(0,angle,0)*Vector3.forward*2;
                destination=NpcTactics.ClearStep(lastKnown,candidate)?candidate:lastKnown;
                searchStep++; sweepAt=Time.time+1.3f;
            }
            if(Vector3.Distance(transform.position,destination)>.6f) MoveAlong(destination,1.5f);
            else transform.Rotate(0,65*Time.deltaTime,0);
        }
        void Listen()
        {
            Vector3 toward=pendingNoise-transform.position; toward.y=0;
            if(toward.sqrMagnitude>.1f) transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(toward),45*Time.deltaTime);
        }
        void Wander()
        {
            if(Time.time>idleAt)
            {
                Vector3 point=home+new Vector3(Mathf.Sin(Time.time*.5f+home.x)*3,0,Mathf.Cos(Time.time*.4f+home.z)*3);
                destination=NpcTactics.Walkable(point)?point:home; idleAt=Time.time+4;
            }
            MoveAlong(destination,1.1f);
        }
        void MoveAlong(Vector3 goal,float pace)
        {
            Vector3 direction=goal-transform.position; direction.y=0;
            if(direction.magnitude<.45f) return;
            if(Physics.SphereCast(transform.position+Vector3.up,.4f,direction.normalized,out _,direction.magnitude,EscapeGame.SolidMask))
            {
                if(Time.time>=planAt) { Plan(goal); planAt=Time.time+1.2f; }
                while(route.Count>0 && Vector3.Distance(transform.position,route.Peek())<.6f) route.Dequeue();
                if(route.Count==0) { transform.Rotate(0,40*Time.deltaTime,0); return; }
                direction=route.Peek()-transform.position;
            }
            else route.Clear();
            direction.y=0;
            if(direction.sqrMagnitude<.05f) return;
            Vector3 steer=NpcTactics.Separate(transform,direction);
            transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(steer),Time.deltaTime*6);
            controller.Move((steer*pace+Vector3.down*8)*Time.deltaTime);
        }
    }
}
