using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityChess.SaveSystem;

namespace UnityChess
{
    public class AIDuelManager : MonoBehaviour , ISaveable
    {
        GameManager gameManager = null;
		SaveLoadManager saveLoadManager = null;
        [SerializeField] int duelsToRun = 3;
        [SerializeField] int executedDuelsCount = 0;
		[SerializeField] GameManager.AIType whiteAiType;
		[SerializeField] GameManager.AIType blackAiType;
		[SerializeField] int winsWhite = 0;
		[SerializeField] int winsBlack = 0;
		[SerializeField] int draws = 0;

		string saveFileName;


		private void Awake()
		{
            gameManager = FindObjectOfType<GameManager>();
			saveFileName = "duel_data_" +  whiteAiType.ToString() + "_vs_" +  blackAiType.ToString() + ".duel";
			saveLoadManager = FindObjectOfType<SaveLoadManager>();
		}

		private void Start()
		{
			gameManager.GameEndedEvent.AddListener(HandleGameEndedEvent);

			saveLoadManager.Load(saveFileName);

			gameManager.WhiteAiTypeToCreate = whiteAiType;
			gameManager.BlackAiTypeToCreate = blackAiType;
			StartNewDuelIfAppropriate();
		}



		private void HandleGameEndedEvent(Side winner)
		{
			switch (winner)
			{
				case Side.White:
					winsWhite += 1;
					break;
				case Side.Black:
					winsBlack += 1;
					break;
				case Side.None:
					draws += 1;
					break;
				default:
					throw new System.Exception("Invalid winner of the game: " + winner.ToString() + " | int value: " + (int)winner);
			}

			executedDuelsCount += 1;

			saveLoadManager.Save(saveFileName);

			StartNewDuelIfAppropriate();

		}

		private void StartNewDuelIfAppropriate()
		{
			if (executedDuelsCount < duelsToRun)
			{
				Debug.Log("Should start new duel now");
				gameManager.StartNewGame(true, true);
			}
			else
			{
				Debug.Log("Last duel complete");
			}
		}

		object ISaveable.CaptureState()
		{
			return new SaveData
			{
				DuelsToRun = duelsToRun,
				ExecutedDuelsCount = executedDuelsCount,
				WhiteSideAIType = (int)whiteAiType,
				BlackSideAIType = (int)blackAiType,
				WinsWhite = winsWhite,
				WinsBlack = winsBlack,
				Draws = draws
			};
		}

		void ISaveable.RestoreState(object state)
		{
			SaveData loadedState = (SaveData)state;

			duelsToRun = loadedState.DuelsToRun;
			executedDuelsCount = loadedState.ExecutedDuelsCount;
			whiteAiType = (GameManager.AIType)loadedState.WhiteSideAIType;
			blackAiType = (GameManager.AIType)loadedState.BlackSideAIType;
			winsWhite = loadedState.WinsWhite;
			winsBlack = loadedState.WinsBlack;
			draws = loadedState.Draws;
		}

		[System.Serializable]
		struct SaveData 
		{
			public int DuelsToRun;
			public int ExecutedDuelsCount;
			public int WhiteSideAIType;
			public int BlackSideAIType;
			public int WinsWhite;
			public int WinsBlack;
			public int Draws;
		}



	}
}
