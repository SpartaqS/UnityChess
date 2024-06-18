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

#if DEBUG_MCTS || DEBUG_MCTS_SIMPLE
		int debug_wins = 0;
		int debug_losses = 0;
#endif

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
			if (!currentGame.ConditionsTimeline.TryGetCurrent(out GameConditions initialConditions))
			{
				throw new System.Exception("currentGame: could not retrieve currentConditions");
			}

			if (!currentGame.BoardTimeline.TryGetCurrent(out Board initialBoard))
			{
				throw new System.Exception("currentGame: could not retrieve currentBoard");
			}

			// optional: set random seed to have reproducible decisions
			//Random.InitState(0);

			if (root == null)
			{
				root = new Node(null, initialConditions.SideToMove.Complement(), null); // previous player's move has caused us to end up in this position that we are evalauting
			} 
			else // this is not the first move of the AI_MCTS: find root in previous' player's moves
			{
				Game currentGameCopy = new Game(game);
				foreach (Node node in root.Children)
				{

				}
			}


			Movement bestMove = null;

			int totalResult = 0;
			int totalPlayouts = 0;
#if DEBUG_MCTS || DEBUG_MCTS_SIMPLE
			debug_wins = 0;
			debug_losses = 0;
#endif

			for (int i = 0; i < leafsToExplore; i++) 
			{
				currentGame = new Game(initialConditions, initialBoard); // reset the working copy of Game
				Node currentNode = root;// new Node(root, currentConditions.SideToMove, currentMove);

				//TODO START

				int leafExpandMultiplier = 1;

				// Select
				while (!currentNode.IsLeaf())
				{
					currentNode = currentNode.SelectChild();
					currentGame.TryExecuteMove(currentNode.ExecutedMove);
				}

				// Expand
				if (currentNode.IsLeaf()) //always true the time
				{// add all possible moves to the list and pick a random one to actually simulate
					if (!currentGame.LegalMovesTimeline.TryGetCurrent(out Dictionary<Piece, Dictionary<(Square, Square), Movement>> possibleMovesPerPiece))// Game.CalculateLegalMovesForPosition(currentBoard, currentConditions);
					{
						throw new System.Exception("currentGame: could not retrieve possibleMovesPerPiece");
					}

					if(possibleMovesPerPiece != null) // reached a non-terminal node
					{
						List<Movement> movements = Game.UnpackMovementsToList(possibleMovesPerPiece);
						Movement examinedMove = PickRandomMove(movements);

						movements.Remove(examinedMove);
						foreach (Movement uncheckedMove in movements)
						{
							Node uncheckedNode = new Node(currentNode, currentNode.Side.Complement(), uncheckedMove);
							currentNode.AddChild(uncheckedNode);
						}

						// add node for examinedMove and switch to it for simulations
						Node examinedNode = new Node(currentNode, currentNode.Side.Complement(), examinedMove);
						currentNode.AddChild(examinedNode);

						currentNode = examinedNode;
						currentGame.TryExecuteMove(currentNode.ExecutedMove);

						leafExpandMultiplier = -1;
					} // else: reached terminal node: only "simulate" (get instant result) and backpropagate					
				}

				//TODO END

				for (int j = 0; j < playoutsPerLeaf; j++)
				{
					// Simulate
					int result = leafExpandMultiplier * Simulate(currentNode);

					// Backpropagate
					Backpropagate(currentNode, result);

					totalResult += result;
					totalPlayouts++;

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
				Debug.Log($"total result: {totalResult} ({totalPlayouts} playouts, ({debug_wins} wins) ");
#endif
			}

			// end of examinations: pick move with best score

			Node bestNode = root.SelectBestScoredChild();
			bestMove = bestNode.ExecutedMove;


#if DEBUG || DEBUG_MCTS_SIMPLE
			Debug.Log($"best result: {bestNode.Score} ({bestNode.Visits} playouts) [{debug_wins} total wins]");
#endif

			ChangeRootTo(bestNode); // we will execute this legal move, so the timeline should be advanced to this place

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

			if (!currentGame.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions))
			{
				throw new System.Exception("currentGame: could not retrieve currentConditions");
			}

			if (!currentGame.BoardTimeline.TryGetCurrent(out Board currentBoard))
			{
				throw new System.Exception("currentGame: could not retrieve currentBoard");
			}

			int simulationMultiplier = (currentConditions.SideToMove == controlledSide ? 1 : -1); // adjust result for the simulation
			int result = SimulateStep(node.ExecutedMove, maxStepsPerPlayout); // if the making ExecutedMove is a checkmate, the result is 1 so we need to interpret it as a win
			return simulationMultiplier * result;
		}

		// execute performedMove on simulationGame and see what happens
		private int SimulateStep(Movement performedMove, int stepsLeft)
		{
			if (stepsLeft < 1)
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

			if (!currentGame.LegalMovesTimeline.TryGetCurrent(out Dictionary<Piece, Dictionary<(Square, Square), Movement>> possibleMovesPerPiece))
			{
				throw new System.Exception("currentGame: could not retrieve possibleMovesPerPiece");
			}

			// Detect checkmate and stalemate when no legal moves are available
			if (possibleMovesPerPiece == null)
			{
				currentGame.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);
				if (latestHalfMove.CausedCheckmate) // the player who just moved checkmated the "current" player
				{
#if DEBUG_MCTS
					Debug.Log($"AI_MCTS: {currentConditions.SideToMove} checkmated with {stepsLeft} steps left");
#endif
					return -1;
				}
				else //(latestHalfMove.CausedStalemate)
				{
#if DEBUG_MCTS
					Debug.Log($"AI_MCTS: {currentConditions.SideToMove} stalemated with {stepsLeft} steps left");
#endif
					return 0;
				}
			}
			// moves are possible: pick a random one and simulate it
			List<Movement> movements = Game.UnpackMovementsToList(possibleMovesPerPiece);
			Movement chosenMove = PickRandomMove(movements);
			currentGame.TryExecuteMove(chosenMove);
			return -SimulateStep(chosenMove, stepsLeft - 1); // * -1 because the result is returned from the perspective of the player whose move it is after executing chosenMove;
		}

		private Movement PickRandomMove(List<Movement> movements)
		{
			int randomMoveIndex = Random.Range(0, movements.Count - 1);
			Movement chosenMove = movements[randomMoveIndex];
			return chosenMove;
		}

		private void Backpropagate(Node node, int score)
		{
			while (node != null)
			{
				node.Update(score);
				node = node.Parent;
				score = -score; // flip the perspective when moving up the tree (white's win is black's loss)
			}

#if DEBUG_MCTS || DEBUG_MCTS_SIMPLE
			if (score < 0)
			{
				debug_wins++;
			}
			else if (score > 0)
			{
				debug_losses++;
			}
#endif
		}

