using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityChess.Engine;
using System.Threading.Tasks;
using Unity.MLAgents;

namespace UnityChess.StrategicAI
{
	public class AI_MLAgent1 : Agent,  IUCIEngine
	{//TODO actually implement interfacing between Model and game
		protected Game game;
		
		void IUCIEngine.Start()
		{
			// nothing to do at start
		}
		Task IUCIEngine.SetupNewGame(Game game)
		{
			this.game = game;
			return Task.CompletedTask;
		}
		/// <summary>
		/// dumb but simple "strategy": move the leftmost piece, if can capture: must capture
		/// </summary>
		/// <param name="timeoutMS"></param>
		/// <returns></returns>
		async Task<Movement> IUCIEngine.GetBestMove(int timeoutMS)
		{
			if (!game.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions))
				return null;

			if (!game.BoardTimeline.TryGetCurrent(out Board currentBoard))
				return null;

			Side currentSide = currentConditions.SideToMove;


			Movement bestMove = null;
			Dictionary<Piece, Dictionary<(Square, Square), Movement>> possibleMovesPerPiece = Game.CalculateLegalMovesForPosition(currentBoard, currentConditions);
			bool isCapturePossible = false;
			List<MovementWithSide> capturingMoves = new List<MovementWithSide>();
			List<MovementWithSide> noncapturingMoves = new List<MovementWithSide>();
			// capturing is most important: collect capturing moves
			foreach (Piece piece in possibleMovesPerPiece.Keys)
			{
				foreach (Movement move in possibleMovesPerPiece[piece].Values)
				{
					if (piece is Pawn)
					{
						if (move is EnPassantMove)
						{
							isCapturePossible = true;
							capturingMoves.Add(new MovementWithSide(currentSide, move));
							continue; // en passant cannot end in promotion, but is a capturing move
						}
						if (move is PromotionMove)
						{// pawn promotes: pick a promotion for it
							(move as PromotionMove).SetPromotionPiece(new Queen(currentSide));
						}

					}

					// piece captures
					if (IsCapturingMove(currentBoard, move.End))
					{
						isCapturePossible = true;
						capturingMoves.Add(new MovementWithSide(currentSide, move));
						continue;
					}
					// piece cannot capture
					noncapturingMoves.Add(new MovementWithSide(currentSide, move));
				}
			}

			if (isCapturePossible)
			{
				capturingMoves.Sort(new MovementWithSideComparer());
				bestMove = capturingMoves[0].Movement;
			}
			else
			{
				noncapturingMoves.Sort(new MovementWithSideComparer());
				bestMove = noncapturingMoves[0].Movement;
			}

			await Task.Delay(1000);
			return bestMove;
		}

		void IUCIEngine.ShutDown()
		{
			// nothing to do at shutdown
		}

		protected bool IsCapturingMove(Board board, Square endSquare)
		{
			return board[endSquare] is Piece;
		}

		/// <summary>
		/// Assumes that MovementWithSide are of the same side
		/// </summary>
		public class MovementWithSideComparer : IComparer<MovementWithSide>
		{
			// -1: y is earlier than x
			// 1 : x is earlier than y
			public int Compare(MovementWithSide x, MovementWithSide y)
			{
				Side side = x.Side;
				if (x.Movement.Start.Rank < y.Movement.Start.Rank)
				{
					return side == Side.White ? 1 : -1; // white wants to move the frontmost units
				}
				else if (x.Movement.Start.Rank > y.Movement.Start.Rank)
				{
					return side == Side.White ? -1 : 1; // white wants to move the frontmost units
				}
				else
				{   // check the start column (we want to move leftmost pieces first
					if (x.Movement.Start.File < y.Movement.Start.File)
					{
						return 1;
					}
					else if (x.Movement.Start.File > y.Movement.Start.File)
					{
						return -1;
					}
					else
					{
						// choose move which travels more tiles
						if(x.Distance < y.Distance)
						{
							return 1;
						}
						if (x.Distance > y.Distance)
						{
							return -1;
						}
						else
						{
							return 0;
						}
						}
					}
			}
		}

		public class MovementWithSide
		{
			public readonly Side Side;
			public readonly int Distance;
			public readonly Movement Movement;
			public MovementWithSide(Side side, Movement movement)
			{
				Side = side;
				Distance = Mathf.Abs(movement.End.Rank - movement.Start.Rank) + Mathf.Abs(movement.End.File - movement.Start.File);
				Movement = movement;
			}
		}

	}
}
