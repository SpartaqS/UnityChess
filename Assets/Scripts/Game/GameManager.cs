// MLAgents definitions
//#define TRAIN_WHITE_AI
//#define TRAIN_BLACK_AI
//#define BLACK_HUMAN_VS_AI
#define WHITE_HUMAN_VS_AI
//#define AI_TEST
//#define DEBUG_VIEW
#if TRAIN_WHITE_AI
#define AI_TEST
#elif TRAIN_BLACK_AI
#define AI_TEST
#elif TRAIN_BOTH_AI
#define AI_TEST
#endif
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityChess;
using UnityChess.Engine;
using UnityChess.StrategicAI;
using UnityEngine;

public class GameManager : MonoBehaviourSingleton<GameManager> {
	//GameManagerState gameManagerState = GameManagerState.gameNotStarted;
	int aiAgentsRequieredRequestsToStartNewGame = 0;
	int aiAgentsRequestingToStartNewGame = 1;
	public static event Action NewGameStartedEvent;
	public UnityEngine.Events.UnityEvent<Side> GameEndedEvent = new UnityEngine.Events.UnityEvent<Side>(); // the winner of the game is broadcasted
	public static event Action GameResetToHalfMoveEvent;
	public static event Action MoveExecutedEvent;

	public Board CurrentBoard {
		get {
			game.BoardTimeline.TryGetCurrent(out Board currentBoard);
			return currentBoard;
		}
	}

