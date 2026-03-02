namespace Game1;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("debug", StringComparison.OrdinalIgnoreCase))
        {
            int maxTurns = args.Length > 1 && int.TryParse(args[1], out int parsedTurns) ? parsedTurns : 120;
            RunDebug(maxTurns);
            return;
        }

        int gameCount = args.Length > 0 && int.TryParse(args[0], out int parsed) ? parsed : 1000;
        float score = Test.Run(gameCount);
        Console.WriteLine($"Average score over {gameCount} games: {score:F3}");
    }

    static void RunDebug(int maxTurns)
    {
        GameState gameState = new();
        int warnings = 0;

        Console.WriteLine("turn prePos preH postPos postH move mode build roads food bestD bestS bestM notes");

        int turn = 0;
        while (turn < maxTurns && !gameState.IsGameDone)
        {
            GridPoint prePos = gameState.PlayerPosition;
            int preHunger = gameState.PlayerHunger;
            TileType preTile = gameState.GetTile(prePos);
            int preFood = CountTiles(gameState, TileType.Food);
            int preRoad = CountTiles(gameState, TileType.Road);

            Solution.MakeMove(gameState);
            Solution.DecisionTrace trace = Solution.LastTrace;

            GridPoint postPos = gameState.PlayerPosition;
            int postHunger = gameState.PlayerHunger;

            string notes = "";
            if (trace.Mode == Solution.DecisionMode.ChaseFood && !trace.HasFoodTarget)
                notes = Append(notes, "CHASE_WITHOUT_TARGET");
            if (trace.Move == MoveType.Pave && preTile == TileType.Food)
                notes = Append(notes, "PAVED_OVER_FOOD");
            if (trace.InBuildPhase && trace.Mode == Solution.DecisionMode.IdlePave && trace.BuiltBlueprintRoads < 18)
                notes = Append(notes, "BUILD_PHASE_IDLE");

            if (notes.Length > 0)
                warnings++;

            Console.WriteLine(
                $"{turn,4} ({prePos.x,2},{prePos.y,2}) {preHunger,3} " +
                $"({postPos.x,2},{postPos.y,2}) {postHunger,3} {trace.Move,-5} {trace.Mode,-11} " +
                $"{(trace.InBuildPhase ? "Y" : "N"),5} {preRoad,5} {preFood,4} {trace.BestFoodDist,5} {trace.BestFoodSteps,5} {trace.BestFoodMetric,5} {notes}");

            turn++;
        }

        Console.WriteLine($"debug_end turns={turn} totalMoves={gameState.TotalMovesMade} hunger={gameState.PlayerHunger} warnings={warnings}");
    }

    static int CountTiles(GameState state, TileType tileType)
    {
        int count = 0;
        for (int x = 0; x < GameState.MapWidth; x++)
        {
            for (int y = 0; y < GameState.MapHeight; y++)
            {
                if (state.GetTile(x, y) == tileType)
                    count++;
            }
        }

        return count;
    }

    static string Append(string notes, string item) => notes.Length == 0 ? item : $"{notes}|{item}";
}
