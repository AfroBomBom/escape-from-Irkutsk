using UnityEditor;
using UnityEngine;

namespace IrkutskPrototype
{
    public sealed class BehaviorTreeWindow : EditorWindow
    {
        MonoBehaviour actor;
        Vector2 scroll;
        [MenuItem("Irkutsk/NPC Behavior Tree")]
        static void Open()=>GetWindow<BehaviorTreeWindow>("Дерево NPC");
        void OnInspectorUpdate() { if(EditorApplication.isPlaying) Repaint(); }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Включи Play и выбери NPC. Ветки проверяются сверху вниз. Жёлтый — действие выполняется, зелёный — условие выполнено, красный — отказ, серый — ветка не проверялась в этом кадре.",MessageType.Info);
            actor=(MonoBehaviour)EditorGUILayout.ObjectField("Персонаж",actor,typeof(MonoBehaviour),true);
            if(EditorApplication.isPlaying && EscapeGame.Instance)
            {
                EditorGUILayout.BeginHorizontal();
                foreach(var person in EscapeGame.Instance.Humans)
                    if(person && GUILayout.Button(person.name)) actor=person;
                EditorGUILayout.EndHorizontal();
                for(int i=0;i<EscapeGame.Instance.Zombies.Count;i++)
                {
                    if(i%4==0) EditorGUILayout.BeginHorizontal();
                    if(GUILayout.Button("Заражённый "+(i+1))) actor=EscapeGame.Instance.Zombies[i];
                    if(i%4==3 || i==EscapeGame.Instance.Zombies.Count-1) EditorGUILayout.EndHorizontal();
                }
            }
            if(!actor) return;
            if(!EditorApplication.isPlaying) { EditorGUILayout.LabelField("Дерево доступно во время Play."); return; }
            BehaviorNode tree; Transform target; string state;
            if(actor is HumanAgent human) { tree=human.Behavior; target=human.CurrentTarget; state=human.State; }
            else if(actor is Infected z) { tree=z.Behavior; target=z.CurrentTarget; state=z.Status; }
            else { EditorGUILayout.LabelField("Выбери человека или заражённого."); return; }
            EditorGUILayout.LabelField("Состояние",state);
            EditorGUILayout.LabelField("Цель",target?target.name:"Нет");
            if(actor is HumanAgent observer)
            {
                EditorGUILayout.LabelField("Оценка урона герою",observer.KnownHeroDamage.ToString("F0")+" (свой: "+observer.OwnConfirmedHeroDamage.ToString("F0")+")");
                EditorGUILayout.LabelField("Оценка HP героя",observer.EstimatedHeroHealth.ToString("F0"));
                EditorGUILayout.LabelField("Память урона",observer.HeroDamageMemoryRemaining.ToString("F1")+" с");
                EditorGUILayout.LabelField("Преимущество по HP",observer.HasHeroHealthAdvantage?"Да":"Нет");
            }
            if(actor is HumanAgent coverUser)
                EditorGUILayout.LabelField("Укрытие",coverUser.SelectedCover?coverUser.SelectedCover.name:"Нет");
            if(actor is HumanAgent listener)
                EditorGUILayout.LabelField("Источник шума",listener.HasSoundClue?listener.LastHeardPosition.ToString("F1"):"Нет активной зацепки");
            if(actor is HumanAgent looter && looter.Role==HumanRole.Marauder)
            {
                EditorGUILayout.LabelField("Добыча",looter.LootInventory.Count+" / 3");
                foreach(var item in looter.LootInventory) if(item) EditorGUILayout.LabelField("  • "+item.name);
                EditorGUILayout.LabelField("Сбор",looter.LootTarget?looter.LootTarget.name+" "+(looter.LootProgress*100).ToString("F0")+"%":"Осмотр местности");
            }
            if(actor is HumanAgent patrol && patrol.IsPatrol)
            {
                EditorGUILayout.LabelField("Тактическая задача",patrol.TacticalTask.ToString());
                EditorGUILayout.LabelField("Удержание решения",patrol.TaskHoldRemaining.ToString("F1")+" с");
                EditorGUILayout.LabelField("Тяжёлое ранение",patrol.IsIncapacitated?patrol.BleedoutRemaining.ToString("F1")+" с до гибели":"Нет");
                EditorGUILayout.LabelField("Спасение",patrol.RescuePatient?patrol.RescuePatient.name+" "+(patrol.RescueProgress*100).ToString("F0")+"%":"Нет");
            }
            scroll=EditorGUILayout.BeginScrollView(scroll);
            Draw(tree,0);
            EditorGUILayout.EndScrollView();
        }
        static void Draw(BehaviorNode node,int depth)
        {
            bool current=node.LastTickFrame==Time.frameCount;
            Color old=GUI.contentColor;
            GUI.contentColor=!current?Color.gray:node.Status==BehaviorStatus.Running?new Color(1,.75f,.15f):node.Status==BehaviorStatus.Success?new Color(.3f,.85f,.4f):new Color(1,.4f,.3f);
            Rect row=EditorGUILayout.GetControlRect(); row.x+=depth*16; row.width-=depth*16;
            string status=!current?"—":node.Status==BehaviorStatus.Running?"В работе":node.Status==BehaviorStatus.Success?"Да":"Нет";
            EditorGUI.LabelField(new Rect(row.x,row.y,Mathf.Max(60,row.width-70),row.height),node.Name);
            EditorGUI.LabelField(new Rect(row.xMax-65,row.y,65,row.height),status);
            GUI.contentColor=old;
            for(int i=0;i<node.Children.Count;i++) Draw(node.Children[i],depth+1);
        }
    }
}
