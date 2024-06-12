#define DEBUG
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
	/// <summary>
	/// This MlAgent outputs the entire board as an output: so it should learn what a baord can be transformed into
	/// </summary>
	public class AI_MLAgent2 : AI_MLAgent1
	{
		protected override void RequestRestartTrainingFailure()
		{
			AddReward(-100f);
			requestStartNewGame.Invoke(controlledSide, 2);
		}

		public override void DecodeMove(ActionBuffers actions, out Movement decodedMove, out bool actionIsValidMove)
		{
			Board decodedBoard = new Board();
			int actionsIndex = 0;

			// decode the board
			for (int row = 1; row <= 8; row++)
			{
				for (int col = 1; col <= 8; col++)
				{
					int actionSquareInt = actions.DiscreteActions[actionsIndex];

					PieceEnum decodedPieceEnum = (PieceEnum)(actionSquareInt - 6);
					Piece decodedPiece = GetPieceFromEnum(decodedPieceEnum);

					decodedBoard[new Square(col, row)] = decodedPiece;
					actionsIndex++;
				}
			}

			// check if decodedBoard can be obtained by a legal move
			if (!game.LegalMovesTimeline.TryGetCurrent(out Dictionary<Piece, Dictionary<(Square, Square), Movement>> possibleMovesPerPiece))
			{
				throw new System.Exception("game: could not retrieve possibleMovesPerPiece");
			}

			Game gameCopy = new Game(game);

			List<Movement> possibleMovesList = Game.UnpackMovementsToList(possibleMovesPerPiece);

			foreach (Movement legalMove in possibleMovesList)
			{
				gameCopy.TryExecuteMove(legalMove);
				if (!gameCopy.BoardTimeline.TryGetCurrent(out Board boardAfterLegalMove))
				{
					throw new System.Exception("gameCopy: could not retrieve boardAfterLegalMove");
				}

				if (decodedBoard == boardAfterLegalMove)
				{ // MLAgent has generated a board after a legal move: retireve the move
					decodedMove = legalMove;
					actionIsValidMove = true;
					Debug.Log($"Correct chosen move: {decodedMove.Start.ToString()} -> {decodedMove.End.ToString()} {(decodedMove is PromotionMove promotionMove ? $"promotes to: {promotionMove.PromotionPiece.ToString()}" : "")}");
					AddReward(100f);
					return;
				}
				gameCopy.ResetGameToHalfMoveIndex((System.Math.Max(-1, gameCopy.HalfMoveTimeline.HeadIndex - 1)));
			}
			// no legal move leads to what the MLAgent has generated	
			actionIsValidMove = false;
			decodedMove = null;
			//Debug.Log($"Invalid decodedBoard");
			//Debug.Log($"Invalid decodedBoard: {FENSerializer.GetBoardString(decodedBoard)}");
			//Debug.Log($"Invalid decodedBoard:\n{decodedBoard.ToTextArt()}");
			// add negative reward for each square that is different from the starting board (so the AI learns to produce similar but not exactly the same boards)

			if (!game.BoardTimeline.TryGetCurrent(out Board currentBoard))
			{
				throw new System.Exception("game: could not retrieve currentBoard");
			}

			int invalidBoardPenalty = 0;

			for (int row = 1; row <= 8; row++)
			{
				for (int col = 1; col <= 8; col++)
				{
					Piece decodedPiece = decodedBoard[col, row];
					Piece currentPiece = currentBoard[col, row];

					if (decodedPiece == null && currentPiece == null) // both squares are empty
					{
						continue;
					}

					if (decodedPiece != null && currentPiece != null) // both squares are non-empty
					{
						if (decodedPiece.Owner != currentPiece.Owner) // pieces are of different color
						{
							invalidBoardPenalty += 1;
							AddReward(-1f);
							continue;
						}

						if (decodedPiece.GetType().Name != currentPiece.GetType().Name) // pieces are of same color but different types
						{
							invalidBoardPenalty += 1;
							AddReward(-1f);
							continue;
						}

						// both pieces are of the same color and type: they are the same
						continue;
					}
					else // one piece is null and other is not null
					{
						invalidBoardPenalty += 1;
						AddReward(-1f);
					}
				}
			}
			
			if(invalidBoardPenalty < 5 && invalidBoardPenalty > 1) // highest possible diff in a legal change occurs when castling, diff == 0 -> no move, diff == 1 -> single piece swap
			{
				if(!keepTrainingAfterInvalidMove) //?? reduce the penalty to diffs between 2 and 4
					AddReward(90f);
#if DEBUG
				Debug.Log($"Invalid decodedBoard (diff: {invalidBoardPenalty})\nInvalid decodedBoard:\n{decodedBoard.ToTextArt()}\nInvalid currentBoard:\n{currentBoard.ToTextArt()}");
#endif
			} 
			else
			{
#if DEBUG
				Debug.Log($"Invalid decodedBoard (diff: {invalidBoardPenalty})");
#endif
			}
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

			// obtain moveBoard from the chosen move
			Game gameCopy = new Game(game);
			gameCopy.TryExecuteMove(move);
			if (!gameCopy.BoardTimeline.TryGetCurrent(out Board moveBoard))
			{
				throw new System.Exception("gameCopy: could not retrieve boardAfterLegalMove");
			}

			int actionsIndex = 0;
			// encode the board
			for (int row = 1; row <= 8; row++)
			{
				for (int col = 1; col <= 8; col++)
				{
					GetPieceEnum(moveBoard[new Square(col, row)], out PieceEnum encodedPieceEnum);
					int actionSquareInt = (int)encodedPieceEnum + 6;
					discreteActions[actionsIndex] = actionSquareInt;
					actionsIndex++;
				}
			}

		}
		/// <summary>
		/// almost the same as in MlAgent1, only difference is that the board is transladed into ints from 0 to 12 to match the action's possible values
		/// </summary>
		/// <param name="sensor"></param>
		public override void CollectObservations(VectorSensor sensor)
		{
			if (!game.BoardTimeline.TryGetCurrent(out Board currentBoard))
			{
				throw new System.NullReferenceException("currentBoard is null");
			}
			if (!game.ConditionsTimeline.TryGetCurrent(out GameConditions currentGameConditions))
			{
				throw new System.NullReferenceException("currentGameConditions is null");
			}

			// translate board matrix to a sequence of ints
			//string observationsStr = "\n";
			for (int row = 1; row <= 8; row++)
			{
				for (int col = 1; col <= 8; col++)
				{
					Piece piece = currentBoard[new Square(col, row)];

					GetPieceEnum(piece, out PieceEnum encodedPieceEnum);

					int actionSquareInt = (int)encodedPieceEnum + 6;
					sensor.AddObservation(actionSquareInt);
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
	}
}
