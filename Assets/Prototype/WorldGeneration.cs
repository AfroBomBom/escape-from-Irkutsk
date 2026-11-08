using System;
using System.Collections.Generic;
using UnityEngine;
namespace IrkutskPrototype
{
    public static class WorldMapBounds
    {
        public const int CityMinX=-67, CityMaxX=30, CityMinZ=-47, CityMaxZ=47;
        public static bool ContainsGrid(int x,int z)
            => x>=CityMinX && x<=116 && z>=CityMinZ && z<=CityMaxZ && (x<=31 || Mathf.Abs(z)<=6);
    }
    public sealed class GeneratedHouse
    {
        public string Name;
        public Vector3 Center;
        public float Width,Depth,Height;
        public bool NorthEntrance;
        public float BackSign => NorthEntrance?-1:1;
        public Vector3 OutsideDoor => Center+Vector3.forward*(-BackSign*(Depth/2+1.5f));
        public Vector3 InsideDoor => Center+Vector3.forward*(-BackSign*.5f);
    }
    public sealed partial class EscapeGame
    {
        const int CityWidth=WorldMapBounds.CityMaxX-WorldMapBounds.CityMinX+1;
        const int CityDepth=WorldMapBounds.CityMaxZ-WorldMapBounds.CityMinZ+1;
        readonly bool[,] connectedCity=new bool[CityWidth,CityDepth];
        readonly List<Vector3> occupiedSpawns=new List<Vector3>();
        public readonly List<GeneratedHouse> GeneratedHouses=new List<GeneratedHouse>();
        public IReadOnlyList<Vector3> InitialNpcPositions => occupiedSpawns;
        public Vector3[] PatrolRoute { get; private set; }
        public int WorldSeed { get; private set; }
        System.Random worldRandom;
        void InitializeWorldGeneration()
        {
            var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-world-seed");
            WorldSeed=index>=0 && index+1<args.Length && int.TryParse(args[index+1],out int seed)?seed:Guid.NewGuid().GetHashCode();
            worldRandom=new System.Random(WorldSeed);
        }
        void BuildRandomHouses()
        {
            var lots=new List<Vector2>();
            foreach(int x in new[]{-57,-41}) foreach(int z in new[]{-36,-18,0,18,36}) lots.Add(new Vector2(x,z));
            foreach(int x in new[]{-24,-8,8,24}) foreach(int z in new[]{-36,36}) lots.Add(new Vector2(x,z));
            for(int i=lots.Count-1;i>0;i--) { int j=worldRandom.Next(i+1); var tmp=lots[i]; lots[i]=lots[j]; lots[j]=tmp; }
            for(int i=0;i<9;i++)
            {
                int size=i<3?i:worldRandom.Next(3);
                string name=i==0?"Small house":i==1?"Medium house":i==2?"Large house":"House "+(i+1);
                var h=new GeneratedHouse
                {
                    Name=name,Center=new Vector3(lots[i].x+worldRandom.Next(-1,2),0,lots[i].y+worldRandom.Next(-1,2)),
                    Width=size==0?4.5f:size==1?6:8,Depth=size==2?6:5,Height=size==0?2.8f:size==1?4:6,
                    NorthEntrance=worldRandom.Next(2)==0
                };
                GeneratedHouses.Add(h); EnterableHouse(h.Name,h.Center.x,h.Center.z,h.Width,h.Depth,h.Height,h.NorthEntrance);
            }
        }
        bool InOrNearHouse(Vector3 p,float margin=1)
        {
            foreach(var h in GeneratedHouses)
                if(Mathf.Abs(p.x-h.Center.x)<=h.Width/2+margin && Mathf.Abs(p.z-h.Center.z)<=h.Depth/2+margin) return true;
            return false;
        }
        void BuildCityConnectivity()
        {
            Physics.SyncTransforms();
            var open=new bool[CityWidth,CityDepth];
            for(int x=0;x<CityWidth;x++) for(int z=0;z<CityDepth;z++)
            {
                Vector3 point=new Vector3(x+WorldMapBounds.CityMinX,1,z+WorldMapBounds.CityMinZ);
                open[x,z]=!Physics.CheckBox(point,new Vector3(.42f,.65f,.42f),Quaternion.identity,SolidMask,QueryTriggerInteraction.Ignore);
            }
            int sx=-9-WorldMapBounds.CityMinX, sz=-10-WorldMapBounds.CityMinZ;
            if(!open[sx,sz]) throw new InvalidOperationException("The garage exit is blocked during generation.");
            var queue=new Queue<Vector2Int>(); queue.Enqueue(new Vector2Int(sx,sz)); connectedCity[sx,sz]=true;
            var offsets=new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
            while(queue.Count>0)
            {
                var current=queue.Dequeue();
                foreach(var offset in offsets)
                {
                    var next=current+offset;
                    if(next.x<0 || next.x>=CityWidth || next.y<0 || next.y>=CityDepth || !open[next.x,next.y] || connectedCity[next.x,next.y]) continue;
                    connectedCity[next.x,next.y]=true; queue.Enqueue(next);
                }
            }
        }
        public bool IsConnectedCityPoint(Vector3 point)
        {
            int x=Mathf.RoundToInt(point.x)-WorldMapBounds.CityMinX, z=Mathf.RoundToInt(point.z)-WorldMapBounds.CityMinZ;
            return x>=0 && x<CityWidth && z>=0 && z<CityDepth && connectedCity[x,z];
        }
        bool AvailableCityPoint(Vector3 point,float minimumHeroDistance,float actorSeparation)
        {
            if(!IsConnectedCityPoint(point) || InOrNearHouse(point) || Vector3.Distance(point,Hero.position)<minimumHeroDistance) return false;
            foreach(var previous in occupiedSpawns) if(Vector3.Distance(previous,point)<actorSeparation) return false;
            foreach(var part in Parts) if(Vector3.Distance(part.transform.position,point)<1.5f) return false;
            foreach(var item in ExtraItems) if(item.gameObject.activeInHierarchy && Vector3.Distance(item.transform.position,point)<1.5f) return false;
            return !Physics.CheckCapsule(point+Vector3.up*.45f,point+Vector3.up*1.35f,.35f,SolidMask,QueryTriggerInteraction.Ignore);
        }
        Vector3 PickCityPoint(float minHeroDistance,float separation,bool nearHero=false,bool reserve=true)
        {
            for(int attempt=0;attempt<4000;attempt++)
            {
                var p=nearHero?new Vector3(worldRandom.Next(-21,2),0,worldRandom.Next(-21,2))
                    :new Vector3(worldRandom.Next(-65,30),0,worldRandom.Next(-45,46));
                if(nearHero && Vector3.Distance(p,Hero.position)>12) continue;
                if(!AvailableCityPoint(p,minHeroDistance,separation)) continue;
                if(reserve) occupiedSpawns.Add(p); return p;
            }
            throw new InvalidOperationException("Could not find a reachable city spawn.");
        }
        bool TryPickMedicPoint(Vector3 soldier,out Vector3 medic)
        {
            for(int attempt=0;attempt<500;attempt++)
            {
                var p=soldier+new Vector3(worldRandom.Next(-4,5),0,worldRandom.Next(-4,5));
                if(Vector3.Distance(p,soldier)<2 || Vector3.Distance(p,soldier)>5 || !AvailableCityPoint(p,12,1.8f)) continue;
                if(!Sight.Clear(soldier+Vector3.up*1.3f,p+Vector3.up*1.3f)) continue;
                occupiedSpawns.Add(p); medic=p; return true;
            }
            medic=default; return false;
        }
        Vector3 PickPatrolWaypoint(Vector3 anchor)
        {
            for(int attempt=0;attempt<4000;attempt++)
            {
                Vector3 p=anchor+new Vector3(worldRandom.Next(-18,19),0,worldRandom.Next(-18,19));
                float distance=Vector3.Distance(anchor,p);
                if(distance>=8 && distance<=22 && AvailableCityPoint(p,4,1)) return p;
            }
            throw new InvalidOperationException("Could not find a reachable local patrol waypoint.");
        }
        void BuildRandomHumans()
        {
            BuildCityConnectivity();
            Vector3 citizen=PickCityPoint(4,3,true), soldier=default, medic=default;
            bool pairFound=false;
            for(int attempt=0;attempt<100 && !pairFound;attempt++)
            {
                soldier=PickCityPoint(12,4); pairFound=TryPickMedicPoint(soldier,out medic);
                if(!pairFound) occupiedSpawns.RemoveAt(occupiedSpawns.Count-1);
            }
            if(!pairFound) throw new InvalidOperationException("Could not place a reachable patrol pair.");
            Vector3 marauder=PickCityPoint(12,4);
            Citizen=SpawnHuman("Житель",HumanRole.Civilian,citizen,blue);
            Soldier=SpawnHuman("Солдат",HumanRole.Soldier,soldier,dark);
            Medic=SpawnHuman("Врач",HumanRole.Medic,medic,snow);
            var scavenger=SpawnHuman("Мародёр",HumanRole.Marauder,marauder,red);
            float patrolHeading=worldRandom.Next(360); Soldier.transform.rotation=Medic.transform.rotation=Quaternion.Euler(0,patrolHeading,0);
            Citizen.transform.rotation=Quaternion.Euler(0,worldRandom.Next(360),0); scavenger.transform.rotation=Quaternion.Euler(0,worldRandom.Next(360),0);
            var route=new List<Vector3>();
            for(int i=0;i<5;i++) route.Add(PickPatrolWaypoint(soldier));
            PatrolRoute=route.ToArray();
        }
        Vector3 PickRoadPoint()
        {
            for(int attempt=0;attempt<2000;attempt++)
            {
                var p=new Vector3(worldRandom.Next(38,102),0,worldRandom.Next(-4,5));
                bool near=false; foreach(var used in occupiedSpawns) if(Vector3.Distance(used,p)<6) { near=true; break; }
                if(near || Physics.CheckBox(p+Vector3.up,new Vector3(.5f,.7f,.5f),Quaternion.identity,SolidMask,QueryTriggerInteraction.Ignore)) continue;
                occupiedSpawns.Add(p); return p;
            }
            throw new InvalidOperationException("Could not find a free highway spawn.");
        }
        void BuildRandomInfected()
        {
            Physics.SyncTransforms();
            for(int i=0;i<12;i++)
            {
                Vector3 p=i<7?PickCityPoint(10,4):PickRoadPoint();
                var root=new GameObject("Infected"); root.transform.position=p; root.transform.rotation=Quaternion.Euler(0,worldRandom.Next(360),0);
                var cc=root.AddComponent<CharacterController>(); cc.height=1.8f; cc.radius=.35f; cc.center=Vector3.up*.9f;
                Box("Torso",new Vector3(0,1,0),new Vector3(.65f,1,.45f),green,false,root.transform);
                Box("Head",new Vector3(0,1.7f,0),new Vector3(.45f,.4f,.45f),snow,false,root.transform);
                Box("Hands",new Vector3(0,1,.5f),new Vector3(1,.2f,.2f),green,false,root.transform);
                Zombies.Add(root.AddComponent<Infected>());
            }
            Physics.SyncTransforms();
        }
    }
}
