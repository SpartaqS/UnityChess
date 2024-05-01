using UnityEngine;
using UnityChess.Engine;

namespace UnityChess.StrategicAI
{
	[CreateAssetMenu(menuName = "StrategicAI/AIMCTSSettings")]
	public class AIMCTSSettings : UCIEngineCustomSettings
	{
		public int LeafsToExplore = 1;
		public int PlayoutsPerLeaf = 1;

		public AIMCTSSettings(int leavesToExplore, int playoutsPerLeaf)
		{
			LeafsToExplore = leavesToExplore;
			PlayoutsPerLeaf = playoutsPerLeaf;
		}
	}
}