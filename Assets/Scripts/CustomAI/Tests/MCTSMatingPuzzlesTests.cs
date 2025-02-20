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
	public class MCTSMatingPuzzlesTests
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
			(new Square("h2"), new King(Side.Black)),


			(new Square("a6"), new Rook(Side.White)),
			(new Square("b8"), new King(Side.White)),
			(new Square("c8"), new Rook(Side.White)),
		};

		readonly (Square, Piece)[] mateInOneStartingBoard = {
			(new Square("h2"), new King(Side.Black)),


			(new Square("a7"), new Rook(Side.White)),
			(new Square("b2"), new King(Side.White)),
			(new Square("g8"), new Rook(Side.White)),
		};

		[SetUp]
		public void SetUp()
		{
			gameEnded = false;
			winnerSide = Side.None;
			movesExecutedByBothPlayers = 0;
		}

		[UnityTest]
		public IEnumerator MCTSMateInOne()
		{ // start a game and see if it is won by black in 7 moves
			int expectedMovesToCheckmate = 1; // AI is expected to win in one move

			SetupMateInOneTest();
			
			GameManager.Instance.StartNewGame(true, true);
			yield return new TestTools.WaitUntilForSeconds(() => gameEnded == true, 15f, GameManager.Instance.PauseGame);
			Assert.AreEqual(Side.White, winnerSide);
			Assert.AreEqual(expectedMovesToCheckmate, movesExecutedByBothPlayers);
		}

		private void SetupMateInOneTest()
		{
			AIMCTSSettings bothAIsSettings = ScriptableObject.CreateInstance<AIMCTSSettings>();
			bothAIsSettings.LeafsToExplore = 100;
			bothAIsSettings.PlayoutsPerLeaf = 1;

			GameManager.Instance.CustomStartingPositionPieces = mateInOneStartingBoard;
			GameManager.Instance.WhiteUciEngineCustomSettings = bothAIsSettings;
			GameManager.Instance.BlackUciEngineCustomSettings = bothAIsSettings;
		}


		[UnityTest]
		public IEnumerator MCTSTwoRooks()
		{ // start a game and see if it is won by black in 7 moves
			int expectedMovesToCheckmate = 14; // last best result: 14 => 7 moves (white starts, each player does 7 moves)

			SetupTwoRooksTest();
			//TODO ensure the AIs are MinMaxes
			GameManager.Instance.StartNewGame(true, true);
			yield return new TestTools.WaitUntilForSeconds(() => gameEnded == true, 30f, GameManager.Instance.PauseGame);
			Assert.AreEqual(Side.White, winnerSide);
			Assert.AreEqual(expectedMovesToCheckmate, movesExecutedByBothPlayers);
		}

		private void SetupTwoRooksTest()
		{
			AIMCTSSettings bothAIsSettings = ScriptableObject.CreateInstance<AIMCTSSettings>();
			bothAIsSettings.LeafsToExplore = 100;
			bothAIsSettings.PlayoutsPerLeaf = 1;

			GameManager.Instance.CustomStartingPositionPieces = twoRooksMatingTestBoard;
			GameManager.Instance.WhiteUciEngineCustomSettings = bothAIsSettings;
			GameManager.Instance.BlackUciEngineCustomSettings = bothAIsSettings;
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
		public void OneTimeTearDown()
		{
			gameManager.GameEndedEvent.RemoveListener(HandleGameEnded);
			Object.DestroyImmediate(gameManager.gameObject);
			Object.DestroyImmediate(boardManager.gameObject);
		}

	}
}