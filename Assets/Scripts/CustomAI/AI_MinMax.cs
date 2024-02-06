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

namespace UnityChess.StrategicAI
{
	public class AI_MinMax : IUCIEngine, IUCIEngineWithCustomSettings
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

		// AI settings
		int searchDepth = 4;

		/// <summary>
		/// Apply settings which directly affect the algorithm used
		/// </summary>
		/// <param name="customSettings"></param>
		void IUCIEngineWithCustomSettings.ApplyCustomSettings(UCIEngineCustomSettings customSettings)
		{
			if (!typeof(AIMinMaxSettings).IsAssignableFrom(customSettings.GetType()))
				throw new System.InvalidOperationException("Provided custom settings are not for MinMax Strategic AI");

			AIMinMaxSettings customSettingsToApply = (AIMinMaxSettings)customSettings;

			searchDepth = customSettingsToApply.SearchDepth;
		}

		bool IUCIEngine.CanRequestRestart()
		{
			return false;
		}

		void IUCIEngine.Start()
		{
			// nothing to do at start
			transpositionTable = new TranspositionTable();
		}
		Task IUCIEngine.SetupNewGame(Game game, UnityEvent<Side> gameEndedEvent, UnityAction<Side,int> startNewGameHandler)
		{
			this.game = game;
			// this AI does not care about gameEndedEvent
			// this AI does not request for the game to be restarted
			return Task.CompletedTask;
		}

		/// <summary>
		/// perform min-max with alpha-beta pruning search for the best move and then return it
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
			// run min max search function
			//Task.Factory.StartNew ()
			InitDebugInfo();
			selectedMovementEval = MinMax(searchDepth, 0, negativeInfinity, positiveInfinity);
			selectedMovement = bestMoveThisIteration; // after adding iterative search this will make more sense
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
		int movesEvaluated;
		//int numQNodes;
		int movesCutoff;
		//int numTranspositions;
		System.Diagnostics.Stopwatch searchStopwatch;
		int ttHits;
		string bestBoardAtEndOfBestMove = null;
		string boardAtEndOfCurrentMove = null;
		void InitDebugInfo()
		{
			searchStopwatch = System.Diagnostics.Stopwatch.StartNew();
			movesEvaluated = 0;
			//numQNodes = 0;
			movesCutoff = 0;
			//numTranspositions = 0;
			ttHits = 0;
		}

		void LogDebugInfo()
		{
			Debug.Log($"Selected move: {selectedMovement}\nMove search time: {searchStopwatch.ElapsedMilliseconds}\nmovesEvaluated: {movesEvaluated}\nmovesCutoff: {movesCutoff}\nttHits: {ttHits}\nbestBoardAtEndOfBestMove: {bestBoardAtEndOfBestMove}");
		}


		private int MinMax(int depth, int currentSearchDepth, int alpha, int beta)
		{
			// if depth == 0 quit else do search
			if (abortSearch)
			{
				return 0;
			}

			if (currentSearchDepth > 0)
			{
				//	// Detect draw by repetition.
				//	// Returns a draw score even if this position has only appeared once in the game history (for simplicity).
				//	if (board.RepetitionPositionHistory.Contains(board.ZobristKey))
				//	{
				//		return 0;
				//	}

				// Skip this position if a mating sequence has already been found earlier in
				// the search, which would be shorter than any mate we could find from here.
				// This is done by observing that alpha can't possibly be worse (and likewise
				// beta can't  possibly be better) than being mated in the current position.
				//alpha = Max(alpha, -immediateMateScore + currentSearchDepth);
				//beta = Min(beta, immediateMateScore - currentSearchDepth);
				if (alpha >= beta)
				{
					return alpha;
				}
			}

			// Try looking up the current position in the transposition table.

			if (!currentGame.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions))
			{
				throw new System.Exception("currentGame: could not retrieve currentConditions");
			}

			if (!currentGame.BoardTimeline.TryGetCurrent(out Board currentBoard))
			{
				throw new System.Exception("currentGame: could not retrieve currentBoard");
			}

