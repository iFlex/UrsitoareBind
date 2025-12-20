using Assets.Scripts.Systems.Events;
using DefaultNamespace;
using Mirror;
using Prediction;
using Prediction.data;
using Prediction.Interpolation;
using Prediction.policies.singleInstance;
using Prediction.wrappers;
using Unity.Cinemachine;
using UnityEngine;

public abstract class PlayerController : NetworkBehaviour, PredictableComponent, PredictableControllableComponent
{
    [SerializeField] protected Rigidbody rb;
    [SerializeField] protected Renderer renderer;
    [SerializeField] protected bool isShared = false;
    //TODO: serializable
    [SerializeField] public PredictedNetworkBehaviour predictedMono; // { get; private set; }
    
    public CinemachineCamera pcam;
    public CinemachineCamera fcam;
    
    public static SafeEventDispatcher<PlayerController> spawned = new SafeEventDispatcher<PlayerController>();
    public static SafeEventDispatcher<PlayerController> despawned = new SafeEventDispatcher<PlayerController>();
    
    void Start()
    {
        SingletonUtils.CURRENT_DECIDER =
            (SimpleConfigurableResimulationDecider)PredictionManager.SNAPSHOT_INSTANCE_RESIM_CHECKER;
        if (isShared)
        {
            renderer.material.color = Color.red;
        }
    }

    void OnDestroy()
    {
        despawned.Dispatch(this);
    }
    
    public void SetCamera(CinemachineCamera newCamera)
    {
        pcam = newCamera;
    }

    public void Customize()
    {
        renderer.material.color = Color.yellow;
    }

    public void WireAsCtl()
    {
        SingletonUtils.localCPE = predictedMono.clientPredictedEntity;
        SetCamera(SingletonUtils.instance.povCam);
    }
    
    public override void OnStartAuthority()
    {
        WireAsCtl();
        Customize();
    }

    [SerializeField] bool redy = false;
    [SerializeField] bool dbgRedy = false;
    private void Update()
    {
        if (isOwned)
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                RequestSwitchToSharedObj();   
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                RequestSwitchBack();
            }
        }

        dbgRedy = predictedMono.isReady;
        if (!redy && predictedMono.isReady)
        {
            SingletonUtils.localVisInterpolator = (MovingAverageInterpolator) predictedMono.visuals.interpolationProvider;
            spawned.Dispatch(this);
            redy = true;
        }
        if (!redy)
        {
            return;
        }

        if (pcam && !pcam.Follow && predictedMono.clientPredictedEntity != null)
        {
            pcam.Follow = predictedMono.visuals.transform;
            SingletonUtils.instance.topCam.Follow = predictedMono.visuals.transform;
        }
        
        if (predictedMono.IsControlledLocally() && SingletonUtils.instance.clientText)
        {
            SingletonUtils.instance.clientText.text = $"Tick:{PredictionManager.Instance.lastClientAppliedTick} | {predictedMono.clientPredictedEntity.lastTick}\n " + 
                                                      $"ServerDelay:{predictedMono.clientPredictedEntity.GetServerDelay()} | endT:{predictedMono.clientPredictedEntity.serverStateBuffer.GetEndTick()}\n " +
                                                      $"Resimulations:{PredictionManager.Instance.totalResimulations}\n " +
                                                      $"AuthResims:{PredictionManager.Instance.totalResimulationsDueToAuthority}\n " +
                                                      $"FlwrResims:{PredictionManager.Instance.totalResimulationsDueToFollowers}\n " +
                                                      $"BothResims:{PredictionManager.Instance.totalResimulationsDueToBoth}\n " +
                                                      $"AvgResimLen:{PredictionManager.Instance.GetAverageResimPerTick()}\n" +
                                                      $"TotalResimSteps:{PredictionManager.Instance.totalResimulationSteps} ({(float)PredictionManager.Instance.totalResimulationSteps / PredictionManager.Instance.lastClientAppliedTick * 100}%)\n " +
                                                      $"Skips:{PredictionManager.Instance.totalResimulationsSkipped}\n " +
                                                      $"MaxResimOverbudget:{PredictionManager.Instance.maxResimulationOverbudget}\n " +
                                                      $"MaxSvDelay:{predictedMono.clientPredictedEntity.maxServerDelay}\n " +
                                                      $"Velo:{predictedMono.clientPredictedEntity.rigidbody.linearVelocity.magnitude}\n " +
                                                      $"DIST_TRES:{((SimpleConfigurableResimulationDecider)PredictionManager.SNAPSHOT_INSTANCE_RESIM_CHECKER).distResimThreshold}\n " +
                                                      $"SMOOTH_WNDW:{(SingletonUtils.localVisInterpolator != null ? SingletonUtils.localVisInterpolator.slidingWindowTickSize : -1)}\n " +
                                                      $"FPS:{1/Time.deltaTime}\n " +
                                                      $"FrameTime:{Time.deltaTime}\n";

            foreach (PredictedEntity pe in PredictionManager.Instance._predictedEntities)
            {
                if (pe.GetClientEntity().gameObject != predictedMono.gameObject)
                {
                    PredictedEntityVisuals pev = pe.GetVisualsControlled();
                    MovingAverageInterpolator vip = null;
                    if (pev)
                    {
                        vip = (MovingAverageInterpolator) pev.interpolationProvider;
                    }
                    
                    SingletonUtils.instance.clientText.text +=
                        $"\nid:{pe.GetId()} ResimTicksAuth:{pe.GetClientEntity().resimTicksAsAuthority} ResimTicksFlwr:{pe.GetClientEntity().resimTicksAsFollower} smthWindow:{(vip == null ? "" : vip.slidingWindowTickSize)}\n";
                }
            }
        }
    }
    
    public abstract void ApplyForces();

    public abstract int GetFloatInputCount();

    public abstract int GetBinaryInputCount();

    public abstract void SampleInput(PredictionInputRecord input);

    public abstract bool ValidateInput(float deltaTime, PredictionInputRecord input);

    public abstract void LoadInput(PredictionInputRecord input);

    [Command]
    void RequestSwitchToSharedObj(NetworkConnectionToClient conn = null)
    {
        Debug.Log($"[PlayerController][RequestSwitchToSharedObj]({netId}) conn={conn}");
        PredictionMirrorBridge.Instance.SwitchOwnership(conn != null ? conn.connectionId : 0, PredictionMirrorBridge.Instance.sharedPredMono);
    }
    
    [Command]
    void RequestSwitchBack(NetworkConnectionToClient conn = null)
    {
        Debug.Log($"[PlayerController][RequestSwitchBack]({netId}) conn={conn}");
        PredictionMirrorBridge.Instance.SwitchOwnership(conn != null ? conn.connectionId : 0, PredictionMirrorBridge.Instance.GetOriginalOwnedObject(conn.connectionId));
    }
}
