using DefaultNamespace;
using Mirror;
using Unity.Cinemachine;
using UnityEngine;

public class PlayerMirrorController : NetworkBehaviour
{
    public Rigidbody rigidbody;
    public PredictedRigidbody prb;
    public CinemachineCamera pcam;
    
    Vector3 input = Vector3.zero;
    private bool boosting = false;
    public float powerMagnitude = 1f;
    public float boostPowerMultiplier = 3f;
    
    public override void OnStartAuthority()
    {
        SetCamera(SingletonUtils.instance.povCam);
    }
    
    public void SetCamera(CinemachineCamera newCamera)
    {
        pcam = newCamera;
    }
    
    // Update is called once per frame
    void FixedUpdate()
    {
        if (isClient && isOwned)
        {
            ReadInput();
            ApplyInput();
        }
    }

    void ReadInput()
    {
        input = Vector3.zero;
        if (Input.GetKey(KeyCode.UpArrow))
        {
            input += pcam.transform.forward;
            //input += pcam.transform.up;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            input += pcam.transform.forward * -1;
            //input += pcam.transform.up * -1;
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
        
        boosting = Input.GetKey(KeyCode.Z);
    }

    void ApplyInput()
    {
        ApplyInputLocal(prb.predictedRigidbody, input, boosting);
        CmdApplyForce(input, boosting);
    }

    [Command(requiresAuthority = false)]
    void CmdApplyForce(Vector3 inpt, bool boost)
    {
        ApplyInputLocal(rigidbody, inpt, boost);
    }

    void ApplyInputLocal(Rigidbody rb, Vector3 inpt, bool boost)
    {
        rb.AddForce(powerMagnitude * (boost ? boostPowerMultiplier : 1) * inpt.normalized);
    }
    
}
