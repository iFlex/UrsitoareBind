using Assets.Scripts.Systems.Events;
using DefaultNamespace;
using Mirror;
using Prediction;
using Prediction.data;
using Prediction.Interpolation;
using Prediction.wrappers;
using Unity.Cinemachine;
using UnityEngine;

public abstract class PlayerController : NetworkBehaviour, PredictableComponent, PredictableControllableComponent
{
    [SerializeField] protected Rigidbody rb;
    [SerializeField] protected PredictedEntityVisuals pev;
    [SerializeField] protected Renderer renderer;
    
    public ClientPredictedEntity clientPredictedEntity;
    public ServerPredictedEntity serverPredictedEntity;
    public CinemachineCamera pcam;
    public CinemachineCamera fcam;
    
    public static SafeEventDispatcher<PlayerController> spawned = new SafeEventDispatcher<PlayerController>();
    public static SafeEventDispatcher<PlayerController> despawned = new SafeEventDispatcher<PlayerController>();
    public PredictedMonoBehaviour predictedMono;
    
    void Start()
    {
        spawned.Dispatch(this);
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

    public override void OnStartServer()
    {
        serverPredictedEntity = new ServerPredictedEntity(30, rb, gameObject, new PredictableControllableComponent[1]{this}, new PredictableComponent[1]{this});
        serverPredictedEntity.gameObject = gameObject;
    }
    
    public override void OnStartClient()
    {
        ConfigurePrediction(isOwned && !isServer);
    }

    public override void OnStartAuthority()
    {
        clientPredictedEntity.SetControlledLocally(isOwned);
        SingletonUtils.localCPE = clientPredictedEntity;
        SetCamera(SingletonUtils.instance.povCam);
        Customize();
    }

    protected virtual void ConfigurePrediction(bool detachVisuals)
    {
        clientPredictedEntity = new ClientPredictedEntity(30, rb, gameObject, new PredictableControllableComponent[1]{this}, new PredictableComponent[1]{this});
        clientPredictedEntity.gameObject = gameObject;
        pev.SetInterpolationProvider(new MovingAverageInterpolator());
        pev.SetClientPredictedEntity(clientPredictedEntity, detachVisuals);
        
        SingletonUtils.localVisInterpolator = (MovingAverageInterpolator) pev.interpolationProvider;
    }

    private void Update()
    {
        if (pcam && !pcam.Follow && clientPredictedEntity != null)
        {
            pcam.Follow = pev.visualsEntity.transform;
            SingletonUtils.instance.topCam.Follow = pev.visualsEntity.transform;
        }

        if (clientPredictedEntity != null && SingletonUtils.instance.clientText)
        {
            SingletonUtils.instance.clientText.text = $"Tick:{clientPredictedEntity.totalTicks}\n ServerDelay:{clientPredictedEntity.GetServerDelay()}\n Resimulations:{clientPredictedEntity.totalResimulations}\n AvgResimLen:{clientPredictedEntity.GetAverageResimPerTick()} TotalResimSteps:{clientPredictedEntity.totalResimulationSteps}\n Skips:{clientPredictedEntity.totalSimulationSkips}\n Velo:{clientPredictedEntity.rigidbody.linearVelocity.magnitude}\n DistThres:{SingletonUtils.CURRENT_DECIDER.distResimThreshold}\n SmoothWindow:{SingletonUtils.localVisInterpolator.slidingWindowTickSize}\n FPS:{1/Time.deltaTime}\n FrameTime:{Time.deltaTime}";
        }
    }
    
    public abstract void ApplyForces();

    public abstract int GetFloatInputCount();

    public abstract int GetBinaryInputCount();

    public abstract void SampleInput(PredictionInputRecord input);

    public abstract bool ValidateInput(float deltaTime, PredictionInputRecord input);

    public abstract void LoadInput(PredictionInputRecord input);
}
