using UnityEngine;

namespace A5BGames.DisintegrationEffect
{
    public class DemoManager : MonoBehaviour
    {
        [SerializeField]
        private Vector3 originPoint;

        [SerializeField]
        private Disintegratable disintegratable;

        public void TriggerDisintegration () {
            disintegratable.Disintegrate (originPoint);
        }

        public void ResetDisintegratable () {
            var renderer = disintegratable.GetComponent<Renderer> ();
            renderer.enabled = true;
        }
    }
}