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
	/// like AI_UCIEngine1 but chooses a random movement instead of the leftmost, firthermost capture, with capturing having higher weight
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
			Dictionary<Piece, Dictionary<(Square, Square), Movement>> possibleMovesPerPiece = Game.CalculateLegalMovesForPosition(currentBoard, currentConditions);
			bool isCapturePossible = false;
			List<MovementWithSide> capturingMoves, noncapturingMoves;
			isCapturePossible = GetCaptureAndNonCaptureMoves(currentBoard, currentSide, possibleMovesPerPiece, isCapturePossible, out capturingMoves, out noncapturingMoves);

			List<MovementWithSide> allMovements = new List<MovementWithSide>();

			if (isCapturePossible)
			{
				if (Random.Range(0f, 1f) <= captureMoveChance)
				{
					capturingMoves.Sort(new MovementWithSideComparer());
					allMovements.AddRange(capturingMoves);
					bestMove = PickRandomMovement(allMovements);
					return bestMove;
				}

			}
			noncapturingMoves.Sort(new MovementWithSideComparer());
			allMovements.AddRange(noncapturingMoves);
			bestMove = PickRandomMovement(allMovements);
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
