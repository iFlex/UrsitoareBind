using System.Collections.Generic;
using DefaultNamespace;
using Prediction;
using Prediction.Interpolation;
using Prediction.policies.singleInstance;
using Prediction.Wrappers;
using UnityEngine;

public class PredictionTextDebugger : MonoBehaviour
{
    public PredictionManager predictionManager;
    [SerializeField] TMPro.TMP_Text server;
    [SerializeField] TMPro.TMP_Text client;
    
    // Update is called once per frame
    void Update()
    {
        if (predictionManager == null)
        {
            predictionManager = PredictionManager.Instance;
            return;
        }
        
        if (predictionManager.isClient)
        {
            UpdateClient();
        }

        if (predictionManager.isServer)
        {
            UpdateServer();
        }
    }

    void UpdateServer()
    {
        //TODO: this line should be somewhere else...
        server.text = $"NetLayer:: Latency:{SingletonUtils.instance.latencySim.latency} Jitter:{SingletonUtils.instance.latencySim.jitter} stCnt:{PredictionManager.Instance.clientStatesReceived} FlwrWindowSz:{MovingAverageInterpolator.FOLLOWER_SMOOTH_WINDOW}\n";
        //TODO: make _serverEntityToId private again
        foreach (KeyValuePair<ServerPredictedEntity, uint> pair in predictionManager._serverEntityToId)
        {
            //TODO: NOTE: i think elements remain in the buffer somehow and cause the range: reading to be incorrect and keep going up...
            int cid = predictionManager.GetOwner(pair.Key);
            server.text += $"connId:{cid} id:{pair.Value} tickId:{pair.Key.GetTickId()} lastConnTick:{predictionManager._connIdToLatestTick.GetValueOrDefault(cid, uint.MaxValue)} ticksNoInpt:{pair.Key.ticksWithoutInput} catchTicks:{pair.Key.ticksPerCatchupSection} bfrTicks:{pair.Key.totalBufferingTicks} bfrWipe:{pair.Key.catchupBufferWipes} catchup:{pair.Key.catchupTicks} skipped:{pair.Key.ticksPerCatchupSection} range:{pair.Key.BufferSize()} fill:{pair.Key.BufferFill()} inputJumps:{pair.Key.inputJumps} maxDelay:{pair.Key.maxClientDelay} rcvCnt:{pair.Key.clUpdateCount} rcv+cnt:{pair.Key.clAddedUpdateCount}\n";
        }
    }
    
    void UpdateClient()
    {
        if (PredictionManager.Instance.GetLocalEntity() == null)
        {
            client.text = "?";
        }
        else
        {
            client.text = $"ID:{PredictionManager.Instance.GetLocalEntity().id} RESIMULATING:{PredictionManager.Instance.resimulating}\n" +
                      $"Tick:{PredictionManager.Instance.tickId} | {PredictionManager.Instance.GetLocalEntity().lastTick}\n " + 
                      $"ServerDelay:{PredictionManager.Instance.GetLocalEntity().GetServerDelay()} | STick:{PredictionManager.Instance.GetLocalEntity().serverStateBuffer.GetEndTick()}\n " +
                      $"sv_oldTicks:{PredictionManager.Instance.GetLocalEntity().oldServerTickCount}\n " +
                      $"Resimulations:{PredictionManager.Instance.totalResimulations}\n " +
                      $"AuthResims:{PredictionManager.Instance.totalResimulationsDueToAuthority}\n " +
                      $"FlwrResims:{PredictionManager.Instance.totalResimulationsDueToFollowers}\n " +
                      $"BothResims:{PredictionManager.Instance.totalResimulationsDueToBoth}\n " +
                      $"AvgResimLen:{PredictionManager.Instance.GetAverageResimPerTick()}\n" +
                      $"TotalResimSteps:{PredictionManager.Instance.totalResimulationSteps} ({(float)PredictionManager.Instance.totalResimulationSteps / PredictionManager.Instance.tickId * 100}%)\n " +
                      $"ResimSkips:{PredictionManager.Instance.totalResimulationsSkipped}\n " +
                      $"ResimSkipsTooSoon:{PredictionManager.Instance.resimSkipNotEnoughHistory}\n " +
                      $"MaxSvDelay:{PredictionManager.Instance.GetLocalEntity().maxServerDelay}\n " +
                      $"Velo:{PredictionManager.Instance.GetLocalEntity().rigidbody.linearVelocity.magnitude}\n " +
                      $"SvMissingHist:{PredictionManager.Instance.GetLocalEntity().countMissingServerHistory}\n " +
                      $"DIST_TRES:{((SimpleConfigurableResimulationDecider)PredictionManager.SNAPSHOT_INSTANCE_RESIM_CHECKER).distResimThreshold}\n " +
                      $"SMOOTH_WNDW:{(SingletonUtils.localVisInterpolator != null ? SingletonUtils.localVisInterpolator.slidingWindowTickSize : -1)}\n " +
                      $"FPS:{1/Time.deltaTime}\n " +
                      $"FrameTime:{Time.deltaTime}\n";

        }
        
            foreach (PredictedEntity pe in PredictionManager.Instance._predictedEntities)
            {
                if (pe.GetClientEntity() != PredictionManager.Instance.GetLocalEntity())
                {
                    PredictedEntityVisuals pev = pe.GetVisualsControlled();
                    MovingAverageInterpolator vip = null;
                    if (pev)
                    {
                        vip = (MovingAverageInterpolator) pev.interpolationProvider;
                    }
                    
                    client.text +=
                        $"\nid:{pe.GetId()} ResimTicksAuth:{pe.GetClientEntity().resimTicksAsAuthority} ResimTicksFlwr:{pe.GetClientEntity().resimTicksAsFollower} smthWindow:{(vip == null ? "" : vip.slidingWindowTickSize)} noSvHst:{pe.GetClientEntity().countMissingServerHistory}\n";
                }
            }
    }
}
