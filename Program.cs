namespace Game1;

public class Program
{
    public const float Threshold = 215;
    public const int GameCount = 500;
    
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
