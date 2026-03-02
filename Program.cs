namespace Game1;

public class Program
{
    public static void Main(string[] args)
    {
        if (!(args.Length > 0 && args[0] == "evaluate"))
            Solution.Experiment(); // implement this function to get debug data
        Console.WriteLine("Staring test");
        float averageScore = Test.Run(500);
        Console.WriteLine($"Average Score: {averageScore}");
        Console.WriteLine($"Passed: {averageScore > 215}");
    }
}
