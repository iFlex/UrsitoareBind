#if (UNITY_EDITOR)
using NUnit.Framework;
using Prediction.data;
using Prediction.Simulation;
using UnityEngine;

namespace Prediction.Tests.simulation
{
    public class RewindablePhysicsControllerTest
    {
        private RewindablePhysicsController controller;

        [SetUp]
        public void SetUp()
        {
            controller = new RewindablePhysicsController();
        }

        [Test]
        public void TestRewindAndResim()
        {
            GameObject test1 = new GameObject("test1");
            test1.transform.position = Vector3.zero;
            Rigidbody rigidbody1 = test1.AddComponent<Rigidbody>();
            
            GameObject test2 = new GameObject("test2");
            test2.transform.position = Vector3.right;
            Rigidbody rigidbody2 = test2.AddComponent<Rigidbody>();
            
            GameObject test3 = new GameObject("test3");
            test3.transform.position = Vector3.right * 2;
            Rigidbody rigidbody3 = test3.AddComponent<Rigidbody>();

            controller.Track(rigidbody1);
            controller.Track(rigidbody2);
            controller.Track(rigidbody3);
            
            PhysicsStateRecord final1 = new PhysicsStateRecord();
            PhysicsStateRecord final2 = new PhysicsStateRecord();
            PhysicsStateRecord final3 = new PhysicsStateRecord();
            
            PhysicsStateRecord psr1 = new PhysicsStateRecord();
            PhysicsStateRecord psr2 = new PhysicsStateRecord();
            PhysicsStateRecord psr3 = new PhysicsStateRecord();
            
            uint rewindBy = 5;
            uint maxTick = 11;
            Vector3 force = Vector3.forward * 10;
            for (int i = 0; i < 10; ++i) //10 ticks
            {
                rigidbody1.AddForce(force);
                rigidbody2.AddForce(force);
                rigidbody3.AddForce(force);
                
                Assert.AreEqual(i + 1, controller.GetTick());
                controller.Simulate();
                if (i == maxTick - rewindBy - 1)
                {
                    psr1.From(rigidbody1);
                    psr2.From(rigidbody2);
                    psr3.From(rigidbody3);
                }
                Assert.AreEqual(i + 2, controller.GetTick());
            }
            final1.From(rigidbody1);
            final2.From(rigidbody2);
            final3.From(rigidbody3);
            
            Assert.AreEqual(maxTick, controller.GetTick());
            controller.Rewind(rewindBy);
            uint resimFromTick = maxTick - rewindBy + 1;
            Assert.AreEqual(resimFromTick, controller.GetTick());
            AssertEqualState(rigidbody1, psr1);
            AssertEqualState(rigidbody2, psr2);
            AssertEqualState(rigidbody3, psr3);

            for (int i = 1; i < rewindBy; ++i) //4 ticks as the very first one does not require a simulation step
            {
                Assert.AreEqual(resimFromTick + i - 1, controller.GetTick());
                rigidbody1.AddForce(force);
                rigidbody2.AddForce(force);
                rigidbody3.AddForce(force);
                //TODO: resimulate is no needed as a separate method ?!
                controller.Resimulate(null);
                Assert.AreEqual(resimFromTick + i, controller.GetTick());
            }
            
            Assert.AreEqual(maxTick, controller.GetTick());
            AssertEqualState(rigidbody1, final1);
            AssertEqualState(rigidbody2, final2);
            AssertEqualState(rigidbody3, final3);
        }

        void AssertEqualState(Rigidbody body, PhysicsStateRecord expected)
        {
            Assert.AreEqual(expected.position, body.position);
            Assert.AreEqual(expected.rotation, body.rotation);
            Assert.AreEqual(expected.velocity, body.linearVelocity);
            Assert.AreEqual(expected.angularVelocity, body.angularVelocity);
        }
        
        //TODO: try to resimulate too far in the past
        //TODO: try to resimulate 1 step behind
        //TODO: try to resimulate with object despawn
        //TODO: try to resimulate with object spawn
    }
}
#endif