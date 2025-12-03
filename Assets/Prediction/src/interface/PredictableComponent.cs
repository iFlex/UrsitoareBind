namespace Prediction
{
    //All components that apply physics forces to the Rigidbody must implement this interface to support prediction.
    public interface PredictableComponent
    {
        void ApplyForces();
    }
}