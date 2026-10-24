using System.Collections.Generic;
using UnityEngine;
using static IrkutskPrototype.Behaviors;
namespace IrkutskPrototype
{
    public enum HumanRole { Civilian, Medic, Soldier, Marauder }
    public sealed partial class HumanAgent : MonoBehaviour
    {
        public HumanRole Role;
        public EscapeGame Game;
        public TextMesh Caption;
        public bool Downed { get; private set; }
        public bool IsDetained { get; private set; }
        public bool IsPatrol => Role==HumanRole.Medic || Role==HumanRole.Soldier;
        public float Health { get; private set; }=100;
        public Transform CurrentTarget { get; private set; }
        public string State { get; private set; }="Обход";
        BehaviorNode behavior;
        bool targetVisible;
        public BehaviorNode Behavior => behavior ?? (behavior=BuildBehavior());
        CharacterController controller;
        Queue<Vector3> route=new Queue<Vector3>();
        Vector3 lastSeen,plannedGoal;
        float searchUntil,shotAt,healAt,scanTime,bindTime,routeAt,pauseUntil,stunnedUntil,coverTime,ignoreUntil;
        Transform ignored;
        int waypoint;
        static readonly Vector3[] PatrolPoints={new Vector3(23,0,8),new Vector3(20,0,12),new Vector3(0,0,10),new Vector3(-16,0,0),new Vector3(-1,0,-10),new Vector3(20,0,-8),new Vector3(27,0,1)};
        void Awake() { controller=GetComponent<CharacterController>(); }
        void Start() { home=transform.position; waypoint=Role==HumanRole.Soldier?1:0; }
        void LateUpdate() { if(Caption) { Caption.transform.rotation=Quaternion.Euler(65,0,0); if(!Downed) Caption.text=name+": "+State+(IsIncapacitated?" "+BleedoutRemaining.ToString("F0")+" с":""); } }
        public void Hit(float damage,bool playerCaused=true,Transform attacker=null)
        {
            if(Downed) return;
            if(IsIncapacitated) { HitIncapacitated(damage,playerCaused); return; }
            if(damage>0) lastDamageAt=Time.time;
            Health-=damage*(Role==HumanRole.Soldier?.5f:Role==HumanRole.Medic?.8f:1);
            Transform source=playerCaused?Game.Hero:attacker;
            if(Role==HumanRole.Civilian && source) Frighten(source.position);
            if(Role==HumanRole.Marauder && source) AlertTo(source);
            if(IsPatrol) { if(playerCaused) Game.PatrolAttacked(this); else if(attacker) Game.ConfirmThreat(this,attacker); }
            if(Health<=0)
            {
                if(IsPatrol && Health> -40 && revivals==0 && !IsDetained) Incapacitate(playerCaused);
                else Die(playerCaused);
            }
        }
        public void Detain() { if(Downed || IsIncapacitated) return; CancelRescue(); IsDetained=true; State="В карантин"; CurrentTarget=null; }
        public void Stun(float seconds) { stunnedUntil=Time.time+seconds; }
        public bool Ignores(Transform target)=>target==ignored && Time.time<ignoreUntil;
        public void AlertTo(Transform target)
        {
            if(target) ReceiveReport(target,target.position);
        }
        public void MoveTo(Vector3 goal,float stop=1,float pace=2.1f)
        {
            if(Downed || IsIncapacitated || !controller.enabled) return;
            Vector3 delta=goal-transform.position; delta.y=0; if(delta.magnitude<stop) return;
            if(Physics.SphereCast(transform.position+Vector3.up,.4f,delta.normalized,out _,delta.magnitude,EscapeGame.SolidMask))
            {
                if(Time.time>routeAt || Vector3.Distance(goal,plannedGoal)>2) { route=GridRoute.Find(transform.position,goal); plannedGoal=goal; routeAt=Time.time+1; }
                while(route.Count>0 && Vector3.Distance(transform.position,route.Peek())<.5f) route.Dequeue();
                if(route.Count==0) return; delta=route.Peek()-transform.position; delta.y=0;
            }
            else route.Clear();
            if(delta.sqrMagnitude<.02f) return;
            transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(delta),Time.deltaTime*6);
            controller.Move((NpcTactics.Separate(transform,delta)*pace+Vector3.down*8)*Time.deltaTime);
        }
        void Roam()
        {
            State="Обход";
            if(Time.time<pauseUntil) { transform.Rotate(0,35*Time.deltaTime,0); return; }
            Vector3 goal=PatrolPoints[waypoint];
            if(Vector3.Distance(transform.position,goal)<1.2f) { waypoint=(waypoint+1)%PatrolPoints.Length; pauseUntil=Time.time+1.2f; return; }
            MoveTo(goal);
        }
        void Update()
        {
            TickIncapacitation();
            coverUsedThisTick=false;
            taskFrame=-1;
            Behavior.Tick();
            if(!coverUsedThisTick) ReleaseCover();
            if(taskFrame!=Time.frameCount) { TacticalTask=PatrolTask.None; taskUntil=0; }
            if(!rescueUsedThisTick) CancelRescue();
            rescueUsedThisTick=false;
        }
        BehaviorNode BuildBehavior()
        {
            // Priority runs from top to bottom; action timers and target memory
            // belong to this agent, never to a shared tree instance.
            return Select("Поведение NPC",
                When("Недоступен / оглушён",()=>Game==null || Game.IsOver || Downed || IsIncapacitated || Time.time<stunnedUntil,
                    Run("Ожидать",()=>{})),
                When("Задержан",()=>IsDetained,Run("Идти в карантин",()=>MoveTo(new Vector3(27,0,17)))),
                When("Мирный житель",()=>Role==HumanRole.Civilian,
                    Steps("Безопасность жителя",Do("Заметить опасность",ObserveCivilian),
                        Select("Реакция жителя",When("Опасно",()=>Time.time<fearUntil,Run("Бежать от опасности",Flee)),Run("Вернуться и ждать",ReturnHome)))),
                When("Мародёр",()=>Role==HumanRole.Marauder,
                    Steps("Тактика мародёра",Do("Обзор и память",ObserveMarauder),
                        Select("Решение мародёра",
                            When("Использую укрытие",CanContinueCover,Run("Укрыться и выглянуть",ContinueCover)),
                            When("Есть источник шума",()=>!CurrentTarget && HasSoundClue,Run("Осторожно проверить",InvestigateSound)),
                            When("Слабый звук",()=>!CurrentTarget && ListeningToWeakSound(),Run("Прислушаться",ListenToSound)),
                            When("Нет цели",()=>!CurrentTarget,Run("Обход",Roam)),
                            When("Потерял цель",()=>!targetVisible,Run("Искать",Search)),
                            When("Сильно ранен",()=>Health<40,Run("Отступать с огнём",RetreatFighting)),
                            Run("Выбрать позицию и атаковать",MarauderFight)))),
                Steps("Патруль",
                    Do("Обзор, память и лечение",ObserveAndSupport),
                    Select("Выбрать задачу",
                        When("Можно спасти напарника",CanRescue,Run("Подойти и поднять",RescuePartner)),
                        When("Прикрыть спасение",ShouldGuardRescue,Run("Прикрывать врача",GuardRescue)),
                        When("Напарник ранен",NeedsSupport,Run("Подойти для лечения",AssistPartner)),
                        When("Использую укрытие",CanContinueCover,Run("Тактика из укрытия",Fight)),
                        When("Есть источник шума",()=>!CurrentTarget && HasSoundClue,Run("Осторожно проверить",InvestigateSound)),
                        When("Слабый звук",()=>!CurrentTarget && ListeningToWeakSound(),Run("Прислушаться",ListenToSound)),
                        When("Целей нет",()=>!CurrentTarget,Run("Обходить маршрут",Roam)),
                        When("Цель потеряна",()=>!targetVisible,Run("Искать по последней позиции",Search)),
                        Steps("Видимая цель",
                            Do("Повернуться к цели",FaceTarget),
                            Select("Оценить угрозу",
                                When("Подтверждённая угроза",()=>Game.Threat(CurrentTarget),Run("Бой и дистанция",Fight)),
                                When("Герой в машине",()=>Game.IsHero(CurrentTarget) && Game.IsDriving,Run("Пропустить мирную машину",DropAndRoam)),
                                When("Проверенный герой",()=>Game.IsHero(CurrentTarget) && Game.Scanned,Run("Оглушить и задержать героя",DetainHero)),
                                When("Проверенный NPC",()=>!Game.IsHero(CurrentTarget) && Game.Checked(CurrentTarget),Run("Задержать NPC",DetainNpc)),
                                When("Врач проверяет",()=>Role==HumanRole.Medic,Run("Подойти и сканировать",ScanTarget)),
                                Run("Прикрывать проверку",CoverTarget))))));
        }
        void ObserveAndSupport()
        {
            Transform seen=Game.SelectPatrolTarget(this);
            if(seen) { if(seen!=CurrentTarget) { scanTime=0; bindTime=0; coverTime=0; } CurrentTarget=seen; lastSeen=seen.position; searchUntil=Time.time+4; SawTarget(seen); }
            else { scanTime=0; bindTime=0; }
            if(CurrentTarget && !Game.Alive(CurrentTarget)) CurrentTarget=null;
            if(!seen && Time.time>=searchUntil) CurrentTarget=null;
            targetVisible=seen && CurrentTarget==seen;
            TrySafeHealing();
        }
        void Search()
        {
            State="Поиск";
            if(Vector3.Distance(transform.position,lastSeen)<1.2f) transform.Rotate(0,55*Time.deltaTime,0);
            else MoveTo(lastSeen,1,1.6f);
        }
        void FaceTarget()
        {
            Vector3 facing=CurrentTarget.position-transform.position; facing.y=0;
            if(facing.sqrMagnitude>.01f) transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(facing),Time.deltaTime*6);
        }
        void Fight()
        {
            PatrolCombat();
        }
        void DropAndRoam() { CurrentTarget=null; Roam(); }
        void CoverTarget()
        {
            State="Прикрытие"; coverTime+=Time.deltaTime;
            if(coverTime>6)
            {
                ignored=CurrentTarget; ignoreUntil=Time.time+10; coverTime=0; DropAndRoam(); return;
            }
            MoveTo(CurrentTarget.position,3);
        }
        void ScanTarget()
        {
            float distance=Vector3.Distance(transform.position,CurrentTarget.position);
            State=distance<=Sight.ScannerRange?"Сканирование":"К проверке";
            // Hero scan and capture outcomes are shared with the expedition HUD.
            if(!Game.IsHero(CurrentTarget) && distance<=Sight.ScannerRange)
            { State="Сканирование"; scanTime+=Time.deltaTime; if(scanTime>=2) { Game.CompleteNpcScan(CurrentTarget); scanTime=0; } }
            else if(distance>Sight.ScannerRange) scanTime=0;
            MoveTo(CurrentTarget.position,Game.Checked(CurrentTarget)?1.1f:2.4f);
        }
        void DetainHero()
        {
            State="Задержание";
            if(Role==HumanRole.Medic && Vector3.Distance(transform.position,CurrentTarget.position)<7 && Time.time>=shotAt)
            { if(RepositionIfBlocked()) return; Game.NpcShot(this,CurrentTarget); shotAt=Time.time+5; }
            MoveTo(CurrentTarget.position,1.1f);
        }
        void DetainNpc()
        {
            State="К задержанию";
            float distance=Vector3.Distance(transform.position,CurrentTarget.position);
            var person=CurrentTarget.GetComponent<HumanAgent>();
            if(person && distance<1.5f)
            { State="Задержание"; bindTime+=Time.deltaTime; if(bindTime>=3) { person.Detain(); CurrentTarget=null; } }
            else bindTime=0;
            if(CurrentTarget) MoveTo(CurrentTarget.position,1.1f);
        }
    }
}
