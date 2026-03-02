namespace Game1;

public class Program
{
    public const float Threshold = 223.5f;
    public const int GameCount = 2000;
    
    public static void Main(string[] args)
    {
        if (!(args.Length > 0 && args[0] == "evaluate"))
            Solution.Experiment(); // implement this function to get debug data
        Console.WriteLine("Staring test");
        float averageScore = Test.Run(gameCount: GameCount);
        Console.WriteLine($"Average Score: {averageScore}");
        Console.WriteLine($"Passed: {averageScore > Threshold}");
    }
}
