using Mirror;
using Prediction;
using Prediction.Interpolation;
using Prediction.policies.singleInstance;
using Unity.Cinemachine;
using UnityEngine;

namespace DefaultNamespace
{
    public class SingletonUtils: MonoBehaviour
    {
        public static SingletonUtils instance;
        public static ClientPredictedEntity localCPE;
        public static MovingAverageInterpolator localVisInterpolator;
        public static bool isPovCam = false;
        
        public LatencySimulation latencySim;
        public CinemachineCamera povCam;
        public CinemachineCamera topCam;
        public TMPro.TMP_Text clientText;

        private int deciderIndex = 3;
        private SimpleConfigurableResimulationDecider[] deciders = new SimpleConfigurableResimulationDecider[]
        {
            new SimpleConfigurableResimulationDecider(0.1f),
            new SimpleConfigurableResimulationDecider(0.01f),
            new SimpleConfigurableResimulationDecider(0.001f),
            new SimpleConfigurableResimulationDecider(0.0001f),
            new SimpleConfigurableResimulationDecider(0.00001f),
            new SimpleConfigurableResimulationDecider(0.000001f)
        };
        public static SimpleConfigurableResimulationDecider CURRENT_DECIDER;
        
        void Awake()
        {
            instance = this;
            CURRENT_DECIDER = deciders[deciderIndex];
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.V))
            {
                isPovCam = !isPovCam;
                int p = povCam.Priority;
                povCam.Priority = topCam.Priority;
                topCam.Priority = p;
                Debug.Log($"[SingletonUtils][CamSwap]");
            }
            if (Input.GetKeyDown(KeyCode.B))
            {
                PlayerController pc = localCPE.gameObject.GetComponent<PlayerController>();
                pc.fcam.gameObject.SetActive(!pc.fcam.gameObject.activeSelf);
            }

            if (Input.GetKeyDown(KeyCode.A))
            {
                PredictionManager.Instance.maxResimulationOverbudget++;
            }
            if (Input.GetKeyDown(KeyCode.Z))
            {
                if (PredictionManager.Instance.maxResimulationOverbudget != 0)
                {
                    PredictionManager.Instance.maxResimulationOverbudget--;
                }
            }
            
            //TODO: control more of the latency sim properties here.
            if (Input.GetKey(KeyCode.L) && Input.GetKeyDown(KeyCode.Alpha0))
            {
                latencySim.latency = 0;
            }
            if (Input.GetKey(KeyCode.L) && Input.GetKeyDown(KeyCode.Alpha1))
            {
                latencySim.latency = 20;
            }
            if (Input.GetKey(KeyCode.L) && Input.GetKeyDown(KeyCode.Alpha2))
            {
                latencySim.latency = 35;
            }
            if (Input.GetKey(KeyCode.L) && Input.GetKeyDown(KeyCode.Alpha3))
            {
                latencySim.latency = 52;
            }
            if (Input.GetKey(KeyCode.L) && Input.GetKeyDown(KeyCode.Alpha4))
            {
                latencySim.latency = 100;
            }
            if (Input.GetKey(KeyCode.L) && Input.GetKeyDown(KeyCode.Alpha5))
            {
                latencySim.latency = 150;
            }
            
            if (Input.GetKey(KeyCode.J) && Input.GetKeyDown(KeyCode.Alpha0))
            {
                latencySim.jitter = 0;
            }
            if (Input.GetKey(KeyCode.J) && Input.GetKeyDown(KeyCode.Alpha1))
            {
                latencySim.jitter = 0.01f;
            }
            if (Input.GetKey(KeyCode.J) && Input.GetKeyDown(KeyCode.Alpha2))
            {
                latencySim.jitter = 0.02f;
            }
            if (Input.GetKey(KeyCode.J) && Input.GetKeyDown(KeyCode.Alpha3))
            {
                latencySim.jitter = 0.1f;
            }
            if (Input.GetKey(KeyCode.J) && Input.GetKeyDown(KeyCode.Alpha4))
            {
                latencySim.jitter = 0.2f;
            }

            if (Input.GetKeyDown(KeyCode.I))
            {
                deciderIndex++;
                deciderIndex %= deciders.Length;
                CURRENT_DECIDER = deciders[deciderIndex];
                localCPE.SetSingleStateEligibilityCheckHandler(deciders[deciderIndex].Check);
            }
            if (Input.GetKeyDown(KeyCode.K))
            {
                deciderIndex--;
                deciderIndex = Mathf.Max(0, deciderIndex);
                CURRENT_DECIDER = deciders[deciderIndex];
                localCPE.SetSingleStateEligibilityCheckHandler(deciders[deciderIndex].Check);
            }
            
            if (Input.GetKeyDown(KeyCode.U))
            {
                localVisInterpolator.slidingWindowTickSize++;
            }
            if (Input.GetKeyDown(KeyCode.J))
            {
                localVisInterpolator.slidingWindowTickSize--;
                if (localVisInterpolator.slidingWindowTickSize < 1)
                {
                    localVisInterpolator.slidingWindowTickSize = 1;
                }
            }
        }
    }
}