using System;
using System.Collections.Generic;
using Prediction.data;
using Prediction.Interpolation;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DefaultNamespace
{
    public class TestVisualsSmootingController : MonoBehaviour
    {
        [SerializeField] private LineRenderer original;
        [SerializeField] private LineRenderer smooth;
        private Vector3[] input;
        private void Start()
        {
            float movePerTick = 15f / 50;
            float jumpPerTick = movePerTick * 3;
            
            HashSet<int> jumpPos = new HashSet<int>();
            jumpPos.Add(19);
            jumpPos.Add(75);
            jumpPos.Add(175);
            
            int totalTicks = 200;
            input = new Vector3[totalTicks];
            Vector3 acc = Vector3.zero;
            for (int i = 0; i < totalTicks; ++i)
            {
                acc += jumpPos.Contains(i) ? Vector3.forward * jumpPerTick : (Vector3.right * movePerTick + Random.Range(0.01f, 0.25f) * movePerTick * Vector3.forward);
                input[i] = acc;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                plot(input);
            }
        }

        public void plot(Vector3[] input)
        {
            original.positionCount = input.Length;
            smooth.positionCount = input.Length;
            MovingAverageInterpolator interpolator = new MovingAverageInterpolator();
            
            for (int i = 0; i < input.Length; i++)
            {
                original.SetPosition(i, input[i]);
                PhysicsStateRecord psr = new PhysicsStateRecord();
                psr.tickId = (uint) i;
                psr.position = input[i];
                interpolator.Add(psr);
                smooth.SetPosition(i, interpolator.averagedBuffer.GetEnd().position);
            }
        }
    }
}