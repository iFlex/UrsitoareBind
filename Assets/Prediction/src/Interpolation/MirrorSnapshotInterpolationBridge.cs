using System.Collections.Generic;
using Mirror;
using Prediction.data;
using UnityEngine;

namespace Prediction.Interpolation
{
    public class MirrorSnapshotInterpolationBridge : VisualsInterpolationsProvider
    {
        public readonly SortedList<double, TransformSnapshot> snapshots = new SortedList<double, TransformSnapshot>(16);
        private Transform transform;
        
        //TODO: examine again, lifted from Mirror examples
        
        // for smooth interpolation, we need to interpolate along server time.
        // any other time (arrival on client, client local time, etc.) is not
        // going to give smooth results.
        double localTimeline;

        // catchup / slowdown adjustments are applied to timescale,
        // to be adjusted in every update instead of when receiving messages.
        double localTimescale = 1;

        // we use EMA to average the last second worth of snapshot time diffs.
        // manually averaging the last second worth of values with a for loop
        // would be the same, but a moving average is faster because we only
        // ever add one value.
        ExponentialMovingAverage driftEma;
        ExponentialMovingAverage deliveryTimeEma; // average delivery time (standard deviation gives average jitter)

        
        public void SetInterpolationTarget(Transform t)
        {
            transform = t;
            //sendRate: 50hz
            driftEma = new ExponentialMovingAverage(50 * 1);
            deliveryTimeEma = new ExponentialMovingAverage(50 * 2);
        }
        
        public void Update(float deltaTime)
        {
            if (snapshots.Count == 0)
            {
                Debug.Log($"[MirrorSnapshotInterpolationBridge][update]SKIPPED");
                return;
            }
            
            // step the interpolation without touching time.
            // NetworkClient is responsible for time globally.
            /*SnapshotInterpolation.StepInterpolation(
                snapshots,
                NetworkTime.localTime, // == NetworkClient.localTimeline from snapshot interpolation
                out TransformSnapshot from,
                out TransformSnapshot to,
                out double t);
*/
            SnapshotInterpolation.Step(snapshots,
                Time.unscaledDeltaTime,
                ref localTimeline,
                localTimescale,
                out TransformSnapshot fromSnapshot,
                out TransformSnapshot toSnapshot,
                out double t);
            
            // interpolate & apply
            TransformSnapshot computed = TransformSnapshot.Interpolate(fromSnapshot, toSnapshot, t);
            transform.position = computed.position;
            transform.rotation = computed.rotation;
            Debug.Log($"[MirrorSnapshotInterpolationBridge][update] from:{fromSnapshot.position} toSnapshot:{toSnapshot.position} t:{t}");
        }
        
        public void Add(PhysicsStateRecord record)
        {
            //Debug.Log($"[MirrorSnapshotInterpolationBridge][Add] time:{NetworkTime.localTime} srvTime:{record.tmpServerTime} record:{record}");

            //TODO: ALLOCS...?
            var snap = new TransformSnapshot(
                record.tmpServerTime, //GetTime(record), // arrival remote timestamp. NOT remote time.
                NetworkTime.localTime, //GetTime(localTickId),
                record.position,
                record.rotation,
                Vector3.one
            );
            // insert transform snapshot
            /*
             SnapshotInterpolation.InsertIfNotExists(
                snapshots,
                NetworkClient.snapshotSettings.bufferLimit,
                new TransformSnapshot(
                    record.tmpServerTime, //GetTime(record), // arrival remote timestamp. NOT remote time.
                    NetworkTime.localTime, //GetTime(localTickId),
                    record.position,
                    record.rotation,
                    Vector3.one
                )
            );
            */

            SnapshotInterpolation.InsertAndAdjust(
                snapshots,
                16,
                snap,
                ref localTimeline, // local interpolation time based on server time
                ref localTimescale, // timeline multiplier to apply catchup / slowdown over time)
                0,
                0.15f,
                0.66f,
                0.33f,
                ref driftEma,
                -1,
                1,
                ref deliveryTimeEma
                );
        }
        
        double GetTime(PhysicsStateRecord record)
        {
            return GetTime(record.tickId);
        }
        
        double GetTime(uint tickId)
        {
            return tickId * Time.fixedTime;
        }
    }
}