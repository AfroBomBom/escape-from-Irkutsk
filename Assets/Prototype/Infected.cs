using System.Collections.Generic;
using UnityEngine;

namespace IrkutskPrototype
{
    public sealed class Infected : MonoBehaviour
    {
        public bool Downed { get; private set; }
        public string Status => Time.time<stunnedUntil?"ОГЛУШЁН":alertUntil>Time.time?"! ПОИСК":"";
        CharacterController controller;
        Vector3 home, destination;
        float health=100, stunnedUntil, alertUntil, biteAt, planAt, idleAt;
        readonly Queue<Vector3> route=new Queue<Vector3>();
        void Start() { controller=GetComponent<CharacterController>(); home=transform.position; destination=home; }
        public void Hear(Vector3 point)
        { if(Downed) return; destination=point; alertUntil=Time.time+8; planAt=0; }
        public void Hit(float damage,float stun,Vector3 push)
        {
            if(Downed) return;
            health-=damage; stunnedUntil=Time.time+stun;
            if(controller==null) controller=GetComponent<CharacterController>();
            controller.Move(push); Hear(EscapeGame.Instance.Target);
            if(health<=0) { Downed=true; controller.enabled=false; transform.rotation=Quaternion.Euler(90,transform.eulerAngles.y,0); transform.position+=Vector3.up*.25f; }
        }
        void Update()
        {
            var game=EscapeGame.Instance;
            if(game==null || game.IsOver || Downed || Time.time<stunnedUntil) return;
            Vector3 target=game.Target, delta=target-transform.position;
            float distance=delta.magnitude;
            bool sees=distance<13 && (distance<2 || Vector3.Angle(transform.forward,delta)<65) &&
                !Physics.Linecast(transform.position+Vector3.up,target+Vector3.up,EscapeGame.SolidMask);
            if(sees) { destination=target; alertUntil=Time.time+6; }
            bool alert=Time.time<alertUntil;
            if(distance<(game.IsDriving?2.6f:1.35f) && (sees || game.IsDriving))
            { if(Time.time>=biteAt) { game.Damage(game.IsDriving?7:12,!game.IsDriving); biteAt=Time.time+1; } return; }
            if(!alert && Time.time>idleAt)
            { destination=home+new Vector3(Mathf.Sin(Time.time*.5f+home.x)*3,0,Mathf.Cos(Time.time*.4f+home.z)*3); idleAt=Time.time+4; }
            Vector3 direction=destination-transform.position; direction.y=0;
            if(direction.magnitude<.5f) return;
            if(Physics.SphereCast(transform.position+Vector3.up,.4f,direction.normalized,out _,direction.magnitude,EscapeGame.SolidMask))
            {
                if(Time.time>planAt) { Plan(destination); planAt=Time.time+1.2f; }
                if(route.Count>0)
                { if(Vector3.Distance(transform.position,route.Peek())<.6f) route.Dequeue(); if(route.Count>0) direction=route.Peek()-transform.position; }
            }
            else route.Clear();
            direction.y=0;
            if(direction.sqrMagnitude>.05f)
            {
                transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction),Time.deltaTime*6);
                controller.Move((direction.normalized*(alert?2.65f:1.1f)+Vector3.down*8)*Time.deltaTime);
            }
        }
        // Small A* grid, generated on demand; no NavMesh baking or packages required.
        void Plan(Vector3 goal)
        {
            route.Clear();
            Vector2Int start=Cell(transform.position), end=Cell(goal);
            var open=new List<Vector2Int>{start}; var came=new Dictionary<Vector2Int,Vector2Int>();
            var cost=new Dictionary<Vector2Int,int>{{start,0}};
            Vector2Int[] steps={Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
            int budget=1800;
            while(open.Count>0 && budget-->0)
            {
                int best=0; float score=float.MaxValue;
                for(int i=0;i<open.Count;i++) { float s=cost[open[i]]+Mathf.Abs(open[i].x-end.x)+Mathf.Abs(open[i].y-end.y); if(s<score) { score=s; best=i; } }
                var current=open[best]; open.RemoveAt(best);
                if(current==end)
                {
                    var path=new List<Vector3>();
                    while(current!=start) { path.Add(Point(current)); current=came[current]; }
                    path.Reverse(); foreach(var p in path) route.Enqueue(p); return;
                }
                foreach(var step in steps)
                {
                    var next=current+step;
                    if(next.x<-30 || next.x>117 || next.y<-24 || next.y>24) continue;
                    if(Physics.CheckBox(Point(next)+Vector3.up,new Vector3(.48f,.65f,.48f),Quaternion.identity,EscapeGame.SolidMask)) continue;
                    int c=cost[current]+1;
                    if(cost.TryGetValue(next,out int old) && old<=c) continue;
                    cost[next]=c; came[next]=current; if(!open.Contains(next)) open.Add(next);
                }
            }
        }
        static Vector2Int Cell(Vector3 v) => new Vector2Int(Mathf.RoundToInt(v.x),Mathf.RoundToInt(v.z));
        static Vector3 Point(Vector2Int v) => new Vector3(v.x,0,v.y);
    }
}
