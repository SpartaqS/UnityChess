//#define DEBUG
#define DEBUG_MCTS_SIMPLE
//#define USE_MOVE_ORDERING

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityChess.Engine;
using System.Threading.Tasks;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.Events;
using UnityChess.StrategicAI.Tools;
using System.Linq;

namespace UnityChess.StrategicAI
{
	public class AI_MCTS : IUCIEngine, IUCIEngineWithCustomSettings
	{
		protected Game game;
		protected Movement selectedMovement = null;
		protected int selectedMovementEval = 0;
		protected Movement bestMoveThisIteration = null;
		protected int evalBestMoveThisIteration = 0;
		Side controlledSide = Side.None;
		bool abortSearch = false;

		// working "game" copy - used to simulate moves
		protected Game currentGame;

		protected TranspositionTable transpositionTable;
#if USE_MOVE_ORDERING
		protected MoveOrdering moveOrdering;
#endif
		// AI settings


		/// <summary>
		/// Apply settings which directly affect the algorithm used
		/// </summary>
		/// <param name="customSettings"></param>
		void IUCIEngineWithCustomSettings.ApplyCustomSettings(UCIEngineCustomSettings customSettings)
		{
			if (!typeof(AIMCTSSettings).IsAssignableFrom(customSettings.GetType()))
				throw new System.InvalidOperationException("Provided custom settings are not for MCTS Strategic AI");

			AIMCTSSettings customSettingsToApply = (AIMCTSSettings)customSettings;

			playoutsPerLeaf = customSettingsToApply.PlayoutsPerLeaf;
			leafsToExplore = customSettingsToApply.LeafsToExplore;
		}

		readonly int maxStepsPerPlayout = 100;

		bool IUCIEngine.CanRequestRestart()
		{
			return false;
		}

		void IUCIEngine.Start()
		{
			// nothing to do at start
			transpositionTable = new TranspositionTable();
#if USE_MOVE_ORDERING
			moveOrdering = new MoveOrdering();
#endif
		}
		Task IUCIEngine.SetupNewGame(Game game, UnityEvent<Side> gameEndedEvent, UnityAction<Side,int> startNewGameHandler)
		{
			this.game = game;
			// this AI does not care about gameEndedEvent
			// this AI does not request for the game to be restarted
			return Task.CompletedTask;
		}

		/// <summary>
		/// perform Monte Carlo Tree Search on the current game state to find the best move
		/// </summary>
		/// <param name="timeoutMS"></param>
		/// <returns></returns>
		async Task<Movement> IUCIEngine.GetBestMove(int timeoutMS)
		{
			//float startTime = Time.realtimeSinceStartup;
			if (!game.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions))
				return null;

			if (!game.BoardTimeline.TryGetCurrent(out Board currentBoard))
				return null;

			controlledSide = currentConditions.SideToMove;
			selectedMovement = null;
			currentGame = new Game(currentConditions, currentBoard);
			transpositionTable.Clear();

			//TODO implement timeout
			InitDebugInfo();
			selectedMovement = MCTS(playoutsPerLeaf, leafsToExplore);

			await Task.Run(async () => { while (selectedMovement == null) await Task.Delay(10); });
			Movement bestMove = selectedMovement;
			//float endTime = Time.realtimeSinceStartup;
			//Debug.Log($"Move search time: {searchStopwatch.ElapsedMilliseconds}");
			LogDebugInfo();
			return bestMove;
		}

		void IUCIEngine.ShutDown(UnityEvent<Side> gameEndedEvent)
		{
			// nothing to do at shutdown
			// this AI does not subsribe to gameEndedEvent
		}

		// Diagnostics
		//public SearchDiagnostics searchDiagnostics;
		int leafsEvaluated;
		//int numTranspositions;
		System.Diagnostics.Stopwatch searchStopwatch;
		void InitDebugInfo()
		{
			searchStopwatch = System.Diagnostics.Stopwatch.StartNew();
			leafsEvaluated = 0;
		}

		void LogDebugInfo()
		{
			string debugLog = $"Selected move: {selectedMovement}\nMove search time: {searchStopwatch.ElapsedMilliseconds}\nleavesEvaluated: {leafsEvaluated}";
			Debug.Log(debugLog);
		}

