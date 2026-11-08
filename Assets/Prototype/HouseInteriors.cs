using System.Collections.Generic;
using UnityEngine;
namespace IrkutskPrototype
{
    public sealed class HouseContainer : MonoBehaviour
    {
        public string DisplayName;
        public int ItemKind;
        public bool Empty { get; private set; }
        public Vector3 InteractionPoint;
        public Vector2 RoomCenter,RoomSize;
        public TextMesh Caption;
        public bool Contains(Vector3 point)
            => Mathf.Abs(point.x-RoomCenter.x)<RoomSize.x/2-.15f && Mathf.Abs(point.z-RoomCenter.y)<RoomSize.y/2-.15f;
        public void MarkEmpty()
        {
            Empty=true;
            if(Caption) Caption.text=DisplayName+" • ПУСТО";
        }
    }
    public sealed partial class EscapeGame
    {
        public readonly List<HouseContainer> HouseContainers=new List<HouseContainer>();
        sealed class HouseView { public Vector2 Center,Size; public Renderer[] Walls; }
        readonly List<HouseView> houseViews=new List<HouseView>();
        void RegisterHouseView(float x,float z,float width,float depth,GameObject[] cameraSideWalls)
        {
            var renderers=new Renderer[cameraSideWalls.Length];
            for(int i=0;i<renderers.Length;i++) renderers[i]=cameraSideWalls[i].GetComponent<Renderer>();
            houseViews.Add(new HouseView { Center=new Vector2(x,z),Size=new Vector2(width,depth),Walls=renderers });
        }
        void UpdateHouseWalls()
        {
            if(!Hero) return;
            foreach(var room in houseViews)
            {
                bool inside=!driving && Hero.gameObject.activeInHierarchy
                    && Mathf.Abs(Hero.position.x-room.Center.x)<room.Size.x/2-.15f
                    && Mathf.Abs(Hero.position.z-room.Center.y)<room.Size.y/2-.15f;
                foreach(var wall in room.Walls) if(wall) wall.enabled=!inside;
            }
        }
        void BuildHouseInterior(string name,float x,float z,float width,float depth,bool northEntrance)
        {
            float side=width/2-.65f, back=northEntrance?-1:1;
            Vector3 P(float horizontal,float vertical,float longitudinal)=>new Vector3(x+horizontal,vertical,z+back*longitudinal);
            // Furniture stays along the side walls so the central route from the door remains open.
            Box(name+" table",P(-side,.45f,-1.05f),new Vector3(.8f,.9f,.7f),orange);
            Box(name+" chair",P(side,.27f,-1.15f),new Vector3(.55f,.54f,.55f),blue);
            Box(name+" chair back",P(side,.67f,-1.39f),new Vector3(.55f,.6f,.08f),blue,false);
            Box(name+" bed",P(side,.3f,.25f),new Vector3(.8f,.6f,1.65f),snow);
            Box(name+" bed pillow",P(side,.64f,.78f),new Vector3(.65f,.12f,.35f),amber,false);
            Box(name+" sofa",P(-side,.4f,.3f),new Vector3(.8f,.8f,1.25f),green);
            Box(name+" sofa back",P(-side-.32f,.92f,.3f),new Vector3(.12f,.55f,1.25f),green,false);
            float rear=depth/2-.65f;
            var cupboard=Box(name+" wardrobe",P(-side,1,rear),new Vector3(.9f,2,.7f),steel);
            var bedside=Box(name+" nightstand",P(side,.4f,rear),new Vector3(.7f,.8f,.7f),orange);
            int wardrobeItem=name=="Small house"?4:5;
            AddHouseContainer(cupboard,"Шкаф",wardrobeItem,P(-side+.6f,0,rear),x,z,width,depth,1.3f);
            AddHouseContainer(bedside,"Тумбочка",4,P(side-.6f,0,rear),x,z,width,depth,1.05f);
        }
        void AddHouseContainer(GameObject root,string displayName,int kind,Vector3 point,float x,float z,float width,float depth,float labelHeight)
        {
            var container=root.AddComponent<HouseContainer>();
            container.DisplayName=displayName; container.ItemKind=kind; container.InteractionPoint=point;
            container.RoomCenter=new Vector2(x,z); container.RoomSize=new Vector2(width,depth);
            container.Caption=Label(displayName+" • E",root.transform.position+Vector3.up*labelHeight,Color.white,.16f).GetComponent<TextMesh>();
            container.Caption.transform.SetParent(root.transform,true); HouseContainers.Add(container);
        }
        bool TryHouseInteraction()
        {
            HouseContainer nearest=null; float best=1.4f;
            foreach(var container in HouseContainers)
            {
                if(!container || !container.gameObject.activeInHierarchy || !container.Contains(Hero.position)) continue;
                Vector3 point=container.InteractionPoint;
                float distance=Vector2.Distance(new Vector2(Hero.position.x,Hero.position.z),new Vector2(point.x,point.z));
                if(distance>=best || !Sight.Clear(ActorPosture.Eye(Hero),point+Vector3.up)) continue;
                nearest=container; best=distance;
            }
            if(!nearest) return false;
            if(nearest.Empty) { Say(nearest.DisplayName+": пусто"); return true; }
            if(bag.Count>=2) { Say("Рюкзак полон — освободи место"); return true; }
            bag.Add(nearest.ItemKind); nearest.MarkEmpty(); Say("Найдено: "+ItemName(nearest.ItemKind));
            return true;
        }
    }
}
