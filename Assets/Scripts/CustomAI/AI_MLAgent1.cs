using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityChess.Engine;
using System.Threading.Tasks;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.Events;

namespace UnityChess.StrategicAI
{
	public class AI_MLAgent1 : Agent,  IUCIEngine
	{//TODO actually implement interfacing between Model and game
		bool keepTrainingAfterInvalidMove = true;
		//bool keepTrainingAfterInvalidMove = false;
		protected Game game;
		protected Movement selectedMovement = null;
		Side controlledSide = Side.None;
		UnityEvent<Side,int> requestStartNewGame = new UnityEvent<Side,int>();
		private void RequestRestartEndGame() { requestStartNewGame.Invoke(controlledSide, 1); }
		private void RequestRestartTrainingFailure() 
		{
			AddReward(-1000f);
			requestStartNewGame.Invoke(controlledSide, 2); 
		}
		public override void OnEpisodeBegin()
		{
			base.OnEpisodeBegin();
			//requestStartNewGame.Invoke(controlledSide);
		}
		bool IUCIEngine.CanRequestRestart()
		{
			return true;
		}

		void IUCIEngine.Start()
		{
			// nothing to do at start
		}
		Task IUCIEngine.SetupNewGame(Game game, UnityEvent<Side> gameEndedEvent, UnityAction<Side,int> startNewGameHandler)
		{
			this.game = game;
			gameEndedEvent.AddListener(HandleGameEndEvent);
			requestStartNewGame.AddListener(startNewGameHandler);
			return Task.CompletedTask;
		}

		private void HandleGameEndEvent(Side winningSide)
		{
			if(winningSide == controlledSide)
			{
				AddReward(10000f);
				RequestRestartEndGame();
			}
			else if (winningSide == Side.None)
			{
				AddReward(0f);
				RequestRestartEndGame();
			}
			else // the other player has won
			{
				AddReward(-10000f);
				RequestRestartEndGame();
			}
		}

		/// <summary>
		/// ask the Agent's model for a decision and then get it
		/// </summary>
		/// <param name="timeoutMS"></param>
		/// <returns></returns>
		async Task<Movement> IUCIEngine.GetBestMove(int timeoutMS)
		{
			if (!game.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions))
				return null;

			if (!game.BoardTimeline.TryGetCurrent(out Board currentBoard))
				return null;

			controlledSide = currentConditions.SideToMove;
			selectedMovement = null;
			RequestDecision();
			//TODO implement timeout
			await Task.Run(async () => { while (selectedMovement == null) await Task.Delay(10); });
			Movement bestMove = selectedMovement;
			return bestMove;
		}

		void IUCIEngine.ShutDown(UnityEvent<Side> gameEndedEvent)
		{
			gameEndedEvent.RemoveListener(HandleGameEndEvent);
			requestStartNewGame.RemoveAllListeners();
			EndEpisode();
			// nothing to do at shutdown
		}

		public void DecodeMove(ActionBuffers actions, out Movement decodedMove, out bool actionIsValidMove)
		{

			int startCol = actions.DiscreteActions[0];
			int startRow = actions.DiscreteActions[1];
			int endCol = actions.DiscreteActions[2];
			int endRow = actions.DiscreteActions[3];
			int promotionPieceInt = actions.DiscreteActions[4];

			// get the picked Squares (MLAgent returnsvalues from 0 to 7, change them into form 1 to 8)
			Square startSquare = new Square(startCol + 1, startRow + 1);
			Square endSquare = new Square(endCol + 1, endRow + 1);
			Piece promotionPiece = null;


			if (promotionPieceInt != 0)
			{
				switch ((PieceEnum)(promotionPieceInt + 1))
				{
					case PieceEnum.WhiteRook:
						promotionPiece = new Rook(controlledSide);
						break;
					case PieceEnum.WhiteKnight:
						promotionPiece = new Knight(controlledSide);
						break;
					case PieceEnum.WhiteBishop:
						promotionPiece = new Bishop(controlledSide);
						break;
					case PieceEnum.WhiteQueen:
						promotionPiece = new Queen(controlledSide);
						break;
				}
			}

			if (promotionPiece != null)
			{
				decodedMove = new PromotionMove(startSquare, endSquare);
				(decodedMove as PromotionMove).SetPromotionPiece(promotionPiece);
			}
			else // non-promotion moves are checked by cheking start and end squares only, not their special types so we do not need to decide what move it is at this stage
			{
				decodedMove = new Movement(startSquare, endSquare);
			}
			// validate the move (for training purposes)
			actionIsValidMove = true;

			if (!game.TryGetLegalMove(startSquare, endSquare, out Movement move))
			{// invalid move: try a different one / end the episode and add penalty (if first approach does not work)
				Debug.Log($"Invalid chosen move: {startSquare.ToString()} -> {endSquare.ToString()} {(promotionPiece != null ? $"promotes to: {promotionPiece.ToString()}" : "")}");
				actionIsValidMove = false;
			}

			bool isLegalMoveAPromotionMove = move is PromotionMove;
			bool isChosenMoveAPromotionMove = decodedMove is PromotionMove;
			if (isLegalMoveAPromotionMove != isChosenMoveAPromotionMove)
			{
				Debug.Log($"Invalid chosen promotion move: {startSquare.ToString()} -> {endSquare.ToString()} {(promotionPiece != null ? $"promotes to: {promotionPiece.ToString()}" : "")} ({(isChosenMoveAPromotionMove ? "attempted illegal promotion" : "has not chosen a piece for promotion")})");
				actionIsValidMove = false;
			}
		}

