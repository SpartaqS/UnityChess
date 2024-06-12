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
					return;
				}
			}
			// no legal move leads to what the MLAgent has generated	
			actionIsValidMove = false;
			decodedMove = null;
			Debug.Log($"Invalid decodedBoard: {FENSerializer.GetBoardString(decodedBoard)}");
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
	}
}
