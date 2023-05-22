using System.Threading.Tasks;

namespace UnityChess.Engine {
	public interface IUCIEngine {
		void Start();
		
		void ShutDown(System.Action<Side> gameEndedEvent);
		
		Task SetupNewGame(Game game, System.Action<Side> gameEndEvent);
		
		Task<Movement> GetBestMove(int timeoutMS);
	}
}