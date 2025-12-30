using Prediction.data;
using UnityEngine;
using UnityEngine.InputSystem;
using Vector3 = UnityEngine.Vector3;

public class PlayerRocketController : PlayerController
{
    public float mass = 1f;
    public float gAcceleration = 9.89f;
    public float normalFriction = 0.01f;
    public float breakingFriction = 5f;
    public float fwdPower = 1f;
    public float pitchPower = 1f;
    public float yawPower = 1f;
    public float rollPower = 5f;
    public float boostPowerMultiplier = 10f;

    public float throttle;
    public float pitch;
    public float yaw;
    public float roll;
    public bool isBoosting;
    public bool isBreaking;
    
    public override void ApplyForces()
    {
        rb.AddForce(mass * gAcceleration * Vector3.up);
        rb.AddForce(throttle * fwdPower * (isBoosting ? boostPowerMultiplier : 1) * rb.transform.forward);
        rb.AddRelativeTorque(pitch * pitchPower * Vector3.left);
        rb.AddRelativeTorque(yaw * yawPower * Vector3.up);
        rb.AddRelativeTorque(roll * rollPower * Vector3.forward);
        if (isBreaking)
        {
            rb.linearDamping = breakingFriction;
        }
        else
        {
            rb.linearDamping = normalFriction;
        }
    }

    public override int GetFloatInputCount()
    {
        return 4;
    }

    public override int GetBinaryInputCount()
    {
        return 2;
    }

    public override void SampleInput(PredictionInputRecord data)
    {
        float _throttle = Gamepad.current.rightTrigger.value - Gamepad.current.leftTrigger.value;
        float _pitch = -Gamepad.current.rightStick.y.value;
        float _roll = -Gamepad.current.rightStick.x.value;
        float _yaw = Gamepad.current.leftStick.x.value;
        bool _isBoosting = Gamepad.current.buttonNorth.isPressed;
        bool _isBreaking = Gamepad.current.buttonSouth.isPressed;
        
        data.WriteReset();
        data.WriteNextScalar(_throttle);
        data.WriteNextScalar(_pitch);
        data.WriteNextScalar(_roll);
        data.WriteNextScalar(_yaw);
        data.WriteNextBinary(_isBoosting);
        data.WriteNextBinary(_isBreaking);
    }

    public override bool ValidateInput(float deltaTime, PredictionInputRecord input)
    {
        return true;
    }

    public override void LoadInput(PredictionInputRecord input)
    {
        input.ReadReset();
        throttle = input.ReadNextScalar();
        pitch = input.ReadNextScalar();
        roll = input.ReadNextScalar();
        yaw = input.ReadNextScalar();
        isBoosting = input.ReadNextBool();
        isBreaking = input.ReadNextBool();
        
        throttle = Mathf.Clamp(throttle, -1f, 1f);
        pitch = Mathf.Clamp(pitch, -1f, 1f);
        roll = Mathf.Clamp(roll, -1f, 1f);
        yaw = Mathf.Clamp(yaw, -1f, 1f);
    }
}
