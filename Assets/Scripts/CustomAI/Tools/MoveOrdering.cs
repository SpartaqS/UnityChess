using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityChess.StrategicAI.Tools
{
	public class MoveOrdering
	{
		int[] moveScores = new int[218]; // very likely the max number of movements a position can have:
										 // //https://www.chess.com/forum/view/general/max-number-of-movements//

		const int million = 1000000;
		const int captureBias = 8 * million;
		const int promoteBias = 6 * million;
		const int regularBias = 0;
		public List<Movement> OrderMoves(Board board, List<Movement> movements)
		{
			for (int i = 0; i < movements.Count; i++)
			{

				Movement move = movements[i];

				int score = 0;
				Square startSquare = move.Start;
				Square targetSquare = move.End;

				Piece movePiece = board[startSquare];
				Piece capturePiece = board[targetSquare];
				bool isCapture = capturePiece != null;
				int pieceValue = AI_MinMax.GetPieceMaterialScore(movePiece);

				if (isCapture)
				{
					// Order movements to try capturing the most valuable opponent piece with least valuable of own pieces first
					int captureMaterialDelta = AI_MinMax.GetPieceMaterialScore(capturePiece) - pieceValue;

					score += captureBias + captureMaterialDelta;
				}

				if (movePiece is Pawn)
				{
					if (move is PromotionMove && !isCapture)
					{
						score += promoteBias;
					}
				}
				else if (movePiece is King)
				{
				}
				else
				{
					int toScore = PiecePositionScoreTable.Read(movePiece, targetSquare);
					int fromScore = PiecePositionScoreTable.Read(movePiece, startSquare);
					score += toScore - fromScore;

				}

				if (!isCapture)
				{
					score += regularBias;
				}

				moveScores[i] = score;
			}

			Qsort(movements, moveScores, 0, movements.Count - 1);

			return movements;
		}

		public void Qsort(List<Movement> values, int[] scores, int low, int high)
		{
			if (low < high)
			{
				int pivotIndex = Partition(values, scores, low, high);
				Qsort(values, scores, low, pivotIndex - 1);
				Qsort(values, scores, pivotIndex + 1, high);
			}
		}

		static int Partition(List<Movement> values, int[] scores, int low, int high)
		{
			int pivotScore = scores[high];
			int i = low - 1;

			for (int j = low; j <= high - 1; j++)
			{
				if (scores[j] > pivotScore)
				{
					i++;
					(values[i], values[j]) = (values[j], values[i]);
					(scores[i], scores[j]) = (scores[j], scores[i]);
				}
			}
			(values[i + 1], values[high]) = (values[high], values[i + 1]);
			(scores[i + 1], scores[high]) = (scores[high], scores[i + 1]);

			return i + 1;
		}
	}
}