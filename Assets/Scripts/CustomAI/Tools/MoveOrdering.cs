using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityChess.StrategicAI.Tools
{
	public class MoveOrdering
	{
		public static int maxMoveCount = 218;
		int[] moveScores = new int[maxMoveCount]; // very likely the max number of movements a position can have:
										 // //https://www.chess.com/forum/view/general/max-number-of-movements//

		const int million = 1000000;
		const int hashMoveScore = 100 * million;
		const int winningCaptureBias = 8 * million;
		const int promoteBias = 6 * million;
		const int killerBias = 4 * million;
		const int losingCaptureBias = 2 * million;
		const int regularBias = 0;
		const int invalidMoveScore = -100 * million;
		public System.Span<Movement> OrderMoves(Board board, System.Span<Movement> movements)
		{
			for (int i = 0; i < movements.Length; i++)
			{

				Movement move = movements[i];

				//if (move.Equals(hashMove))
				//{
				//	moveScores[i] = hashMoveScore;
				//	continue;
				//}

				if (!move.IsValid())
				{
					moveScores[i] = invalidMoveScore;
					continue;
				}

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

					score += winningCaptureBias + captureMaterialDelta;
					//bool opponentCanRecapture = BitBoardUtility.ContainsSquare(oppPawnAttacks | oppAttacks, targetSquare);
					//if (opponentCanRecapture)
					//{
					//	score += (captureMaterialDelta >= 0 ? winningCaptureBias : losingCaptureBias) + captureMaterialDelta;
					//}
					//else
					//{
					//	score += winningCaptureBias + captureMaterialDelta;
					//}
				}

				if (movePiece is Pawn)
				{
					if (move.IsPromotionMove && !isCapture)
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

					//if (BitBoardUtility.ContainsSquare(oppPawnAttacks, targetSquare))
					//{
					//	score -= 50;
					//}
					//else if (BitBoardUtility.ContainsSquare(oppAttacks, targetSquare))
					//{
					//	score -= 25;
					//}

				}

				if (!isCapture)
				{
					score += regularBias;
					//bool isKiller = !inQSearch && ply < maxKillerMovePly && killerMoves[ply].Match(move);
					//score += isKiller ? killerBias : regularBias;
					//score += History[board.MoveColourIndex, move.StartSquare, move.TargetSquare];
				}

				moveScores[i] = score;
			}

			Qsort(movements, moveScores, 0, movements.Length - 1);

			return movements;
		}

		public void Qsort(System.Span<Movement> values, int[] scores, int low, int high)
		{
			if (low < high)
			{
				int pivotIndex = Partition(values, scores, low, high);
				Qsort(values, scores, low, pivotIndex - 1);
				Qsort(values, scores, pivotIndex + 1, high);
			}
		}

		static int Partition(System.Span<Movement> values, int[] scores, int low, int high)
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