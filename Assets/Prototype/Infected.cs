using System.Collections.Generic;
using UnityEngine;

namespace IrkutskPrototype
{
    public sealed partial class Infected : MonoBehaviour
    {
        public bool Downed { get; private set; }
        public string Status => Time.time<stunnedUntil?"ОГЛУШЁН":windupUntil>0?"! ЗАМАХ":targetVisible?"! ПОГОНЯ":alertUntil>Time.time?"! ПОИСК":Time.time-lastHeard<3 && suspicion>0?"? ШУМ":"";
        CharacterController controller;
        Transform hands;
        Vector3 home, destination;
        float health=100, stunnedUntil, alertUntil, biteAt, planAt, idleAt;
        float suspicion,lastHeard=-10,investigateAt;
        Vector3 pendingNoise;
        readonly Queue<Vector3> route=new Queue<Vector3>();
        void Awake() { if(!GetComponent<ShotReactionZone>()) gameObject.AddComponent<ShotReactionZone>(); }
        void Start() { controller=GetComponent<CharacterController>(); hands=transform.Find("Hands"); home=transform.position; destination=home; }
        void LateUpdate() { if(hands && !Downed) hands.localPosition=new Vector3(0,1,windupUntil>0?.8f:.5f); }
        public void Hear(Vector3 point,float radius=4)
        {
            float reach=Mathf.Min(radius,9);
            if(Physics.Linecast(transform.position+Vector3.up,point+Vector3.up,EscapeGame.SolidMask)) reach*=.45f;
            if(Downed || Vector3.Distance(transform.position,point)>reach) return;
            if(Time.time-lastHeard>3) suspicion=0;
            lastHeard=Time.time; pendingNoise=point; suspicion+=radius>=10?1:.55f;
            if(suspicion>=1 && investigateAt<=0) investigateAt=Time.time+.9f;
        }
        public void Hit(float damage,float stun,Vector3 push,Vector3? source=null)
        {
            if(Downed) return;
            health-=damage; stunnedUntil=Time.time+stun;
            if(controller==null) controller=GetComponent<CharacterController>();
            controller.Move(push); Remember(source??EscapeGame.Instance.Target,4); windupUntil=0;
            if(health<=0) { Downed=true; controller.enabled=false; transform.rotation=Quaternion.Euler(90,transform.eulerAngles.y,0); transform.position+=Vector3.up*.25f; }
        }
        void Update()
        {
            Behavior.Tick();
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