		public override void OnActionReceived(ActionBuffers actions)
		{
			base.OnActionReceived(actions);

			DecodeMove(actions, out Movement chosenMove, out bool actionIsValidMove);

			if (actionIsValidMove)
			{
				Debug.Log($"Correct chosen move: {chosenMove.Start.ToString()} -> {chosenMove.End.ToString()} {(chosenMove is PromotionMove promotionMove ? $"promotes to: {promotionMove.PromotionPiece.ToString()}" : "")}");
				AddReward(1f);
			}
			else if(keepTrainingAfterInvalidMove)
			{
				//AddReward(-1000f); //??
				AI_UCIEngineRandom1 aii_UCIEngine1 = new AI_UCIEngineRandom1();
				Movement randomMove = aii_UCIEngine1.FindBestMove(game);
				chosenMove = randomMove;
				Debug.Log($"Heuristic chosen move: {chosenMove.Start.ToString()} -> {chosenMove.End.ToString()} {(chosenMove is PromotionMove promotionMove2 ? $"promotes to: {promotionMove2.PromotionPiece.ToString()}" : "")}");
			}
			else // move invalid and we want to end episode on failure
			{
				RequestRestartTrainingFailure();
				return;
			}

			selectedMovement = chosenMove;
		}

		public override void CollectObservations(VectorSensor sensor)
		{
			base.CollectObservations(sensor);
			if (!game.BoardTimeline.TryGetCurrent(out Board currentBoard))
			{
				throw new System.NullReferenceException("currentBoard is null");
			}
			if (!game.ConditionsTimeline.TryGetCurrent(out GameConditions currentGameConditions))
			{
				throw new System.NullReferenceException("currentBoard is null");
			}

			// translate board matrix to a sequence of ints
			//string observationsStr = "\n";
			for (int row = 1; row <= 8; row++)
			{	
				for (int col = 1; col <= 8; col++)
				{
					Piece piece = currentBoard[new Square(col, row)];
					if (piece == null)
					{
						sensor.AddObservation(0);
						//observationsStr += "0 ";
						continue;
					}

					GetPieceEnum(piece, out PieceEnum finalPieceEnum);
					sensor.AddObservation((int)finalPieceEnum);
					//observationsStr += $"{(int)finalPieceEnum} ";
				}
				//observationsStr += "\n";
			}
			// add GameConditions to observation space
			sensor.AddObservation((int)currentGameConditions.SideToMove);
			sensor.AddObservation(currentGameConditions.WhiteCanCastleKingside);
			sensor.AddObservation(currentGameConditions.WhiteCanCastleQueenside);
			sensor.AddObservation(currentGameConditions.BlackCanCastleKingside);
			sensor.AddObservation(currentGameConditions.BlackCanCastleQueenside);
			sensor.AddObservation(currentGameConditions.EnPassantSquare.File);
			sensor.AddObservation(currentGameConditions.EnPassantSquare.Rank);

			//Debug.Log("Observations collected:" + observationsStr);
		}

		private void GetPieceEnum(Piece piece, out PieceEnum finalPieceEnum)
		{
			// if Piece is black, then its enum value is negative
			int isBlackMultiplier = piece.Owner == Side.Black ? -1 : 1;
			PieceEnum pieceEnum = PieceEnum.Empty;
			switch (piece)
			{
				case Pawn:
					pieceEnum = PieceEnum.WhitePawn;
					break;
				case Rook:
					pieceEnum = PieceEnum.WhiteRook;
					break;
				case Knight:
					pieceEnum = PieceEnum.WhiteKnight;
					break;
				case Bishop:
					pieceEnum = PieceEnum.WhiteBishop;
					break;
				case Queen:
					pieceEnum = PieceEnum.WhiteQueen;
					break;
				case King:
					pieceEnum = PieceEnum.WhiteKing;
					break;
				default:
					pieceEnum = PieceEnum.Empty;
					break;
			}

			finalPieceEnum = (PieceEnum)((int)pieceEnum * isBlackMultiplier);
		}

		// enums for clearer debugging
		protected enum PieceEnum
		{
			BlackKing = -6,
			BlackQueen = -5,
			BlackBishop = -4,
			BlackKnight = -3,
			BlackRook = -2,
			BlackPawn = -1,
			Empty = 0,
			WhitePawn = 1,
			WhiteRook = 2,
			WhiteKnight = 3,
			WhiteBishop = 4,
			WhiteQueen = 5,
			WhiteKing = 6
		}

		/// <summary>
		/// The Heuristic is the 'dumb' model for now
		/// </summary>
		/// <param name="actionsOut"></param>
		public override void Heuristic(in ActionBuffers actionsOut)
		{
			Debug.LogWarning($"{controlledSide} is using heuristics:");
			base.Heuristic(actionsOut);
			ActionSegment<int> discreteActions = actionsOut.DiscreteActions;

			AI_UCIEngineRandom1 aii_UCIEngine1 = new AI_UCIEngineRandom1();
			Movement move = aii_UCIEngine1.FindBestMove(game);
			// Offset by -1 because MLAgent gives coordinates from 0 to 7 (not from 1 to 8)
			discreteActions[0] = move.Start.File - 1;
			discreteActions[1] = move.Start.Rank - 1;
			discreteActions[2] = move.End.File - 1;
			discreteActions[3] = move.End.Rank - 1;
			discreteActions[4] = 0;
			if (move is PromotionMove)
			{
				Piece promotionPiece = (move as PromotionMove).PromotionPiece;
				GetPieceEnum(promotionPiece, out PieceEnum pieceEnum);
				int sideMultiplier = controlledSide == Side.Black ? -1 : 1; // regardless of side the Neural Network is supposed to give piece enums as if it was playing White
				discreteActions[4] = ((int)pieceEnum * sideMultiplier) - 1; // we cannot promote to Pawn, "obtainable" promotion pieces are from 2 to 5 (inclusive)
			}
		}
	}
}
