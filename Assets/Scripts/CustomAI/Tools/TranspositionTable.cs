using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityChess.StrategicAI.Tools
{
	public class TranspositionTable
	{
		int maxEntryCount = 100000;
		Dictionary<string, Entry> evaluatedPositions = new Dictionary<string, Entry>(); //TEMP for now just use FEN string as "hash"

		/// <summary>
		/// try to get an evaluation of a hashed game state
		/// </summary>
		/// <param name="ttVal"></param>
		/// <param name="depth"></param>
		/// <param name="currentSearchDepth"></param>
		/// <param name="alpha"></param>
		/// <param name="beta"></param>
		/// <returns>true if lookup returns valid evaluation</returns>
		public bool LookupEvaluation(string positionHash, out int positionEvaluation, int depth, int alpha, int beta)
		{
			if (evaluatedPositions.ContainsKey(positionHash))
			{
				// examine the evalaution with respect to alpha and beta
				Entry entry = evaluatedPositions[positionHash];
				// Only use stored evaluation if it has been searched to at least the same depth as would be searched now
				if (entry.depth >= depth)
				{
					positionEvaluation = entry.value;
					// We have stored the exact evaluation for this position, so return it
					if (entry.evaluationType == EvaluationType.Exact)
					{
						return true;
					}
					// We have stored the upper bound of the eval for this position. If it's less than alpha then we don't need to
					// search the moves in this position as they won't interest us; otherwise we will have to search to find the exact value
					if (entry.evaluationType == EvaluationType.UpperBound && positionEvaluation <= alpha)
					{
						return true;
					}
					// We have stored the lower bound of the eval for this position. Only return if it causes a beta cut-off.
					if (entry.evaluationType == EvaluationType.LowerBound && positionEvaluation >= beta)
					{
						return true;
					}
				}

			}
			positionEvaluation = -1; // evaluation not found
			return false;
		}

		/// <summary>
		/// "dumb" method: will store key at all costs: 
		/// if position was already stored, overwrites the already stored evalaution
		/// overwrites a random evaluation if transposition table is full
		/// </summary>
		/// <param name="positionHash"></param>
		/// <param name="positionEvaluation"></param>
		/// <param name="depth"></param>
		/// <param name="evalType"></param>
		/// <param name="currentMovement"></param>
		public void StoreEvaluation(string positionHash, int positionEvaluation, int depth, EvaluationType evalType, Movement currentMovement)
		{
			if (evaluatedPositions.Count >= maxEntryCount)
			{
				Debug.LogWarning("max entry count reached");
				IEnumerator enumerator = evaluatedPositions.Keys.GetEnumerator();
				enumerator.MoveNext();
				//int keyToRemove = (int)enumerator.Current;
				string keyToRemove = (string)enumerator.Current; //TEMP
				if (keyToRemove != positionHash && !evaluatedPositions.ContainsKey(positionHash)) 
				{// delete a random entry only if adding the new one would break the limit of entries
					evaluatedPositions.Remove(keyToRemove);
				}
			}

			if (evaluatedPositions.ContainsKey(positionHash))
			{ // replace already stored evaluation with a new one
				evaluatedPositions.Remove(positionHash);
			}
			evaluatedPositions.Add(positionHash, new Entry(positionEvaluation, depth, currentMovement, evalType));
		}

		public void Clear()
		{
			evaluatedPositions.Clear();
		}

		public enum EvaluationType {
			// The value for this position is the exact evaluation
			Exact,
			// A move was found during the search that was too good, meaning the opponent will play a different move earlier on,
			// not allowing the position where this move was available to be reached. Because the search cuts off at
			// this point (beta cut-off), an even better move may exist. This means that the evaluation for the
			// position could be even higher, making the stored value the lower bound of the actual value.
			LowerBound,
			// No move during the search resulted in a position that was better than the current player could get from playing a
			// different move in an earlier position (i.e eval was <= alpha for all moves in the position).
			// Due to the way alpha-beta search works, the value we get here won't be the exact evaluation of the position,
			// but rather the upper bound of the evaluation. This means that the evaluation is, at most, equal to this value.
			UpperBound
		}

		private struct Entry
		{
			//public readonly int key; //TEMP not needed?
			public readonly int value;
			public readonly int depth; // how far in the future was this position discovered ('currentDepth' in min-max)
			public readonly Movement bestMove; // best move in this position
			public readonly EvaluationType evaluationType; // what kind of evaluation is stored

			public Entry(int value, int depth, Movement bestMove, EvaluationType evaluationType)
			{
				this.value = value;
				this.depth = depth;
				this.bestMove = bestMove;
				this.evaluationType = evaluationType;
			}
		}

		/// <summary>
		/// UNSAFE: make sure that board hash does exist or will return null
		/// </summary>
		/// <param name="currentBoardHash"></param>
		/// <returns></returns>
		internal Movement GetStoredMove(string currentBoardHash)
		{
			return evaluatedPositions[currentBoardHash].bestMove;
		}

		internal void GetNodeTypeAndDepth(string currentBoardHash, out EvaluationType evaluationType, out int depth)
		{
			Entry entry = evaluatedPositions[currentBoardHash];
			evaluationType = entry.evaluationType;
			depth = entry.depth;
		}
	}
}