			string currentBoardHash = FENSerializer.GetBoardString(currentBoard);
			boardAtEndOfCurrentMove = FENSerializer.GetBoardString(currentBoard); // DIAGNOSTICS
			// If the same position has already been searched to at least an equal depth
			// to the search we're doing now,we can just use the recorded evaluation.
			bool positionStoredInTranspositionTable = transpositionTable.LookupEvaluation(currentBoardHash, out int storedEvaluation, currentSearchDepth, alpha, beta);
			if (positionStoredInTranspositionTable)
			{
				ttHits++;
				if (currentSearchDepth == 0)
				{
					bestMoveThisIteration = transpositionTable.GetStoredMove(currentBoardHash);
					evalBestMoveThisIteration = storedEvaluation;
					transpositionTable.GetNodeTypeAndDepth(currentBoardHash, out TranspositionTable.EvaluationType storedEvaluationType, out int storedDepth);
					Debug.Log ("move retrieved " + bestMoveThisIteration.ToString() + " evaluation type: " + storedEvaluationType + " evaluation depth: " + storedDepth);
					bestBoardAtEndOfBestMove = boardAtEndOfCurrentMove; //DIAGNOSTICS
				}
				return storedEvaluation;
			}

			if (depth == 0)				
			{
				int evaluation = EvaluateBoard(currentBoard, currentConditions.SideToMove);
				// optional if we want to have no search depth (delete?)
				//int evaluation = QuiescenceSearch(alpha, beta);
				return evaluation;
			}

