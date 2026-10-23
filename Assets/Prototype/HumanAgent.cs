using System.Collections.Generic;
using UnityEngine;
namespace IrkutskPrototype
{
    public enum HumanRole { Civilian, Medic, Soldier, Marauder }
    public sealed class HumanAgent : MonoBehaviour
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
        CharacterController controller;
        Queue<Vector3> route=new Queue<Vector3>();
        Vector3 lastSeen,plannedGoal;
        float searchUntil,shotAt,healAt,scanTime,bindTime,routeAt,pauseUntil,stunnedUntil,coverTime,ignoreUntil;
        Transform ignored;
        int waypoint;
        static readonly Vector3[] PatrolPoints={new Vector3(23,0,8),new Vector3(20,0,12),new Vector3(0,0,10),new Vector3(-16,0,0),new Vector3(-1,0,-10),new Vector3(20,0,-8),new Vector3(27,0,1)};
        void Awake() { controller=GetComponent<CharacterController>(); }
        void Start() { waypoint=Role==HumanRole.Soldier?1:0; }
        void LateUpdate() { if(Caption) { Caption.transform.rotation=Quaternion.Euler(65,0,0); if(!Downed) Caption.text=name+": "+State; } }
        public void Hit(float damage,bool playerCaused=true,Transform attacker=null)
        {
            if(Downed) return;
            Health-=damage*(Role==HumanRole.Soldier?.5f:Role==HumanRole.Medic?.8f:1);
            if(IsPatrol) { if(playerCaused) Game.PatrolAttacked(); else if(attacker) Game.RegisterThreat(attacker); }
            if(Health<=0)
            {
                Downed=true; controller.enabled=false; transform.rotation=Quaternion.Euler(90,transform.eulerAngles.y,0); transform.position+=Vector3.up*.2f;
                if(playerCaused) Game.HumanKilled(this); if(Caption) Caption.gameObject.SetActive(false);
            }
        }
        public void Detain() { IsDetained=true; State="В карантин"; CurrentTarget=null; }
        public void Stun(float seconds) { stunnedUntil=Time.time+seconds; }
        public bool Ignores(Transform target)=>target==ignored && Time.time<ignoreUntil;
        public void MoveTo(Vector3 goal,float stop=1)
        {
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
            controller.Move((delta.normalized*2.1f+Vector3.down*8)*Time.deltaTime);
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
            if(Game==null || Game.IsOver || Downed || Time.time<stunnedUntil) return;
            if(IsDetained) { MoveTo(new Vector3(27,0,17)); return; }
            if(Role==HumanRole.Civilian) { State="Мирный"; return; }
            if(Role==HumanRole.Marauder) { MarauderUpdate(); return; }
            Transform seen=Game.SelectPatrolTarget(this);
            if(seen) { if(seen!=CurrentTarget) { scanTime=0; bindTime=0; coverTime=0; } CurrentTarget=seen; lastSeen=seen.position; searchUntil=Time.time+4; }
            else { scanTime=0; bindTime=0; }
            if(CurrentTarget && !Game.Alive(CurrentTarget)) CurrentTarget=null;
            if(!seen && Time.time>=searchUntil) CurrentTarget=null;
            if(Role==HumanRole.Medic && Time.time>healAt)
            { Health=Mathf.Min(100,Health+15); if(!Game.Soldier.Downed && Vector3.Distance(transform.position,Game.Soldier.transform.position)<3) Game.Soldier.Health=Mathf.Min(100,Game.Soldier.Health+15); healAt=Time.time+8; }
            if(!CurrentTarget) { Roam(); return; }
            if(!seen) { State="Поиск"; MoveTo(lastSeen); return; }
            bool threat=Game.Threat(CurrentTarget); float distance=Vector3.Distance(transform.position,CurrentTarget.position);
            Vector3 facing=CurrentTarget.position-transform.position; facing.y=0;
            if(facing.sqrMagnitude>.01f) transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(facing),Time.deltaTime*6);
            State=threat?"Угроза":Role==HumanRole.Medic?"Проверка":"Прикрытие";
            if(Game.IsHero(CurrentTarget))
            {
                if(Role==HumanRole.Medic && (threat || Game.Scanned) && distance<7 && !Game.IsDriving && Time.time>=shotAt) { Game.Dart(); shotAt=Time.time+5; }
                if(Role==HumanRole.Soldier && threat && Time.time>=shotAt) { Game.NpcShot(this,CurrentTarget); shotAt=Time.time+.8f; }
                if(Role==HumanRole.Soldier && !threat && !Game.Scanned)
                {
                    coverTime+=Time.deltaTime;
                    if(coverTime>6) { ignored=CurrentTarget; ignoreUntil=Time.time+10; CurrentTarget=null; coverTime=0; Roam(); return; }
                }
                if(!Game.IsDriving || threat) MoveTo(CurrentTarget.position,threat?(Role==HumanRole.Soldier?6:4):!Game.Scanned?(Role==HumanRole.Soldier?3:2.4f):1.1f);
                else { CurrentTarget=null; Roam(); }
                return;
            }
            if(threat)
            {
                if(Time.time>=shotAt) { Game.NpcShot(this,CurrentTarget); shotAt=Time.time+(Role==HumanRole.Medic?1.3f:.8f); }
                if(distance<2.5f)
                {
                    Vector3 away=-facing.normalized;
                    if(!Physics.SphereCast(transform.position+Vector3.up,.4f,away,out _,.7f,EscapeGame.SolidMask)) controller.Move((away*1.7f+Vector3.down*8)*Time.deltaTime);
                }
                else MoveTo(CurrentTarget.position,Role==HumanRole.Soldier?6:4);
                return;
            }
            if(Role==HumanRole.Soldier && !Game.Checked(CurrentTarget))
            {
                coverTime+=Time.deltaTime;
                if(coverTime>6) { ignored=CurrentTarget; ignoreUntil=Time.time+10; CurrentTarget=null; coverTime=0; Roam(); return; }
            }
            if(Role==HumanRole.Medic && !Game.Checked(CurrentTarget) && distance<=Sight.ScannerRange)
            { State="Сканирование"; scanTime+=Time.deltaTime; if(scanTime>=2) { Game.CompleteNpcScan(CurrentTarget); scanTime=0; } }
            else if(distance>Sight.ScannerRange) scanTime=0;
            var person=CurrentTarget.GetComponent<HumanAgent>();
            if(person && Game.Checked(CurrentTarget) && !Game.Threat(CurrentTarget) && distance<1.5f)
            { State="Задержание"; bindTime+=Time.deltaTime; if(bindTime>=3) { person.Detain(); CurrentTarget=null; } }
            else bindTime=0;
            if(CurrentTarget) MoveTo(CurrentTarget.position,!Game.Checked(CurrentTarget)?(Role==HumanRole.Soldier?3:2.4f):1.1f);
        }
        void MarauderUpdate()
        {
            Transform target=null; float best=12;
            void Consider(Transform candidate)
            {
                if(!Game.Alive(candidate) || !Sight.CanSee(transform,candidate.position,12,Sight.HumanAngle)) return;
                float d=Vector3.Distance(transform.position,candidate.position); if(d<best) { best=d; target=candidate; }
            }
            Consider(Game.IsDriving?Game.Car:Game.Hero);
            foreach(var human in Game.Humans) if(human.IsPatrol) Consider(human.transform);
            foreach(var z in Game.Zombies) Consider(z.transform);
            State=target?"Атака":"Обход";
            if(target) { if(Time.time>=shotAt) { Game.ObserveAggression(transform); Game.NpcShot(this,target); shotAt=Time.time+1.2f; } MoveTo(target.position,6); }
            else Roam();
        }
    }
}
