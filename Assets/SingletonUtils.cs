using Mirror;
using Unity.Cinemachine;
using UnityEngine;

namespace DefaultNamespace
{
    public class SingletonUtils: MonoBehaviour
    {
        public static SingletonUtils instance;
        public LatencySimulation latencySim;
        public CinemachineCamera povCam;
        public CinemachineCamera topCam;
        public TMPro.TMP_Text clientText;
        void Awake()
        {
            instance = this;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.V))
            {
                int p = povCam.Priority;
                povCam.Priority = topCam.Priority;
                topCam.Priority = p;
            }

            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                latencySim.latency = 0;
            }
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                latencySim.latency = 20;
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                latencySim.latency = 35;
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                latencySim.latency = 52;
            }
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                latencySim.latency = 100;
            }
            if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                latencySim.latency = 150;
            }
        }
    }
}