			Dictionary<Piece, Dictionary<(Square, Square), Movement>> possibleMovesPerPiece = Game.CalculateLegalMovesForPosition(currentBoard, currentConditions);
			// Detect checkmate and stalemate when no legal moves are available
			if (possibleMovesPerPiece == null)
			{
				currentGame.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove); //TODO //?? maybye put these in the transposition table so instant checkmates are detected before searching the tt?
				if (latestHalfMove.CausedCheckmate)
				{
					int mateScore = immediateMateScore - currentSearchDepth;
					return -mateScore;
				}
				else //(latestHalfMove.CausedStalemate)
				{
					return 0;
				}
			}

			// unpack moves into a list to iterate over them in the search
			List<Movement> movements = UnpackMovementsToList(possibleMovesPerPiece);

			//moveOrdering.OrderMoves(board, moves, settings.useTranspositionTable);

			TranspositionTable.EvaluationType evalType = TranspositionTable.EvaluationType.UpperBound;
			Movement bestMoveInThisPosition = null;

			for (int i = 0; i < movements.Count; i++)
			{
				Movement currentMovement = movements[i];
				currentGame.TryExecuteMove(currentMovement);
				int eval = -MinMax(depth - 1, currentSearchDepth + 1, -beta, -alpha);
				currentGame.ResetGameToHalfMoveIndex((System.Math.Max(-1, currentGame.HalfMoveTimeline.HeadIndex - 1)));
				movesEvaluated++;

				// Move was *too* good, so opponent won't allow this position to be reached
				// (by choosing a different move earlier on). Skip remaining moves.
				if (eval >= beta)
				{
					evalType = TranspositionTable.EvaluationType.LowerBound;
					transpositionTable.StoreEvaluation(currentBoardHash, beta, currentSearchDepth, evalType, currentMovement);
					movesCutoff++;
					return beta;
				}

				// Found a new best move in this position
				if (eval > alpha)
				{
					evalType = TranspositionTable.EvaluationType.Exact;
					bestMoveInThisPosition = currentMovement;

					alpha = eval;
					if (currentSearchDepth == 0)
					{
						bestMoveThisIteration = currentMovement;
						evalBestMoveThisIteration = eval;
						bestBoardAtEndOfBestMove = boardAtEndOfCurrentMove; //DIAGNOSTICS
					}
				}
			}

			transpositionTable.StoreEvaluation(currentBoardHash, alpha, currentSearchDepth, evalType, bestMoveInThisPosition);

			return alpha;

		}

		// evaluation constants
		const int immediateMateScore = 100000;
		const int positiveInfinity = 9999999;
		const int negativeInfinity = -positiveInfinity;
		// https://www.chessstrategyonline.com/content/tutorials/basic-chess-concepts-material
		const int pawnValue = 100;
		const int knightValue = 300;
		const int bishopValue = 350;
		const int rookValue = 500;
		const int queenValue = 900;

		protected class SideEvaluation {
			public SideEvaluation(Side evaluatedSide)
			{
				this.evaluatedSide = evaluatedSide;
			}
			public readonly Side evaluatedSide;

			public int materialScore = 0;
			public int piecePositionScore = 0;

			public int Total()
			{
				return materialScore + piecePositionScore;
			}
		}

		protected class PieceWithPosition {
			public Piece piece;
			public Square square;

			public PieceWithPosition(Piece piece, Square square)
			{
				this.piece = piece;
				this.square = square;
			}
		}



		protected int EvaluateBoard(Board board, Side sideToMove)
		{
			string debugBoardFEN = FENSerializer.GetBoardString(board); //debug

			//TODO:
			SideEvaluation whiteEvaluation = new SideEvaluation(Side.White);
			SideEvaluation blackEvaluation = new SideEvaluation(Side.Black);

			//1. sum pieces material score
			whiteEvaluation.materialScore = GetMaterialScore(board, Side.White);
			blackEvaluation.materialScore = GetMaterialScore(board, Side.Black);

			//2. give bonuses based on piece positions
			//2a. TODO generate lists of pieces for faster iteration

			whiteEvaluation = GetPositionScore(board, whiteEvaluation);
			blackEvaluation = GetPositionScore(board, blackEvaluation);


			// compute final evaluation
			int evaluation = whiteEvaluation.Total() - blackEvaluation.Total();
			// adjust perspective of side to move (assuming there is always a side to move when evaluating the board)
			int sideToMoveModifier = (sideToMove == Side.Black) ? -1 : 1;

			return evaluation * sideToMoveModifier;
		}

		protected int GetMaterialScore(Board board, Side sideToScore)
		{
			int materialScore = 0;

			for (int rank = 1; rank <= 8; rank++)
			for (int file = 1; file <= 8; file++)
				{
					Piece piece = board[file, rank];
					if (piece == null)
						continue;

					if (piece.Owner != sideToScore)
						continue;

					int pieceValue = 0;
					switch (piece)
					{
						case Pawn:
							pieceValue = pawnValue;
							break;
						case Knight:
							pieceValue = knightValue;
							break;
						case Bishop:
							pieceValue = bishopValue;
							break;
						case Rook:
							pieceValue = rookValue;
							break;
						case Queen:
							pieceValue = queenValue;
							break;
					}
					
					materialScore += pieceValue;
				}

			return materialScore;
		}

		protected SideEvaluation GetPositionScore(Board board, SideEvaluation sideEvaluation)
		{
			for (int rank = 1; rank <= 8; rank++)
				for (int file = 1; file <= 8; file++)
				{
					Piece piece = board[file, rank];
					if (piece == null)
						continue;

					if (piece.Owner != sideEvaluation.evaluatedSide)
						continue;

					Square pieceSquare = new Square(file, rank);
					bool readAsBlack = sideEvaluation.evaluatedSide == Side.Black;
					
					switch (piece)
					{
						case Pawn:
							break;
						case Knight:
							break;
						case Bishop:
							break;
						case Rook:
							sideEvaluation.piecePositionScore += PiecePositionScoreTable.Read(PiecePositionScoreTable.Rooks, pieceSquare, readAsBlack);
							break;
						case Queen:
							break;
						case King:
							sideEvaluation.piecePositionScore += PiecePositionScoreTable.Read(PiecePositionScoreTable.KingLate, pieceSquare, readAsBlack);
							break;
					}
				}

			return sideEvaluation;
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
	}
}