	public Side SideToMove {
		get {
			game.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions);
			return currentConditions.SideToMove;
		}
	}

	public Side StartingSide => game.ConditionsTimeline[0].SideToMove;
	public Timeline<HalfMove> HalfMoveTimeline => game.HalfMoveTimeline;
	public int LatestHalfMoveIndex => game.HalfMoveTimeline.HeadIndex;
	public int FullMoveNumber => StartingSide switch {
		Side.White => LatestHalfMoveIndex / 2 + 1,
		Side.Black => (LatestHalfMoveIndex + 1) / 2 + 1,
		_ => -1
	};

	private bool isWhiteAI;
	private bool isBlackAI;

	public List<(Square, Piece)> CurrentPieces {
		get {
			currentPiecesBacking.Clear();
			for (int file = 1; file <= 8; file++) {
				for (int rank = 1; rank <= 8; rank++) {
					Piece piece = CurrentBoard[file, rank];
					if (piece != null) currentPiecesBacking.Add((new Square(file, rank), piece));
				}
			}

			return currentPiecesBacking;
		}
	}

	private readonly List<(Square, Piece)> currentPiecesBacking = new List<(Square, Piece)>();

	[SerializeField] private UnityChessDebug unityChessDebug;
	private Game game;
	private FENSerializer fenSerializer;
	private PGNSerializer pgnSerializer;
	private CancellationTokenSource promotionUITaskCancellationTokenSource;
	private ElectedPiece userPromotionChoice = ElectedPiece.None;
	private Dictionary<GameSerializationType, IGameSerializer> serializersByType;
	private GameSerializationType selectedSerializationType = GameSerializationType.FEN;

	private IUCIEngine whiteUciEngine;
	private IUCIEngine blackUciEngine;

	[SerializeField] private AIType whiteAiTypeToCreate = AIType.None;
	[SerializeField] private AIType blackAiTypeToCreate = AIType.None;

	[SerializeField]
	private UCIEngineCustomSettings whiteUciEngineCustomSettings = null;
	[SerializeField]
	private UCIEngineCustomSettings blackUciEngineCustomSettings = null;

	[SerializeField]
	private UCIEngineCustomSettings minMaxDefaultSettings = null;
	[SerializeField]
	private UCIEngineCustomSettings mctsDefaultSettings = null;
	
	public void Start() {
		VisualPiece.VisualPieceMoved += OnPieceMoved;

		serializersByType = new Dictionary<GameSerializationType, IGameSerializer> {
			[GameSerializationType.FEN] = new FENSerializer(),
			[GameSerializationType.PGN] = new PGNSerializer()
		};

		if (startNewGameInStart)
		{
			StartNewGame();
		}
		
#if DEBUG_VIEW
		unityChessDebug.gameObject.SetActive(true);
		unityChessDebug.enabled = true;
#endif
	}

	private void OnDestroy() {
		DestroyAIPlayer(whiteUciEngine);
		DestroyAIPlayer(blackUciEngine);
	}

	[SerializeField] GameObject blackMLAgentAiPrefab; //TEMP: use a dictionary or something to store correct prefabs for each ai
	[SerializeField] GameObject whiteMLAgentAiPrefab; //TEMP: use a dictionary or something to store correct prefabs for each ai

	[SerializeField] bool startNewGameInStart = false; // in tests we do not want to start a game when initializing the GameManager
	[SerializeField] bool useCustomStartBoard = false;
	[SerializeField] (Square, Piece)[] customStartingPositionPieces = {
			(new Square("h2"), new King(Side.Black)),


			(new Square("a6"), new Rook(Side.White)),
			(new Square("b8"), new King(Side.White)),
			(new Square("c8"), new Rook(Side.White)),
		};

	//[SerializeField]
	//(Square, Piece)[] customStartingPositionPieces = {
	//		(new Square("e2"), new King(Side.White)),


	//		(new Square("a8"), new Rook(Side.Black)),
	//		(new Square("b6"), new King(Side.Black)),
	//		(new Square("b7"), new Rook(Side.Black)),
	//	};
	
	//[SerializeField]
	//(Square, Piece)[] customStartingPositionPieces = {
	//		(new Square("h7"), new King(Side.White)),


	//		(new Square("a2"), new Rook(Side.Black)),
	//		(new Square("d4"), new King(Side.Black)),
	//		(new Square("g2"), new Rook(Side.Black)),
	//	};

	// these two are used in tests
	public bool UseCustomStartBoard { get => useCustomStartBoard; set => useCustomStartBoard = value; }
	public (Square, Piece)[] CustomStartingPositionPieces { get => customStartingPositionPieces; set => customStartingPositionPieces = value; }
	
	public UCIEngineCustomSettings WhiteUciEngineCustomSettings { get => whiteUciEngineCustomSettings; set => whiteUciEngineCustomSettings = value; }
	public UCIEngineCustomSettings BlackUciEngineCustomSettings { get => blackUciEngineCustomSettings; set => blackUciEngineCustomSettings = value; }
	public AIType WhiteAiTypeToCreate { get => whiteAiTypeToCreate; set => whiteAiTypeToCreate = value; }
	public AIType BlackAiTypeToCreate { get => blackAiTypeToCreate; set => blackAiTypeToCreate = value; }


	private void CreateSelectedAIPlayer(ref IUCIEngine uciEngine, AIType aiType, Side side)
	{

		//blackUciEngine = new MockUCIEngine(); // temporarily not use this, until I fix problem with conflicting IAsyncEnumerable<T> // need to figure out a neat way to pass the correct types somehow
		//blackUciEngine = CreateAIPlayer(typeof(AI_UCIEngine1));
		//blackUciEngine = CreateAIPlayer(typeof(AI_MLAgent1), Side.Black);

		UCIEngineCustomSettings selectedUCIEngineSettings = whiteUciEngineCustomSettings;
		if(side == Side.Black)
		{
			selectedUCIEngineSettings = blackUciEngineCustomSettings; //TODO select approptiate settings based on AI type
		}

		switch (aiType)
		{
			case AIType.RandomAggressive:
				uciEngine = CreateAIPlayer(typeof(AI_UCIEngineRandom1), side);
				break;
			case AIType.MinMax:
				uciEngine = CreateAIPlayer(typeof(AI_MinMax), side, selectedUCIEngineSettings == null ? minMaxDefaultSettings : selectedUCIEngineSettings );
				break;
			case AIType.MCTS:
				uciEngine = CreateAIPlayer(typeof(AI_MCTS), side, selectedUCIEngineSettings == null ? mctsDefaultSettings : selectedUCIEngineSettings);
				break;
			case AIType.ReinforcementLearning:
				uciEngine = CreateAIPlayer(typeof(AI_MLAgent1), side);
				break;
			case AIType.None:
			default:
				throw new SystemException("No AIType assigned for: " + side);
		}

	}


	/// <summary>
	/// used when creating AI Players who are not customizable
	/// </summary>
	/// <param name="aiType"></param>
	/// <param name="side"></param>
	/// <returns></returns>
	private IUCIEngine CreateAIPlayer(Type aiType, Side side)
	{
		return CreateAIPlayer(aiType, side, null);
	}

	/// <summary>
	/// Create AI Player and apply custom settings if such are provided
	/// </summary>
	/// <param name="aiType"></param>
	/// <param name="side"></param>
	/// <param name="customSettings"></param>
	/// <returns></returns>
	private IUCIEngine CreateAIPlayer(Type aiType, Side side, UCIEngineCustomSettings customSettings)
	{
		if (!typeof(IUCIEngine).IsAssignableFrom(aiType))
			throw new Exception("aiType is not a IUCIEngine");

		if (side != Side.White && side != Side.Black)
			throw new Exception("side is neither Black nor White");

		IUCIEngine engine = null;

		if (typeof(MonoBehaviour).IsAssignableFrom(aiType))
		{
			GameObject aiPrefab = side == Side.Black ? blackMLAgentAiPrefab : whiteMLAgentAiPrefab;
			GameObject aiGameObject = Instantiate(aiPrefab, transform);
			// if Type is a MonoBehaviour, the component should be in the prefab
			MonoBehaviour instantiatedMonoBehaviour = (MonoBehaviour)aiGameObject.GetComponent<IUCIEngine>();
			engine = instantiatedMonoBehaviour as IUCIEngine;
		}
		else
		{
			engine = (IUCIEngine)Activator.CreateInstance(aiType);
		}

		if (customSettings != null && typeof(IUCIEngineWithCustomSettings).IsAssignableFrom(aiType))
		{
			((IUCIEngineWithCustomSettings)engine).ApplyCustomSettings(customSettings);
		}

		return engine;
	}

	private IUCIEngine DestroyAIPlayer(IUCIEngine aiType)
	{
		if (aiType == null)
		{// no player to destroy
			return null;
		}

		aiType.ShutDown(GameEndedEvent);
		MonoBehaviour monoBehaviourAI = aiType as MonoBehaviour;
		if (monoBehaviourAI != null)
		{
			Destroy(monoBehaviourAI.gameObject);
		}

		return null;
	}

	/// <summary>
	/// AI can request to start a new game
	/// </summary>
	/// <param name="requestingSide"></param>
	/// <param name="votingPower"></param> use 1 to request after a game has ended, use 2 to reqest a reset during training
	private void HandleAIStartNewGameRequest(Side requestingSide, int votingPower = 1)
	{
		aiAgentsRequestingToStartNewGame += votingPower;
		Debug.LogWarning($"{requestingSide} AI requested to start a new game (${votingPower}) [{aiAgentsRequestingToStartNewGame}/{aiAgentsRequieredRequestsToStartNewGame}]");
		if (aiAgentsRequieredRequestsToStartNewGame > aiAgentsRequestingToStartNewGame)
			return;

		StartNewGame();
	}

	public void StartNewGameWithCurrentSettings()
	{
		StartNewGame(isWhiteAI, isBlackAI);
	}

