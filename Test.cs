namespace Game1;

public static class Test
{

    public static float Run(int GameCount)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        float totalScore = 0;
        for (int gameNumber = 0; gameNumber < GameCount; gameNumber++)
        {
            GameState gameState = new();
            while (!gameState.IsGameDone)
            {
                int movesMade = gameState.TotalMovesMade;
                Solution.MakeMove(gameState);
                if (gameState.TotalMovesMade != movesMade + 1)
                {
                    Console.WriteLine("Either made no move or made multiple moves. Failing test.");
                    return 0;
                }
                if (watch.Elapsed.TotalSeconds > GameCount * 0.01f)
                {
                    Console.WriteLine("Test timed out. Failing test.");
                    return 0;
                }
            }
            float gameScore = gameState.TotalMovesMade;
            totalScore += gameScore;
        }    
        float averageScore = totalScore / GameCount;
        return averageScore;
    }
}