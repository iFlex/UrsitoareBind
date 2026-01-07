using Prediction.data;
using UnityEngine;

namespace Prediction.Interpolation
{
    public interface VisualsInterpolationsProvider
    {
        void Update(float deltaTime);
        void Add(PhysicsStateRecord record);
        void SetInterpolationTarget(Transform t);
        void Reset();
        void SetControlledLocally(bool isLocalAuthority);
    }
}