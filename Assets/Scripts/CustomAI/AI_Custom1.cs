using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityChess.Engine;
using System.Threading.Tasks;

namespace UnityChess.StrategicAI
{
	public class AI_IUCIEngine : IUCIEngine
	{
		Task<Movement> IUCIEngine.GetBestMove(int timeoutMS)
		{
			throw new System.NotImplementedException();
		}

		Task IUCIEngine.SetupNewGame(Game game)
		{
			throw new System.NotImplementedException();
		}

		void IUCIEngine.ShutDown()
		{
			throw new System.NotImplementedException();
		}

		void IUCIEngine.Start()
		{
			throw new System.NotImplementedException();
		}
	}
}
