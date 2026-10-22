using UnityEngine;

namespace IrkutskPrototype
{
    public enum HumanRole { Civilian, Trader, Medic, Soldier }
    public sealed class HumanAgent : MonoBehaviour
    {
        public HumanRole Role;
        public EscapeGame Game;
        public TextMesh Caption;
        public bool Downed { get; private set; }
        public bool IsPatrol => Role==HumanRole.Medic || Role==HumanRole.Soldier;
        public float Health { get; private set; }=100;
        CharacterController controller;
        Vector3 lastSeen;
        float searchUntil,shotAt,healAt;
        void Awake() { controller=GetComponent<CharacterController>(); }
        void LateUpdate() { if(Caption) Caption.transform.rotation=Quaternion.Euler(65,0,0); }
        public void Hit(float damage)
        {
            if(Downed) return;
            Health-=damage*(Role==HumanRole.Soldier?.5f:Role==HumanRole.Medic?.8f:1);
            if(IsPatrol) Game.PatrolAttacked();
            if(Health<=0) { Downed=true; controller.enabled=false; transform.rotation=Quaternion.Euler(90,transform.eulerAngles.y,0); transform.position+=Vector3.up*.2f; Game.HumanKilled(this); if(Caption) Caption.gameObject.SetActive(false); }
        }
        void Update()
        {
            if(Game==null || Game.IsOver || Downed || !IsPatrol) return;
            Vector3 delta=Game.Target-transform.position; float distance=delta.magnitude;
            bool sees=distance<12 && !Physics.Linecast(transform.position+Vector3.up,Game.Target+Vector3.up,EscapeGame.SolidMask);
            if(sees) { lastSeen=Game.Target; searchUntil=Time.time+5; }
            if(Caption) Caption.text=Game.PatrolHostile?(Role==HumanRole.Medic?"Врач: ДРОТИК":"Солдат: ОГОНЬ"):searchUntil>Time.time?"СТОЙ! КАРАНТИН!":Role==HumanRole.Medic?"Врач":"Солдат";
            if(sees && Role==HumanRole.Medic && distance<7 && !Game.IsDriving && Time.time>=shotAt) { Game.Dart(); shotAt=Time.time+5; }
            if(sees && Role==HumanRole.Soldier && Game.PatrolHostile && Time.time>=shotAt) { Game.Damage(8); shotAt=Time.time+.8f; }
            if(Role==HumanRole.Medic && Time.time>healAt)
            { Health=Mathf.Min(100,Health+15); if(!Game.Soldier.Downed && Vector3.Distance(transform.position,Game.Soldier.transform.position)<3) Game.Soldier.Health=Mathf.Min(100,Game.Soldier.Health+15); healAt=Time.time+8; }
            if(Time.time>searchUntil || (Game.IsDriving && !Game.PatrolHostile)) return;
            Vector3 direction=lastSeen-transform.position; direction.y=0;
            float stop=Game.PatrolHostile && Role==HumanRole.Soldier?6:1.1f;
            if(direction.magnitude<stop) return;
            direction.Normalize();
            if(Physics.SphereCast(transform.position+Vector3.up,.4f,direction,out _,1.1f,EscapeGame.SolidMask))
            {
                Vector3 side=Quaternion.Euler(0,70,0)*direction;
                direction=Physics.SphereCast(transform.position+Vector3.up,.4f,side,out _,1,EscapeGame.SolidMask)?Quaternion.Euler(0,-70,0)*direction:side;
            }
            transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction),Time.deltaTime*6);
            controller.Move((direction*2.3f+Vector3.down*8)*Time.deltaTime);
        }
    }
}
