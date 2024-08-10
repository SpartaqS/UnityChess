using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityChess.Engine;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine.Events;

namespace UnityChess.StrategicAI
{
	/// <summary>
	/// like AI_UCIEngine1 but chooses a random movement instead of the leftmost, furthermost capture. Whether the chosen move is a capture or not is determined by captureMoveChance
	/// </summary>
	public class AI_UCIEngineRandom1 : AI_UCIEngine1
	{
		public override Movement FindBestMove(Game game)
		{
			float captureMoveChance = 0.5f;
			if (!game.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions))
				return null;

			if (!game.BoardTimeline.TryGetCurrent(out Board currentBoard))
				return null;

			Side currentSide = currentConditions.SideToMove;


			Movement bestMove = null;
			game.LegalMovesTimeline.TryGetCurrent(out Dictionary<Piece, Dictionary<(Square, Square), Movement>> possibleMovesPerPiece);
			bool isCapturePossible = false;
			List<MovementWithSide> capturingMoves, noncapturingMoves;
			isCapturePossible = GetCaptureAndNonCaptureMoves(currentBoard, currentSide, possibleMovesPerPiece, isCapturePossible, out capturingMoves, out noncapturingMoves);

			if (isCapturePossible)
			{
				if (Random.Range(0f, 1f) <= captureMoveChance || noncapturingMoves.Count < 1)
				{
					bestMove = PickRandomMovement(capturingMoves);
					return bestMove;
				}

			}
			bestMove = PickRandomMovement(noncapturingMoves);

			return bestMove;
		}

		private static Movement PickRandomMovement(List<MovementWithSide> allMovements)
		{
			Movement bestMove;
			int chosenMoveIndex = Random.Range(0, allMovements.Count - 1);
			bestMove = allMovements[chosenMoveIndex].Movement;
			return bestMove;
		}
	}
}
