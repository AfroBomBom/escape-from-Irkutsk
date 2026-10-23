using System;
using System.Collections.Generic;
using UnityEngine;

namespace IrkutskPrototype
{
    public enum BehaviorStatus { Success, Failure, Running }

    // Reactive composites restart from their first child each frame. This lets
    // danger interrupt scanning immediately, without waiting for an action to end.
    public abstract class BehaviorNode
    {
        public string Name { get; }
        public BehaviorStatus Status { get; private set; }
        public int LastTickFrame { get; private set; }=-1;
        public IReadOnlyList<BehaviorNode> Children { get; }
        protected BehaviorNode(string name,params BehaviorNode[] children)
        { Name=name; Children=Array.AsReadOnly(children); }
        public BehaviorStatus Tick()
        { LastTickFrame=Time.frameCount; Status=Evaluate(); return Status; }
        protected abstract BehaviorStatus Evaluate();
    }

    public static class Behaviors
    {
        sealed class Selector : BehaviorNode
        {
            public Selector(string name,BehaviorNode[] children):base(name,children) { }
            protected override BehaviorStatus Evaluate()
            {
                for(int i=0;i<Children.Count;i++)
                { var result=Children[i].Tick(); if(result!=BehaviorStatus.Failure) return result; }
                return BehaviorStatus.Failure;
            }
        }
        sealed class Sequence : BehaviorNode
        {
            public Sequence(string name,BehaviorNode[] children):base(name,children) { }
            protected override BehaviorStatus Evaluate()
            {
                for(int i=0;i<Children.Count;i++)
                { var result=Children[i].Tick(); if(result!=BehaviorStatus.Success) return result; }
                return BehaviorStatus.Success;
            }
        }
        sealed class Leaf : BehaviorNode
        {
            readonly Func<BehaviorStatus> action;
            public Leaf(string name,Func<BehaviorStatus> action):base(name) { this.action=action; }
            protected override BehaviorStatus Evaluate()=>action();
        }
        public static BehaviorNode Select(string name,params BehaviorNode[] children)=>new Selector(name,children);
        public static BehaviorNode Steps(string name,params BehaviorNode[] children)=>new Sequence(name,children);
        public static BehaviorNode Test(string name,Func<bool> condition)=>new Leaf(name,()=>condition()?BehaviorStatus.Success:BehaviorStatus.Failure);
        public static BehaviorNode Do(string name,Action action)=>new Leaf(name,()=>{ action(); return BehaviorStatus.Success; });
        public static BehaviorNode Run(string name,Action action)=>new Leaf(name,()=>{ action(); return BehaviorStatus.Running; });
        public static BehaviorNode When(string name,Func<bool> condition,BehaviorNode action)
            =>Steps(name,Test("Условие",condition),action);
    }
}
