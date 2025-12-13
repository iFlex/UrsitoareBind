using Prediction.data;
using UnityEngine;

public class PhysicsTestController : MonoBehaviour
{
    [SerializeField] private GameObject obj;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Rigidbody other;

    public bool stepIt = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Physics.simulationMode = SimulationMode.Script;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.V))
        {
            rb.AddForce(Vector3.left * 100);
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            other.AddForce(Vector3.left * 100);
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            stepIt = true;
        }
    }

    private PhysicsStateRecord psr = new PhysicsStateRecord();
    void FixedUpdate()
    {
        if (stepIt)
        {
            psr.To(rb);
        }
        Physics.Simulate(Time.fixedDeltaTime);
        psr.From(rb);
    }
}
