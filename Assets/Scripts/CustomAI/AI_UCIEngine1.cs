using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityChess.Engine;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine.Events;

namespace UnityChess.StrategicAI
{
	public class AI_UCIEngine1 : IUCIEngine
	{
		protected Game game;
		bool IUCIEngine.CanRequestRestart()
		{
			return false;
		}
		void IUCIEngine.Start()
		{
			// nothing to do at start
		}
		Task IUCIEngine.SetupNewGame(Game game, UnityEvent<Side> gameEndedEvent, UnityAction<Side,int> startNewGameHandler)
		{
			this.game = game;
			// this AI does not care about gameEndedEvent
			// this AI does not request for the game to be restarted
			return Task.CompletedTask;
		}
		/// <summary>
		/// dumb but simple "strategy": move the leftmost piece, if can capture: must capture
		/// </summary>
		/// <param name="timeoutMS"></param>
		/// <returns></returns>
		async Task<Movement> IUCIEngine.GetBestMove(int timeoutMS)
		{
			Movement bestMove = FindBestMove(game);
			await Task.Delay(250);
			return bestMove;
		}

		public virtual Movement FindBestMove(Game game)
		{
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
				capturingMoves.Sort(new MovementWithSideComparer());
				bestMove = capturingMoves[0].Movement;
			}
			else
			{
				noncapturingMoves.Sort(new MovementWithSideComparer());
				bestMove = noncapturingMoves[0].Movement;
			}
			return bestMove;
		}

		protected bool GetCaptureAndNonCaptureMoves(Board currentBoard, Side currentSide, Dictionary<Piece, Dictionary<(Square, Square), Movement>> possibleMovesPerPiece, bool isCapturePossible, out List<MovementWithSide> capturingMoves, out List<MovementWithSide> noncapturingMoves)
		{
			capturingMoves = new List<MovementWithSide>();
			noncapturingMoves = new List<MovementWithSide>();
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
							List<Movement> possiblePromotionMoves = new List<Movement>();
							PromotionMove promoMoveQueen = new PromotionMove(move.Start, move.End);
							PromotionMove promoMoveBishop = new PromotionMove(move.Start, move.End);
							PromotionMove promoMoveKnight = new PromotionMove(move.Start, move.End);
							PromotionMove promoMoveRook = new PromotionMove(move.Start, move.End);

							promoMoveQueen.SetPromotionPiece(new Queen(currentSide));
							promoMoveBishop.SetPromotionPiece(new Bishop(currentSide));
							promoMoveKnight.SetPromotionPiece(new Knight(currentSide));
							promoMoveRook.SetPromotionPiece(new Rook(currentSide));
							possiblePromotionMoves.Add(promoMoveQueen);
							possiblePromotionMoves.Add(promoMoveBishop);
							possiblePromotionMoves.Add(promoMoveKnight);
							possiblePromotionMoves.Add(promoMoveRook);

							List<MovementWithSide> listToAddTo = noncapturingMoves;

							if (IsCapturingMove(currentBoard, move.End))
							{
								listToAddTo = capturingMoves;
								isCapturePossible = true;
							}

							foreach (PromotionMove promoMove in possiblePromotionMoves) 
							{
								listToAddTo.Add(new MovementWithSide(currentSide, promoMove));
							}

							continue;
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

			return isCapturePossible;
		}

		void IUCIEngine.ShutDown(UnityEvent<Side> gameEndedEvent)
		{
			// nothing to do at shutdown
			// this AI does not subsribe to gameEndedEvent
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
