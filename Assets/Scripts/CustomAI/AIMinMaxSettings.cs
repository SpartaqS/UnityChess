using UnityEngine;
using UnityChess.Engine;

namespace UnityChess.StrategicAI
{
	[CreateAssetMenu(menuName = "StrategicAI/MinMaxSettings")]
	public class AIMinMaxSettings : UCIEngineCustomSettings
	{
		public int SearchDepth = 4;

		public AIMinMaxSettings(int searchDepth)
		{
			SearchDepth = searchDepth;
		}
	}
}
