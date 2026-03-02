namespace Game1;

/// Game Rules:
/// - Player starts in the middle of a 11x11 grid
/// - The player repeatedly makes moves until their hunger reaches 30, at which point they lose.
/// - The final score is the number of moves the player makes before losing. 
/// - The player's hunger changes each move depending on what tile the player moves on to:
///     - Moving onto a road increases hunger by 1
///     - Moving onto grass increases hunger by 3
///     - Moving onto food decreases hunger by 7. The food then transitions to a grass tile. 
/// - Their are 5 moves:
///     - Move up, which moves the player 1 tile up
///     - Move down, which moves the player 1 tile down
///     - Move left, which moves the player 1 tile left
///     - Move right, which moves the player 1 tile right
///     - Pave, which turns the current tile into a road
/// - If the player attempts to move out of bounds, their move does not change their position.
/// - Every move there is a 1% chance of any grass tile turning into food
///     - This probability is multiplied by 99% each move. 
/// - Every move there is a 5% chance of any food tile turning into grass

public class GameState
{
    public GridPoint PlayerPosition { get; private set; } = new(MapWidth / 2, MapHeight / 2);
    public int PlayerHunger { get; private set; } = 0;

    readonly TileType[,] _map = new TileType[MapWidth, MapHeight];
    public TileType GetTile(int x, int y) => _map[x, y];
    public TileType GetTile(GridPoint point) => _map[point.x, point.y];
    TileType SetTile(int x, int y, TileType tile) => _map[x, y] = tile;

    public float GrowProbability { get; private set; } = 0.02f;
            
    public bool IsGameDone { get; private set; } = false;
    public int TotalMovesMade { get; private set; } = 0;

    public const int MapWidth = 11, MapHeight = 11;
    public const int HungerFromEating = 10;
    public const int HungerCostNormal = 3, HungerCostRoadMove = 1;
    public const int LoseAtHunger = 30;
    public const float GrowProbDecayRate = 0.01f;
    public const float FoodSpoilProbability = 0.05f;

    public GameState() { }

    public void MakeMove(MoveType move)
    {
        if (IsGameDone)
            throw new InvalidOperationException("Cannot make move, game is already done.");

        switch (move)
        {
            case MoveType.Up:
                PlayerPosition += new GridPoint(0, 1);
                break;
            case MoveType.Down:
                PlayerPosition += new GridPoint(0, -1);
                break;
            case MoveType.Left:
                PlayerPosition += new GridPoint(-1, 0);
                break;
            case MoveType.Right:
                PlayerPosition += new GridPoint(1, 0);
                break;                
            case MoveType.Pave:
                _map[PlayerPosition.x, PlayerPosition.y] = TileType.Road;
                break;
            default:
                throw new ArgumentException("Invalid move type.");
        }

        PlayerPosition = new(
            Math.Clamp(PlayerPosition.x, 0, MapWidth - 1),
            Math.Clamp(PlayerPosition.y, 0, MapHeight - 1)
        );

        PlayerHunger += GetTile(PlayerPosition) == TileType.Road ? HungerCostRoadMove : HungerCostNormal;
        if (GetTile(PlayerPosition) == TileType.Food)
        {
            SetTile(PlayerPosition.x, PlayerPosition.y, TileType.Grass);
            PlayerHunger = Math.Max(PlayerHunger - HungerFromEating, 0);
        }

        TotalMovesMade++;

        if (PlayerHunger >= LoseAtHunger)
            IsGameDone = true;
        else
            Tick();
    }


    void Tick()
    {
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                TileType tileType = GetTile(x, y);
                if (tileType == TileType.Grass)
                {
                    if (Random.Shared.NextDouble() < GrowProbability)
                        SetTile(x, y, TileType.Food);
                }
                else if (tileType == TileType.Food)
                {
                    if (Random.Shared.NextDouble() < FoodSpoilProbability)
                        SetTile(x, y, TileType.Grass);
                }
            }
        }

        GrowProbability *= 1 - GrowProbDecayRate;
    }
}

public readonly record struct GridPoint(int x, int y)
{
    public static GridPoint operator +(GridPoint a, GridPoint b) => new(a.x + b.x, a.y + b.y);
    public static GridPoint operator -(GridPoint a, GridPoint b) => new(a.x - b.x, a.y - b.y);
}

public enum TileType
{
    Grass,
    Road,
    Food,     
}

public enum MoveType
{
    Up,
    Down,
    Left,
    Right,
    Pave,
}