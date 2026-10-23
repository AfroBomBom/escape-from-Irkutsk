using System.Collections.Generic;
using UnityEngine;
namespace IrkutskPrototype
{
    public static class GridRoute
    {
        public static Queue<Vector3> Find(Vector3 origin,Vector3 goal)
        {
            var result=new Queue<Vector3>(); Vector2Int start=Cell(origin),end=Cell(goal);
            var open=new List<Vector2Int>{start}; var cost=new Dictionary<Vector2Int,int>{{start,0}};
            var parent=new Dictionary<Vector2Int,Vector2Int>(); int budget=2200;
            Vector2Int[] directions={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
            while(open.Count>0 && budget-->0)
            {
                int best=0; float score=float.MaxValue;
                for(int i=0;i<open.Count;i++) { var p=open[i]; float f=cost[p]+Mathf.Abs(p.x-end.x)+Mathf.Abs(p.y-end.y); if(f<score) { best=i; score=f; } }
                var current=open[best]; open.RemoveAt(best);
                if(current==end)
                {
                    var path=new List<Vector3>(); while(current!=start) { path.Add(Point(current)); current=parent[current]; }
                    path.Reverse(); foreach(var p in path) result.Enqueue(p); return result;
                }
                foreach(var d in directions)
                {
                    var next=current+d;
                    if(next.x<-30 || next.x>116 || next.y<-24 || next.y>24) continue;
                    if(Physics.CheckBox(Point(next)+Vector3.up,new Vector3(.42f,.65f,.42f),Quaternion.identity,EscapeGame.SolidMask)) continue;
                    int c=cost[current]+1; if(cost.TryGetValue(next,out int old) && old<=c) continue;
                    parent[next]=current; cost[next]=c; if(!open.Contains(next)) open.Add(next);
                }
            }
            return result;
        }
        static Vector2Int Cell(Vector3 p)=>new Vector2Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.z));
        static Vector3 Point(Vector2Int p)=>new Vector3(p.x,0,p.y);
    }
}