		private Movement MCTS(int playoutsPerLeaf, int leafsToExplore)
		{
			if (!currentGame.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions))
			{
				throw new System.Exception("currentGame: could not retrieve currentConditions");
			}

			if (!currentGame.BoardTimeline.TryGetCurrent(out Board currentBoard))
			{
				throw new System.Exception("currentGame: could not retrieve currentBoard");
			}

			Dictionary<Piece, Dictionary<(Square, Square), Movement>> possibleMovesPerPiece = Game.CalculateLegalMovesForPosition(currentBoard, currentConditions);

			//TODO: do the actual algorithm here
			Random.InitState(0);
			List<Movement> movements = Game.UnpackMovementsToList(possibleMovesPerPiece);
			Node root = new Node(null, currentConditions.SideToMove.Complement(), null); // previous player's move has caused us to end up in this position that we are evalauting

			//TEMP (should use a random one within some tolerance to control difficulty)
			int bestResult = -2 * playoutsPerLeaf;
#if DEBUG_MCTS || DEBUG_MCTS_SIMPLE
			int bestResultPlayouts = -1;
			int bestResultWins = -2 * playoutsPerLeaf;
#endif
			Movement bestMove = null;

			List<int> randomNodeIndexesToExplore = GenerateRandomIntegers(0, movements.Count - 1, leafsToExplore);

	
			foreach (int randomMoveIndex in randomNodeIndexesToExplore) 
			{
				int totalResult = 0;
				int totalPlayouts = 0;
#if DEBUG_MCTS || DEBUG_MCTS_SIMPLE
				int wins = 0;
#endif
				Movement currentMove = movements[randomMoveIndex];
				for (int i = 0; i < playoutsPerLeaf; i++)
				{
					Node currentPlayerRandomNode = new Node(root, currentConditions.SideToMove, currentMove);
					root.AddChild(currentPlayerRandomNode);

					int result = Simulate(currentPlayerRandomNode);
					totalResult += result;
					totalPlayouts++;
#if DEBUG_MCTS || DEBUG_MCTS_SIMPLE
					if (result > 0)
					{
						wins++;
					}
#endif

#if DEBUG_MCTS
					string resultText;
					switch (result)
					{
						case -1:
							resultText = "loss";
							break;
						case 0:
							resultText = "stalemate";
							break;
						case 1:
							resultText = "win";
							break;
						default:
							resultText = "UNDEFINED";
							break;
					}
					Debug.Log($"simulation ended in: {resultText}");
#endif
				}

#if DEBUG_MCTS
				Debug.Log($"total result: {totalResult} ({totalPlayouts} playouts, ({wins} wins) ");
#endif
				if (totalResult > bestResult)
				{
					bestMove = currentMove;
					bestResult = totalResult;
#if DEBUG_MCTS || DEBUG_MCTS_SIMPLE
					bestResultPlayouts = totalPlayouts;
					bestResultWins = wins;
#endif
				}
			}

#if DEBUG || DEBUG_MCTS_SIMPLE
			Debug.Log($"best result: {bestResult} ({bestResultPlayouts} playouts, ({bestResultWins} wins) ");
#endif
			return bestMove;
		}

		private Node root;
		private int playoutsPerLeaf;
		private int leafsToExplore;

		/// <summary>
		/// Runs a single game to the end, starting from the current game state in node
		/// </summary>
		/// <param name="node"></param>
		/// <returns>-1 : loss of starting player; 0 : tie; 1: win of the starting player</returns>
		private int Simulate(Node node)
		{
			int result = SimulateStep(node.ExecutedMove, maxStepsPerPlayout);
			Backpropagate(node, result);
			return result;
		}

		private int SimulateStep(Movement performedMove, int stepsLeft)
		{
			if(stepsLeft < 1)
			{
#if DEBUG_MCTS
				Debug.Log($"AI_MCTS: {maxStepsPerPlayout} steps and no result");
#endif
				return 0;
			}

			// get possible moves by the player whose move it is after the performedMove
			if (!currentGame.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions))
			{
				throw new System.Exception("simulationGame: could not retrieve currentConditions");
			}

			if (!currentGame.BoardTimeline.TryGetCurrent(out Board currentBoard))
			{
				throw new System.Exception("simulationGame: could not retrieve currentBoard");
			}

			Dictionary<Piece, Dictionary<(Square, Square), Movement>> possibleMovesPerPiece = Game.CalculateLegalMovesForPosition(currentBoard, currentConditions);
			// Detect checkmate and stalemate when no legal moves are available
			if (possibleMovesPerPiece == null)
			{
				currentGame.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);
				if (latestHalfMove.CausedCheckmate) // the player who moved checkmated the "current" player
				{
					Debug.Log($"AI_MCTS: {currentConditions.SideToMove} checkmated with {stepsLeft} steps left");
					return -1;
				}
				else //(latestHalfMove.CausedStalemate)
				{
					Debug.Log($"AI_MCTS: {currentConditions.SideToMove} stalemated with {stepsLeft} steps left");
					return 0;
				}
			}
			// moves are possible: pick a random one and execute it
			List<Movement> movements = Game.UnpackMovementsToList(possibleMovesPerPiece);
			int randomMoveIndex = Random.Range(0, movements.Count - 1);
			Movement chosenMove = movements[randomMoveIndex];
			currentGame.TryExecuteMove(chosenMove);
			int simulationResult = -SimulateStep(chosenMove, stepsLeft - 1); // -1 because the result is returned from the berspective of the player whose move it is after executing chosenMove;
			currentGame.ResetGameToHalfMoveIndex((System.Math.Max(-1, currentGame.HalfMoveTimeline.HeadIndex - 1)));
			return simulationResult;
		}

		private void Backpropagate(Node node, int score)
		{
			while (node != null)
			{
				node.Update(score);
				node = node.Parent;
			}
		}

		// Example: Check if the game is finished
		private bool IsGameFinished()
		{
			// Implementation based on your game rules
			return false;
		}

		// Example: Make a random move
		private void MakeRandomMove()
		{
			// Implementation based on your game rules
		}

		// Example: Get the game result
		private int GetGameResult()
		{
			// Implementation based on your game rules
			return 0;
		}

