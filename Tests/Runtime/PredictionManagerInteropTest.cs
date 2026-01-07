#if (UNITY_EDITOR) 
using NUnit.Framework;
using Prediction.policies.singleInstance;
using Prediction.Tests.mocks;

namespace Prediction.Tests
{
    public class PredictionManagerInteropTest
    {
        MockPhysicsController physicsController;
        SimpleConfigurableResimulationDecider resimDecider = new SimpleConfigurableResimulationDecider();
        PredictionManager managerClient;
        PredictionManager managerServer;
        
        //TODO: these tests may not be relevant
        [SetUp]
        public void SetUp()
        {
            physicsController = new MockPhysicsController();
            
        }
        
        [Test]
        public void HappyPathMinimalDelay()
        {
            
        }
        
        //TODO: test swapping ownership, see that the buffers are cleared and new ticks are accepted correctly
    }
}
#endif