#if AI_TEST
	public async void StartNewGame(bool isWhiteAI = true, bool isBlackAI = true) {
#elif WHITE_HUMAN_VS_AI
	public async void StartNewGame(bool isWhiteAI = false, bool isBlackAI = true) {
#elif BLACK_HUMAN_VS_AI
	public async void StartNewGame(bool isWhiteAI = true, bool isBlackAI = false) {
#else
	public async void StartNewGame(bool isWhiteAI = false, bool isBlackAI = false) {
#endif
		//// do not start a game if we are starting
		//if (gameManagerState == GameManagerState.gameStarting)
		//	return;

		if (useCustomStartBoard)
		{
			game = new Game(GameConditions.NormalStartingConditions, customStartingPositionPieces);
		}
		else
		{
			game = new Game();
		}

		this.isWhiteAI = isWhiteAI;
		this.isBlackAI = isBlackAI;
		aiAgentsRequieredRequestsToStartNewGame = 0;
		aiAgentsRequestingToStartNewGame = 0;
		DestroyAIPlayer(whiteUciEngine);
		DestroyAIPlayer(blackUciEngine);
		if (isWhiteAI || isBlackAI) {
			if (isWhiteAI)
			{
#if TRAIN_WHITE_AI || TRAIN_BOTH_AI
				//whiteUciEngine = CreateAIPlayer(typeof(AI_MLAgent1), Side.White);
				whiteUciEngine = CreateAIPlayer(typeof(AI_MLAgent2), Side.White);
#elif TRAIN_BLACK_AI
				whiteUciEngine = CreateAIPlayer(typeof(AI_UCIEngineRandom1), Side.White);
#else
				CreateSelectedAIPlayer(ref whiteUciEngine, whiteAiTypeToCreate, Side.White);
#endif


				if (whiteUciEngine.CanRequestRestart())
				{
					aiAgentsRequieredRequestsToStartNewGame += 1;
				}
				whiteUciEngine.Start();

				

				await whiteUciEngine.SetupNewGame(game, GameEndedEvent, HandleAIStartNewGameRequest);
			}

			if (isBlackAI)
			{
#if TRAIN_WHITE_AI
				blackUciEngine = CreateAIPlayer(typeof(AI_UCIEngineRandom1), Side.Black);
#elif TRAIN_BLACK_AI || TRAIN_BOTH_AI
				//blackUciEngine = CreateAIPlayer(typeof(AI_MLAgent1), Side.Black);
				blackUciEngine = CreateAIPlayer(typeof(AI_MLAgent2), Side.Black);
#else
				CreateSelectedAIPlayer(ref blackUciEngine, blackAiTypeToCreate, Side.Black);
#endif
				
				if (blackUciEngine.CanRequestRestart())
				{
					aiAgentsRequieredRequestsToStartNewGame += 1;
				}
				blackUciEngine.Start();
				await blackUciEngine.SetupNewGame(game, GameEndedEvent, HandleAIStartNewGameRequest);
			}
			
			NewGameStartedEvent?.Invoke();

			if (isWhiteAI) {
				Movement bestMove = await whiteUciEngine.GetBestMove(10_000);
				DoAIMove(bestMove);
			}
		} else {
			NewGameStartedEvent?.Invoke();
		}
	}

	public string SerializeGame() {
		return serializersByType.TryGetValue(selectedSerializationType, out IGameSerializer serializer)
			? serializer?.Serialize(game)
			: null;
	}
	
	public void LoadGame(string serializedGame) {
		game = serializersByType[selectedSerializationType].Deserialize(serializedGame);
		NewGameStartedEvent?.Invoke();
	}

	public void ResetGameToHalfMoveIndex(int halfMoveIndex) {
		if (!game.ResetGameToHalfMoveIndex(halfMoveIndex)) return;
		
		UIManager.Instance.SetActivePromotionUI(false);
		promotionUITaskCancellationTokenSource?.Cancel();
		GameResetToHalfMoveEvent?.Invoke();
	}

	private bool TryExecuteMove(Movement move) {
		if (!game.TryExecuteMove(move)) {
			return false;
		}

		HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);
		if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate) {
			BoardManager.Instance.SetActiveAllPieces(false);

			Side winnerSide = Side.None;
			if (latestHalfMove.CausedCheckmate)
			{
				winnerSide = latestHalfMove.Piece.Owner;
			} // otherwise it was a stalemate, therefore noone won
			GameEndedEvent?.Invoke(winnerSide);
		} else {
			BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
		}
		MoveExecutedEvent?.Invoke();

		return true;
	}
	
	private async Task<bool> TryHandleSpecialMoveBehaviourAsync(SpecialMove specialMove) {
		switch (specialMove) {
			case CastlingMove castlingMove:
				BoardManager.Instance.CastleRook(castlingMove.RookSquare, castlingMove.GetRookEndSquare());
				return true;
			case EnPassantMove enPassantMove:
				BoardManager.Instance.TryDestroyVisualPiece(enPassantMove.CapturedPawnSquare);
				return true;
			case PromotionMove { PromotionPiece: null } promotionMove:
				UIManager.Instance.SetActivePromotionUI(true);
				BoardManager.Instance.SetActiveAllPieces(false);

				promotionUITaskCancellationTokenSource?.Cancel();
				promotionUITaskCancellationTokenSource = new CancellationTokenSource();
				
				ElectedPiece choice = await Task.Run(GetUserPromotionPieceChoice, promotionUITaskCancellationTokenSource.Token);
				
				UIManager.Instance.SetActivePromotionUI(false);
				BoardManager.Instance.SetActiveAllPieces(true);

				if (promotionUITaskCancellationTokenSource == null
				    || promotionUITaskCancellationTokenSource.Token.IsCancellationRequested
				) { return false; }

				promotionMove.SetPromotionPiece(
					PromotionUtil.GeneratePromotionPiece(choice, SideToMove)
				);
				BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
				BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
				BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);

				promotionUITaskCancellationTokenSource = null;
				return true;
			case PromotionMove promotionMove:
				BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
				BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
				BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);
				
				return true;
			default:
				return false;
		}
	}
	
	private ElectedPiece GetUserPromotionPieceChoice() {
		while (userPromotionChoice == ElectedPiece.None) { }

		ElectedPiece result = userPromotionChoice;
		userPromotionChoice = ElectedPiece.None;
		return result;
	}
	
	public void ElectPiece(ElectedPiece choice) {
		userPromotionChoice = choice;
	}

	private async void OnPieceMoved(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null) {
		Square endSquare = new Square(closestBoardSquareTransform.name);

		if (isGamePaused) // do not modify the game after it has been paused
			return;

		if (!game.TryGetLegalMove(movedPieceInitialSquare, endSquare, out Movement move)) {
			movedPieceTransform.position = movedPieceTransform.parent.position;
#if DEBUG_VIEW
			Piece movedPiece = CurrentBoard[movedPieceInitialSquare];
			game.TryGetLegalMovesForPiece(movedPiece, out ICollection<Movement> legalMoves);
			UnityChessDebug.ShowLegalMovesInLog(legalMoves);
#endif
			return;
		}

		if (move is PromotionMove promotionMove) {
			promotionMove.SetPromotionPiece(promotionPiece);
		}

		if ((move is not SpecialMove specialMove || await TryHandleSpecialMoveBehaviourAsync(specialMove))
		    && TryExecuteMove(move)
		) {
			if (move is not SpecialMove) { BoardManager.Instance.TryDestroyVisualPiece(move.End); }

			if (move is PromotionMove) {
				movedPieceTransform = BoardManager.Instance.GetPieceGOAtPosition(move.End).transform;
			}

			movedPieceTransform.parent = closestBoardSquareTransform;
			movedPieceTransform.position = closestBoardSquareTransform.position;
		}

		bool gameIsOver = game.HalfMoveTimeline.TryGetCurrent(out HalfMove lastHalfMove)
		                  && lastHalfMove.CausedStalemate || lastHalfMove.CausedCheckmate;
		if (gameIsOver)
			return;
		if(SideToMove == Side.White && isWhiteAI)	    
		{
			Movement bestMove = await whiteUciEngine.GetBestMove(10_000);
			DoAIMove(bestMove);
		}
		else if (SideToMove == Side.Black && isBlackAI)
		{
			Movement bestMove = await blackUciEngine.GetBestMove(10_000);
			DoAIMove(bestMove);
		}
	}

	private void DoAIMove(Movement move) {
		if(move == null)
		{
			GameEndedEvent?.Invoke(Side.None);
		}
		GameObject movedPiece = BoardManager.Instance.GetPieceGOAtPosition(move.Start);
		GameObject endSquareGO = BoardManager.Instance.GetSquareGOByPosition(move.End);
		OnPieceMoved(
			move.Start,
			movedPiece.transform,
			endSquareGO.transform,
			(move as PromotionMove)?.PromotionPiece
		);
	}

	public bool HasLegalMoves(Piece piece) {
		return game.TryGetLegalMovesForPiece(piece, out _);
	}

	bool isGamePaused = false;
	public void PauseGame()
	{
		isGamePaused = true;
		//TODO: if current player is an AI, order it to cancel its current search
	}

	public async void UnPauseGame()
	{
		isGamePaused = false;

		if (isWhiteAI && SideToMove == Side.White)
		{
			Movement bestMoveAfterUnpause = await whiteUciEngine.GetBestMove(10_000);
			DoAIMove(bestMoveAfterUnpause);
		}
		else if (isBlackAI && SideToMove == Side.Black)
		{
			Movement bestMoveAfterUnpause = await blackUciEngine.GetBestMove(10_000);
			DoAIMove(bestMoveAfterUnpause);
		}
	}

	public enum GameManagerState // TODO ? Delete this?
	{
		gameNotStarted,
		gameStarting,
		gameInProgress,
		gameFinished
	}

	public enum AIType
	{
		None,
		RandomAggressive,
		MinMax,
		MCTS,
		ReinforcementLearning
	}
}