#region Tree
		public class Node
		{
			public int Visits { get; set; }
			public int Score { get; set; }
			public Node Parent { get; set; }
			public List<Node> Children { get; set; }
			public Side Side { get; set; } // player whose move was executed

			public Movement ExecutedMove; // move that caused this position

			public Node()
			{
				Visits = 0;
				Score = 0;
				Children = new List<Node>();
			}

			public Node(Node parent, Side side, Movement executedMove)
			{
				Parent = parent;
				Visits = 0;
				Score = 0;
				Children = new List<Node>();
				Side = side;
				ExecutedMove = executedMove;
			}

			// Add child to the node
			public void AddChild(Node child)
			{
				Children.Add(child);
			}

			// Check if the node is a leaf
			public bool IsLeaf()
			{
				return Children.Count == 0;
			}

			// Calculate the Upper Confidence Bound (UCB) for this node
			public double UCB(int totalVisits)
			{
				if (Visits == 0)
					return double.MaxValue;
				else
					return Score / Visits + System.Math.Sqrt(2 * System.Math.Log(totalVisits) / Visits);
			}

			// Select the child with the highest UCB value
			public Node SelectChild()
			{
				return Children.OrderByDescending(c => c.UCB(Visits)).First();
			}

			// Update the node's score and visits
			public void Update(int score)
			{
				Visits++;
				Score += score;
			}
		}

#endregion

#region Tools
		private List<int> GenerateRandomIntegers(int a, int b, int n)
		{
			List<int> result = new List<int>();

			if (n > b - a + 1)
			{
				for (int i = a; i <= b; i++)
				{
					result.Add(i);
				}
				Shuffle(result);
			}
			else
			{
				HashSet<int> set = new HashSet<int>();
				while (result.Count < n)
				{
					int num = Random.Range(a, b + 1);
					if (!set.Contains(num))
					{
						set.Add(num);
						result.Add(num);
					}
				}
			}

			return result;
		}

		// Fisher-Yates shuffle algorithm to shuffle the list
		void Shuffle<T>(List<T> list)
		{
			int n = list.Count;
			while (n > 1)
			{
				n--;
				int k = Random.Range(0, n + 1);
				T value = list[k];
				list[k] = list[n];
				list[n] = value;
			}
		}

#endregion
	}
}
