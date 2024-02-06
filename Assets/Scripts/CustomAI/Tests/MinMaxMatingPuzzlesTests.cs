using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Collections.Generic;

using UnityChess.Engine;
using System.Threading.Tasks;
using UnityChess.StrategicAI.Tools;
using UnityChess;

namespace UnityChess.StrategicAI.Test
{
	public class MinMaxMatingPuzzlesTests
    {
		//private Board board;

		private GameManager gameManager;
		private bool gameEnded;
		private Side winnerSide;
		private int movesExecutedByBothPlayers;

		//TODO: probably bad (delete by replacing references to singletons from GameManager with event invokes on non-logical stuff //TEMP
		BoardManager boardManager;

		[OneTimeSetUp]
		public void OneTimeSetUp()
		{ // TODO: setup game manager
			//board = new Board();
			//board.ClearBoard();
			gameManager = new GameObject("Test GameManager").AddComponent<GameManager>();
			GameManager.Instance.UseCustomStartBoard = true;

			GameManager.Instance.GameEndedEvent.AddListener(HandleGameEnded);
			GameManager.MoveExecutedEvent += HandleMoveExecuted;

			boardManager = new GameObject("Test BoardManager").AddComponent<BoardManager>();
		}

		readonly (Square, Piece)[] twoRooksMatingTestBoard = {
			(new Square("e2"), new King(Side.White)),


			(new Square("a8"), new Rook(Side.Black)),
			(new Square("b6"), new King(Side.Black)),
			(new Square("b7"), new Rook(Side.Black)),
		};

		[SetUp]
		public void SetUp()
		{
			gameEnded = false;
			winnerSide = Side.None;
			movesExecutedByBothPlayers = 0;
		}

		[UnityTest]
		public IEnumerator MinMaxTwoRooksDepth2()
		{ // start a game and see if it is won by black in 7 moves
			int expectedMovesToCheckmate = 14; // last best result: 14 => 7 moves (white starts, each player does 7 moves)

			int searchDepth = 2;
			AIMinMaxSettings bothAIsSettings = new AIMinMaxSettings(searchDepth);

			GameManager.Instance.CustomStartingPositionPieces = twoRooksMatingTestBoard;
			GameManager.Instance.WhiteUciEngineCustomSettings = bothAIsSettings;
			GameManager.Instance.BlackUciEngineCustomSettings = bothAIsSettings;
			//TODO ensure the AIs are MinMaxes
			GameManager.Instance.StartNewGame(true, true);
			yield return new TestTools.WaitUntilForSeconds(() => gameEnded == true, 30f, GameManager.Instance.PauseGame);
			Assert.AreEqual(Side.Black, winnerSide);
			Assert.AreEqual(expectedMovesToCheckmate, movesExecutedByBothPlayers);
		}

		[UnityTest]
		public IEnumerator MinMaxTwoRooksDepth4()
		{ // start a game and see if it is won by black in 9 moves
			int expectedMovesToCheckmate = 18; // last best result: 18 => 9 moves (white starts, each player does 9 moves)

			int searchDepth = 4;
			AIMinMaxSettings bothAIsSettings = new AIMinMaxSettings(searchDepth);

			GameManager.Instance.CustomStartingPositionPieces = twoRooksMatingTestBoard;
			GameManager.Instance.WhiteUciEngineCustomSettings = bothAIsSettings;
			GameManager.Instance.BlackUciEngineCustomSettings = bothAIsSettings;
			//TODO ensure the AIs are MinMaxes
			GameManager.Instance.StartNewGame(true, true);
			yield return new TestTools.WaitUntilForSeconds(() => gameEnded == true, 30f, GameManager.Instance.PauseGame);
			Assert.AreEqual(Side.Black, winnerSide);
			Assert.AreEqual(expectedMovesToCheckmate, movesExecutedByBothPlayers);
		}

		[UnityTest]
		public IEnumerator MinMaxTwoRooksDepth6()
		{ // start a game and see if it is won by black in 9 moves
			int expectedMovesToCheckmate = 18; // last best result: 18 => 9 moves (white starts, each player does 9 moves)

			int searchDepth = 6;
			AIMinMaxSettings bothAIsSettings = new AIMinMaxSettings(searchDepth);

			GameManager.Instance.CustomStartingPositionPieces = twoRooksMatingTestBoard;
			GameManager.Instance.WhiteUciEngineCustomSettings = bothAIsSettings;
			GameManager.Instance.BlackUciEngineCustomSettings = bothAIsSettings;
			//TODO ensure the AIs are MinMaxes
			GameManager.Instance.StartNewGame(true, true);
			//yield return WaitForSecondsRealtime
			yield return new TestTools.WaitUntilForSeconds(() => gameEnded == true, 30f, GameManager.Instance.PauseGame);
			Assert.AreEqual(Side.Black, winnerSide);
			Assert.AreEqual(expectedMovesToCheckmate, movesExecutedByBothPlayers);
		}

		private void HandleGameEnded(Side winnerSide)
		{
			gameEnded = true;
			this.winnerSide = winnerSide;
		}

		private void HandleMoveExecuted()
		{
			this.movesExecutedByBothPlayers += 1;
		}

		[OneTimeTearDown]
		public void TearDown()
		{
			gameManager.GameEndedEvent.RemoveListener(HandleGameEnded);
			Object.DestroyImmediate(gameManager.gameObject);
			Object.DestroyImmediate(boardManager.gameObject);
		}

	}
}