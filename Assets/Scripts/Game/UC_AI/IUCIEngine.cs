using System.Threading.Tasks;
using UnityEngine.Events;

namespace UnityChess.Engine {
	public interface IUCIEngine {
		bool CanRequestRestart();
		void Start();
		
		void ShutDown(UnityEvent<Side> gameEndedEvent);
		
		Task SetupNewGame(Game game, UnityEvent<Side> gameEndEvent, UnityAction<Side,int> startNewGameHandler);
		
		Task<Movement> GetBestMove(int timeoutMS);
	}
}