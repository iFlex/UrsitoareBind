using Assets.Scripts.Systems.Events;
using DefaultNamespace;
using Mirror;
using Prediction;
using Prediction.data;
using Prediction.Interpolation;
using Unity.Cinemachine;
using UnityEngine;

public class PlayerController : NetworkBehaviour, PredictableComponent, PredictableControllableComponent
{
    [SerializeField] private Rigidbody rb;
    [SerializeField] private PredictedEntityVisuals pev;
    [SerializeField] private Renderer renderer;
    
    public ClientPredictedEntity clientPredictedEntity;
    public ServerPredictedEntity serverPredictedEntity;
    public CinemachineCamera pcam;
    
    public float powerMagnitude = 1f;
    public float boostPowerMultiplier = 3f;
    
    public static SafeEventDispatcher<PlayerController> spawned = new SafeEventDispatcher<PlayerController>();
    public static SafeEventDispatcher<PlayerController> despawned = new SafeEventDispatcher<PlayerController>();
    
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
        Debug.Log("PAINT_IT!");
        renderer.material.color = Color.yellow;
    }

    public override void OnStartServer()
    {
        serverPredictedEntity = new ServerPredictedEntity(30, rb, gameObject, new PredictableControllableComponent[1]{this}, new PredictableComponent[1]{this});
        serverPredictedEntity.gameObject = gameObject;
    }
    
    public override void OnStartClient()
    {
        if (!isServer || isOwned)
        {
            ConfigurePrediction(!isServer);
        }
    }

    public override void OnStartAuthority()
    {
        if (!isServer || isOwned)
        {
            clientPredictedEntity.isControlled = true;
            SingletonUtils.localCPE = clientPredictedEntity;
        }
        
        SetCamera(SingletonUtils.instance.povCam);
        Customize();
    }

    void ConfigurePrediction(bool detachVisuals)
    {
        clientPredictedEntity = new ClientPredictedEntity(30, rb, gameObject, new PredictableControllableComponent[1]{this}, new PredictableComponent[1]{this});
        clientPredictedEntity.gameObject = gameObject;
        clientPredictedEntity.interpolationsProvider = new MovingAverageInterpolator();
        SingletonUtils.localVisInterpolator = (MovingAverageInterpolator) clientPredictedEntity.interpolationsProvider;
        pev.SetClientPredictedEntity(clientPredictedEntity, detachVisuals);
    }

    private void Update()
    {
        if (pcam && !pcam.Follow && clientPredictedEntity != null)
        {
            pcam.Follow = pev.visualsEntity.transform;
            //pcam.Follow = clientPredictedEntity.gameObject.transform;
        }

        if (clientPredictedEntity != null && SingletonUtils.instance.clientText)
        {
            SingletonUtils.instance.clientText.text = $"Tick:{clientPredictedEntity.totalTicks}\n ServerDelay:{clientPredictedEntity.GetServerDelay()}\n Resimulations:{clientPredictedEntity.totalResimulations}\n AvgResimLen:{clientPredictedEntity.GetAverageResimPerTick()} TotalResimSteps:{clientPredictedEntity.totalResimulationSteps}\n Skips:{clientPredictedEntity.totalSimulationSkips}\n Velo:{clientPredictedEntity.rigidbody.linearVelocity.magnitude}\n DistThres:{SingletonUtils.CURRENT_DECIDER.distResimThreshold}\n SmoothWindow:{SingletonUtils.localVisInterpolator.slidingWindowTickSize}";
        }
    }

    private Vector3 inputDir;
    private bool isBoosting;
    public void ApplyForces()
    {
        rb.AddForce(powerMagnitude * (isBoosting ? boostPowerMultiplier : 1) * inputDir.normalized);
    }

    public int GetFloatInputCount()
    {
        return 3;
    }

    public int GetBinaryInputCount()
    {
        return 1;
    }

    public void SampleInput(PredictionInputRecord data)
    {
        Vector3 input = Vector3.zero;
        if (Input.GetKey(KeyCode.UpArrow))
        {
            //input += pcam.transform.forward;
            input += pcam.transform.up;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            //input += pcam.transform.forward * -1;
            input += pcam.transform.up * -1;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            input += pcam.transform.right * -1;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            input += pcam.transform.right;
        }
        input = input.normalized;
        
        data.WriteReset();
        data.WriteNextScalar(input.x);
        data.WriteNextScalar(input.y);
        data.WriteNextScalar(input.z);
        data.WriteNextBinary(Input.GetKey(KeyCode.Z));
    }

    public bool ValidateState(uint tickId, PredictionInputRecord input)
    {
        return true;
    }

    public void LoadInput(PredictionInputRecord input)
    {
        input.ReadReset();
        inputDir = new Vector3(input.ReadNextScalar(), input.ReadNextScalar(), input.ReadNextScalar());
        isBoosting = input.ReadNextBool();
    }
}
