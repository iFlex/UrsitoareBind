using System.Collections.Generic;
using UnityEngine;

namespace Prediction.wrappers
{
    public class WrapperHelpers
    {
        public static PredictableControllableComponent[] GetControllableComponents(MonoBehaviour[] objects)
        {
            List<PredictableControllableComponent> compos = new List<PredictableControllableComponent>();
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] is PredictableControllableComponent)
                {
                    compos.Add((PredictableControllableComponent)objects[i]);
                }   
            }
            return compos.ToArray();
        }
        
        public static PredictableComponent[] GetComponents(MonoBehaviour[] controllable)
        {
            List<PredictableComponent> compos = new List<PredictableComponent>();
            for (int i = 0; i < controllable.Length; i++)
            {
                if (controllable[i] is PredictableComponent)
                {
                    compos.Add((PredictableComponent)controllable[i]);
                }   
            }
            return compos.ToArray();
        }
        
        public static PredictableComponent[] GetComponents(MonoBehaviour[] controllable, MonoBehaviour[] predictable)
        {
            List<PredictableComponent> compos = new List<PredictableComponent>();
            for (int i = 0; i < controllable.Length; i++)
            {
                if (controllable[i] is PredictableComponent)
                {
                    compos.Add((PredictableComponent)controllable[i]);
                }   
            }
            for (int i = 0; i < predictable.Length; i++)
            {
                if (controllable[i] is PredictableComponent && !compos.Contains((PredictableComponent)predictable[i]))
                {
                    compos.Add((PredictableComponent)controllable[i]);
                }   
            }
            return compos.ToArray();
        }
    }
}