using UnityEditor;
using UnityEngine;

namespace IrkutskPrototype
{
    public sealed class BehaviorTreeWindow : EditorWindow
    {
        HumanAgent actor;
        Vector2 scroll;
        [MenuItem("Irkutsk/NPC Behavior Tree")]
        static void Open()=>GetWindow<BehaviorTreeWindow>("Дерево NPC");
        void OnInspectorUpdate() { if(EditorApplication.isPlaying) Repaint(); }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Включи Play и выбери NPC. Ветки проверяются сверху вниз. Жёлтый — действие выполняется, зелёный — условие выполнено, красный — отказ, серый — ветка не проверялась в этом кадре.",MessageType.Info);
            actor=(HumanAgent)EditorGUILayout.ObjectField("Персонаж",actor,typeof(HumanAgent),true);
            if(EditorApplication.isPlaying && EscapeGame.Instance)
            {
                EditorGUILayout.BeginHorizontal();
                foreach(var person in EscapeGame.Instance.Humans)
                    if(person && GUILayout.Button(person.name)) actor=person;
                EditorGUILayout.EndHorizontal();
            }
            if(!actor) return;
            if(!EditorApplication.isPlaying) { EditorGUILayout.LabelField("Дерево доступно во время Play."); return; }
            EditorGUILayout.LabelField("Состояние",actor.State);
            EditorGUILayout.LabelField("Цель",actor.CurrentTarget?actor.CurrentTarget.name:"Нет");
            scroll=EditorGUILayout.BeginScrollView(scroll);
            Draw(actor.Behavior,0);
            EditorGUILayout.EndScrollView();
        }
        static void Draw(BehaviorNode node,int depth)
        {
            bool current=node.LastTickFrame==Time.frameCount;
            Color old=GUI.contentColor;
            GUI.contentColor=!current?Color.gray:node.Status==BehaviorStatus.Running?new Color(1,.75f,.15f):node.Status==BehaviorStatus.Success?new Color(.3f,.85f,.4f):new Color(1,.4f,.3f);
            Rect row=EditorGUILayout.GetControlRect(); row.x+=depth*16; row.width-=depth*16;
            string status=!current?"—":node.Status==BehaviorStatus.Running?"В работе":node.Status==BehaviorStatus.Success?"Да":"Нет";
            EditorGUI.LabelField(row,node.Name,status);
            GUI.contentColor=old;
            for(int i=0;i<node.Children.Count;i++) Draw(node.Children[i],depth+1);
        }
    }
}