#region Tree
		private void ChangeRootTo(Node newRoot)
		{
			root = newRoot;
			newRoot.Parent = null; // lose connection to alternate timelines (garbage collector should handle them)
		}

		public class Node
		{
			public int Visits { get; set; }
			public int Score { get; set; }
			public Node Parent { get; set; }
			public List<Node> Children { get; set; }
			public Side Side { get; set; } // player whose move was executed
			//public List<Movement> UncheckedMoves { get => uncheckedMoves; }

			public Movement ExecutedMove; // move that caused this position

			//private List<Movement> uncheckedMoves; // moves that were not explored yet (empty when node is a leaf)

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
				//uncheckedMoves = new List<Movement>();
			}

			//public Node(Node parent, Side side, Movement executedMove, List<Movement> uncheckedMoves)
			//{
			//	Parent = parent;
			//	Visits = 0;
			//	Score = 0;
			//	Children = new List<Node>();
			//	Side = side;
			//	ExecutedMove = executedMove;
			//	this.uncheckedMoves = uncheckedMoves;
			//}

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

			//// When node was selected at least once, we want to add all possible moves for future considerations
			//public void SetUncheckedMoves(List<Movement> uncheckedMoves)
			//{
			//	this.uncheckedMoves = uncheckedMoves;
			//}

			//public bool IsFullyExplored()
			//{
			//	// check if there are any moves left to be explored
			//	return UncheckedMoves.Count < 1;
			//}

			// Calculate the Upper Confidence Bound (UCB) for this node
			public double UCB(int totalVisits)
			{
				if (Visits == 0)
					return double.MaxValue;
				else
					return (double)Score / (double)Visits + System.Math.Sqrt(2 * System.Math.Log(totalVisits) / (double)Visits);
			}

			// Get Score / Visists ratio (not used in UCB to avoid repeating if (Visits == 0) check
			public double ScoreVisitRatio()
			{
				if (Visits == 0)
					return double.MinValue;
				else
					return(double)Score / (double)Visits;
			}

			// Select the child with the highest UCB value
			public Node SelectChild()
			{
				return Children.OrderByDescending(c => c.UCB(Visits)).First();
			}

			public Node SelectBestScoredChild()
			{
				return Children.OrderByDescending(c => c.ScoreVisitRatio()).First();
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
