using Prediction.data;
using UnityEngine;

public class PlayerBallController : PlayerController
{
    public float powerMagnitude = 1f;
    public float boostPowerMultiplier = 3f;
    
    private Vector3 inputDir;
    private bool isBoosting;
   
    public override void ApplyForces()
    {
        rb.AddForce(powerMagnitude * (isBoosting ? boostPowerMultiplier : 1) * inputDir.normalized);
    }

    public override int GetFloatInputCount()
    {
        return 3;
    }

    public override int GetBinaryInputCount()
    {
        return 1;
    }

    public override void SampleInput(PredictionInputRecord data)
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

    public override bool ValidateState(float deltaTime, PredictionInputRecord input)
    {
        return true;
    }

    public override void LoadInput(PredictionInputRecord input)
    {
        input.ReadReset();
        inputDir = new Vector3(input.ReadNextScalar(), input.ReadNextScalar(), input.ReadNextScalar());
        isBoosting = input.ReadNextBool();
    }
}
