using UnityEngine;
namespace IrkutskPrototype
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CoverObstacle : MonoBehaviour
    {
        [Tooltip("Low cover requires crouching in its hiding slots.")]
        public bool Low=true;
        void Reset() { gameObject.layer=8; }
        void OnEnable() { gameObject.layer=8; }
        void OnDrawGizmosSelected()
        {
            var box=GetComponent<BoxCollider>(); if(!box) return;
            Gizmos.color=Low?Color.cyan:Color.blue;
            var old=Gizmos.matrix; Gizmos.matrix=transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center,box.size); Gizmos.matrix=old;
        }
    }
}
