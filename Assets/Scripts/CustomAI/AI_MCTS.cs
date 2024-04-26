#define DEBUG
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
			if (!typeof(AIMinMaxSettings).IsAssignableFrom(customSettings.GetType()))
				throw new System.InvalidOperationException("Provided custom settings are not for MinMax Strategic AI");

			AIMCTSSettings customSettingsToApply = (AIMCTSSettings)customSettings;

			playoutsPerLeaf = customSettingsToApply.PlayoutsPerLeaf;
		}

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
			selectedMovement = MCTS();

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
		int leavesEvaluated;
		//int numTranspositions;
		System.Diagnostics.Stopwatch searchStopwatch;
		void InitDebugInfo()
		{
			searchStopwatch = System.Diagnostics.Stopwatch.StartNew();
			leavesEvaluated = 0;
		}

		void LogDebugInfo()
		{
			string debugLog = $"Selected move: {selectedMovement}\nMove search time: {searchStopwatch.ElapsedMilliseconds}\nleavesEvaluated: {leavesEvaluated}";
			Debug.Log(debugLog);
		}

		protected List<Movement> UnpackMovementsToList(Dictionary<Piece, Dictionary<(Square, Square), Movement>> possibleMovesPerPiece)
		{
			List<Movement> movements = new List<Movement>();

			foreach (Piece piece in possibleMovesPerPiece.Keys)
			{
				foreach (Movement move in possibleMovesPerPiece[piece].Values)
				{
					if (piece is Pawn)
					{
						if (move is PromotionMove)          // TODO: generate moves with promotions for each piece type that can be obtained via a promotion
						{// pawn promotes: pick a promotion for it
							Side currentSide = piece.Owner;
							(move as PromotionMove).SetPromotionPiece(new Queen(currentSide));
							movements.Add(move);
							continue;
						}

					}
					movements.Add(move);
				}
			}

			// TODO: order the movements list to speed up search
			return movements;
		}

		private Node root;
		private int playoutsPerLeaf;

		private int Simulate(Node node)
		{
			// use GameManager with the current board in node to run a simulation
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

			public Node(Node parent, Side side)
			{
				Parent = parent;
				Visits = 0;
				Score = 0;
				Children = new List<Node>();
				Side = side;
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
	}
}
