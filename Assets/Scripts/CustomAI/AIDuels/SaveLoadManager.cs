using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityChess.SaveSystem
{    
    public class SaveLoadManager : MonoBehaviour
    {
        private string DefaultSavePath => $"{Application.persistentDataPath}/saves/quicksave.txt";
        private string SavePathCustom => $"{Application.persistentDataPath}/saves/";

        [ContextMenu("Save")] //so we can use this in the inspector (normally should be disabled?)
        private void Save()
        {
            Debug.Log("Saving duel stats at: " + Application.persistentDataPath);
            var state = LoadFile(DefaultSavePath);
            CaptureState(state);
            SaveFile(state, DefaultSavePath);
        }

        public void Save(string fileName)
        {
            string finalPath = SavePathCustom + fileName;
            Debug.Log("Saving duel stats at: " + finalPath);
            var state = LoadFile(finalPath);
            CaptureState(state);
            SaveFile(state, finalPath);
        }

        [ContextMenu("Load")] //so we can use this in the inspector (normally should be disabled?)
        public void Load()
        {
            Load(DefaultSavePath);
        }

        public void Load(string fileName)
        {
            string finalPath = SavePathCustom + fileName;
            Debug.Log("Loading : " + finalPath);
            var state = LoadFile(finalPath);
            RestoreState(state);
        }

        private void SaveFile(object state, string savePath)
        {
            using (var stream = File.Open(savePath, FileMode.Create))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(stream, state);
            }
        }

        private Dictionary<string, object> LoadFile(string savePath)
        {
            if(!File.Exists(savePath)) // if no save file found, return empty
            {
                return new Dictionary<string, object>();
            }

            using (FileStream stream = File.Open(savePath, FileMode.Open)) // if save file found, load it
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                return (Dictionary<string, object>)binaryFormatter.Deserialize(stream);
            }
        }

        private void CaptureState(Dictionary<string,object> state)
        {
            foreach (var saveable in FindObjectsOfType<SaveableEntity>())
            {
                state[saveable.Id] = saveable.CaptureState();
            }
        }

        private void RestoreState(Dictionary<string,object> state)
        {
            foreach (var saveable in FindObjectsOfType<SaveableEntity>())
            {
                if(state.TryGetValue(saveable.Id,out object savedValue))
                {
                    saveable.RestoreState(savedValue);
                }
            }
        }
    }
}
