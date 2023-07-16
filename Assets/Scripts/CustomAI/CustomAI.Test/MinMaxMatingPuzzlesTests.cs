using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityChess.Engine;
using System.Threading.Tasks;
using UnityChess.StrategicAI.Tools;
using UnityChess;

namespace UnityChess.StrategicAI.Test
{
    public class MinMaxMatingPuzzlesTests
    {
		private Board board;

		//private GameManager gameManager;

		[SetUp]
		public void Init()
		{ // TODO: setup game manager
			board = new Board();
			board.ClearBoard();
			//GameManager.Instance
		}

		[Test]
		public void MinMaxAddditionTempTest()
		{ // TODO: start game and see if it is won by black in  7 moves
			int a = 1;
			int b = 2;
			int expectedSum = 3;
			Assert.AreEqual(a + b, expectedSum);
		}
	}
}