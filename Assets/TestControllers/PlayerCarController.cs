using Prediction.data;
using UnityEngine;

public class PlayerCarController : PlayerController
{
    public float powerMagnitude = 1f;
    public float steerPowerMagnitude = 5f;
    public float boostPowerMultiplier = 10f;

    private float steer;
    private float throttle;
    private bool isBoosting;
    
    public override void ApplyForces()
    {
        Vector3 torque = steer * steerPowerMagnitude * Vector3.up;
        Debug.Log($"APPLY t:{throttle} boost:{isBoosting} s:{steer} str:{torque}");
        rb.AddForce(throttle * powerMagnitude * (isBoosting ? boostPowerMultiplier : 1) * rb.transform.forward);
        rb.AddTorque(torque);
    }

    public override int GetFloatInputCount()
    {
        return 2;
    }

    public override int GetBinaryInputCount()
    {
        return 1;
    }

    public override void SampleInput(PredictionInputRecord data)
    {
        throttle = 0;
        if (Input.GetKey(KeyCode.UpArrow))
        {
            throttle += 1;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            throttle -= 1;
        }

        steer = 0;
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            steer -= 1;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            steer += 1;
        }
        
        data.WriteReset();
        data.WriteNextScalar(throttle);
        data.WriteNextScalar(steer);
        data.WriteNextBinary(Input.GetKey(KeyCode.Z));
    }

    public override bool ValidateState(uint tickId, PredictionInputRecord input)
    {
        return true;
    }

    public override void LoadInput(PredictionInputRecord input)
    {
        input.ReadReset();
        throttle = input.ReadNextScalar();
        steer = input.ReadNextScalar();
        isBoosting = input.ReadNextBool();
        
        throttle = Mathf.Clamp(throttle, -1f, 1f);
        steer = Mathf.Clamp(steer, -1f, 1f);
    }
}
