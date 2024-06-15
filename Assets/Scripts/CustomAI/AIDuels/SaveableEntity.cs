using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityChess.SaveSystem
{
    public class SaveableEntity : MonoBehaviour
    {
        [SerializeField] private string id; // saveSystem id

        public string Id => id;

        public object CaptureState()
        {
            var state = new Dictionary<string, object>();

            foreach(var saveable in GetComponents<ISaveable>())
            {
                state[saveable.GetType().ToString()] = saveable.CaptureState();
            }

            return state;
        }

        public void RestoreState(object state)
        {
            var stateDictionary = (Dictionary<string, object>)state;

            foreach (var saveable in GetComponents<ISaveable>())
            {
                string typeName = saveable.GetType().ToString();
                if(stateDictionary.TryGetValue(typeName, out object savedValue))
                {
                    saveable.RestoreState(savedValue);
                }
            }
        }
    }
}
