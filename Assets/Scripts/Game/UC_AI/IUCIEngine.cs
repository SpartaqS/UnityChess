using System.Threading.Tasks;
using UnityEngine.Events;

namespace UnityChess.Engine {
	public interface IUCIEngine {
		bool CanRequestRestart();
		void Start();
		
		void ShutDown(System.Action<Side> gameEndedEvent);
		
		Task SetupNewGame(Game game, System.Action<Side> gameEndEvent, UnityAction<Side,int> startNewGameHandler);
		
		Task<Movement> GetBestMove(int timeoutMS);
	}
}