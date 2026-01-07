using Sector0.Events;
using DefaultNamespace;
using Mirror;
using Prediction;
using Prediction.data;
using Prediction.Interpolation;
using Prediction.Wrappers;
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
