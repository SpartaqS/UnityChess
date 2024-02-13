using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityChess.StrategicAI.Tools
{
	public static class PiecePositionScoreTable
	{
		public static int Read(int[] tableToRead, Square square, bool readAsBlack)
		{
			//Square oldSquare = new Square(square.File, square.Rank); ///DEBUG
			if (readAsBlack) // files are the same but ranks are flipped (instrad of 1 to 8 , we need to read the table as if they were 8 to 1)
			{
				square = new Square(square.File, 9 - square.Rank);
			}
			//try { int value = tableToRead[square.File - 1 + (square.Rank - 1) * 8]; }
			//catch
			//{
			//	Debug.LogError("Exception at new square:" + square.ToString()); ///DEBUG
			//	Debug.LogError("Exception at old square:" + oldSquare.ToString()); ///DEBUG
			//}

			// file in Square is from 1 to 8
			// so in human readable table the file == file - 1
			// rank in Square is from 1 to 8
			// but in table the corresponding ranks are ordered is from 7 to 0 (b1 is 1 + 7 * 8)
			// 1 -> 7
			// 2 -> 6
			// 3 -> 5
			// 4 -> 4
			// 5 -> 3
			// 6 -> 2
			// 7 -> 1
			// 8 -> 0
			// so in human readable table the rank == 8 - rank
			return tableToRead[square.File - 1 + (8 - square.Rank) * 8];
		}

		public static int Read(Piece piece, Square square)
		{
			bool readAsBlack = piece.Owner == Side.Black;

			switch (piece)
			{
				case Pawn:
					return Read(Pawns, square, readAsBlack);
				case Knight:
					return Read(Knights, square, readAsBlack);
				case Bishop:
					return Read(Bishops, square, readAsBlack);
				case Rook:
					return Read(Rooks, square, readAsBlack);
				case Queen:
					return Read(Queens, square, readAsBlack);
				case King:
					return Read(Kings, square, readAsBlack);
				default:
					throw new System.Exception("Unregistered piece read attempt");
			}
		}

		// the values are setup in such a way to read it as if this was the chess board ( read as white)
		public static readonly int[] Pawns = {
			 0,   0,   0,   0,   0,   0,   0,   0, //8
			50,  50,  50,  50,  50,  50,  50,  50, //7
			10,  10,  20,  30,  30,  20,  10,  10, //6
			 5,   5,  10,  25,  25,  10,   5,   5, //5
			 0,   0,   0,  20,  20,   0,   0,   0, //4
			 5,  -5, -10,   0,   0, -10,  -5,   5, //3
			 5,  10,  10, -20, -20,  10,  10,   5, //2
			 0,   0,   0,   0,   0,   0,   0,   0  //1
		}; //a,   b,   c,   d,   e,   f,   g,   h  //

		public static readonly int[] Knights = {
			-50, -40, -30, -30, -30, -30, -40, -50, //8
			-40, -20,   0,   0,   0,   0, -20, -40, //7
			-30,   0,  10,  15,  15,  10,   0, -30, //6
			-30,   5,  15,  20,  20,  15,   5, -30, //5
			-30,   0,  15,  20,  20,  15,   0, -30, //4
			-30,   5,  10,  15,  15,  10,   5, -30, //3
			-40, -20,   0,   5,   5,   0, -20, -40, //2
			-50, -40, -30, -30, -30, -30, -40, -50, //1
		};  //a,   b,   c,   d,   e,   f,   g,   h  //

		public static readonly int[] Bishops =  {
			-20, -10, -10, -10, -10, -10, -10, -20, //8
			-10,   0,   0,   0,   0,   0,   0, -10, //7
			-10,   0,   5,  10,  10,   5,   0, -10, //6
			-10,   5,   5,  10,  10,   5,   5, -10, //5
			-10,   0,  10,  10,  10,  10,   0, -10, //4
			-10,  10,  10,  10,  10,  10,  10, -10, //3
			-10,   5,   0,   0,   0,   0,   5, -10, //2
			-20, -10, -10, -10, -10, -10, -10, -20, //1
		};  //a,   b,   c,   d,   e,   f,   g,   h  //

		public static readonly int[] Rooks =  {
			  0,  0,   0,   0,   0,   0,   0,   0, //8
 			  5,  10, 10,  10,  10,  10,  10,   5, //7
			 -5,  0,   0,   0,   0,   0,   0,  -5, //6
			 -5,  0,   0,   0,   0,   0,   0,  -5, //5
			 -5,  0,   0,   0,   0,   0,   0,  -5, //4
			 -5,  0,   0,   0,   0,   0,   0,  -5, //3
			 -5,  0,   0,   0,   0,   0,   0,  -5, //2
			  0,  0,   0,   5,   5,   0,   0,   0  //1
		};  //a,  b,   c,   d,   e,   f,   g,   h  //

		public static readonly int[] Queens =  {
			-20, -10, -10,  -5,  -5, -10, -10, -20, //8
			-10,   0,   0,   0,   0,   0,   0, -10, //7
			-10,   0,   5,   5,   5,   5,   0, -10, //6
			 -5,   0,   5,   5,   5,   5,   0,  -5, //5
			  0,   0,   5,   5,   5,   5,   0,  -5, //4
			-10,   5,   5,   5,   5,   5,   0, -10, //3
			-10,   0,   5,   0,   0,   0,   0, -10, //2
			-20, -10, -10,  -5,  -5, -10, -10, -20  //1
		};  //a,   b,   c,   d,   e,   f,   g,   h  //

		public static readonly int[] Kings =
		{
			-20, -10, -10, -10, -10, -10, -10, -20, //8
			-5,   0,   5,   5,   5,   5,   0,  -5,  //7
			-10, -5,   20,  30,  30,  20,  -5, -10, //6
			-15, -10,  35,  45,  45,  35, -10, -15, //5
			-20, -15,  30,  40,  40,  30, -15, -20, //4
			-25, -20,  20,  25,  25,  20, -20, -25, //3
			-30, -25,   0,   0,   0,   0, -25, -30, //2
			-50, -30, -30, -30, -30, -30, -30, -50  //1
		};  //a,   b,   c,   d,   e,   f,   g,   h  //

	}
}
