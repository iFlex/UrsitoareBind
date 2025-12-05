using Prediction.data;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerRocketController : PlayerController
{
    public float fwdPower = 1f;
    public float verticalPower = 1f;
    public float sidewaysPower = 1f;
    public float steerPowerMagnitude = 5f;
    public float boostPowerMultiplier = 10f;

    private float steerVertical;
    private float steerHorizontal;
    private float throttleVertical;
    private float throttleSideways;
    private float throttleForwards;
    private bool isBoosting;
    
    public override void ApplyForces()
    {
        Vector3 torque = new Vector3(steerVertical * steerPowerMagnitude, steerHorizontal * steerPowerMagnitude, 0);
        rb.AddForce(throttleForwards * fwdPower * (isBoosting ? boostPowerMultiplier : 1) * rb.transform.forward);
        rb.AddForce(throttleSideways * sidewaysPower * (isBoosting ? boostPowerMultiplier : 1) * rb.transform.right);
        rb.AddForce(throttleVertical * verticalPower * (isBoosting ? boostPowerMultiplier : 1) * Vector3.up);
        rb.AddTorque(torque);
        
        Debug.Log($"[PlayerRocketController][ApplyForces] tf:{throttleForwards} ts:{throttleSideways} tv:{throttleVertical} sv:{steerVertical} sh:{steerHorizontal} b:{isBoosting}");
    }

    public override int GetFloatInputCount()
    {
        return 4;
    }

    public override int GetBinaryInputCount()
    {
        return 1;
    }

    public override void SampleInput(PredictionInputRecord data)
    {
        float _throttleForwards = Gamepad.current.rightTrigger.value - Gamepad.current.leftTrigger.value;
        float _throttleSideways = Gamepad.current.leftStick.x.value;
        float _throttleVertical = Gamepad.current.leftStick.y.value;
        float _steerVertical = Gamepad.current.rightStick.y.value;
        float _steerHorizontal = Gamepad.current.rightStick.x.value;
        bool _isBoosting = Gamepad.current.buttonNorth.isPressed;
        
        data.WriteReset();
        data.WriteNextScalar(_throttleForwards);
        data.WriteNextScalar(_throttleSideways);
        data.WriteNextScalar(_throttleVertical);
        data.WriteNextScalar(_steerVertical);
        data.WriteNextScalar(_steerHorizontal);
        data.WriteNextBinary(_isBoosting);
    }

    public override bool ValidateInput(float deltaTime, PredictionInputRecord input)
    {
        return true;
    }

    public override void LoadInput(PredictionInputRecord input)
    {
        input.ReadReset();
        throttleForwards = input.ReadNextScalar();
        throttleSideways = input.ReadNextScalar();
        throttleVertical = input.ReadNextScalar();
        steerVertical = input.ReadNextScalar();
        steerHorizontal = input.ReadNextScalar();
        isBoosting = input.ReadNextBool();
        
        Debug.Log($"[PlayerRocketController] tf:{throttleForwards} ts:{throttleSideways} tv:{throttleVertical} sv:{steerVertical} sh:{steerHorizontal} b:{isBoosting}");
        throttleForwards = Mathf.Clamp(throttleForwards, -1f, 1f);
        throttleSideways = Mathf.Clamp(throttleSideways, -1f, 1f);
        throttleVertical = Mathf.Clamp(throttleVertical, -1f, 1f);
        steerVertical = Mathf.Clamp(steerVertical, -1f, 1f);
        steerHorizontal = Mathf.Clamp(steerHorizontal, -1f, 1f);
    }
}
