namespace Game1;

public static class Solution
{
    public enum DecisionMode : byte
    {
        ChaseFood,
        BuildPave,
        BuildMove,
        CenterReturn,
        IdlePave,
    }

    public readonly record struct DecisionTrace(
        DecisionMode Mode,
        MoveType Move,
        int PlayerHunger,
        bool InBuildPhase,
        int BuiltBlueprintRoads,
        int FoodCount,
        bool HasFoodTarget,
        int BestFoodDist,
        int BestFoodSteps,
        int BestFoodMetric
    );

    public static DecisionTrace LastTrace { get; private set; }

    const int Width = GameState.MapWidth;
    const int Height = GameState.MapHeight;
    const int TileCount = Width * Height;
    const int Inf = 1_000_000;

    const int BuildGoalTiles = 40;
    const int BuildCutoffMoves = 170;
    const int InitialBuildSafeHunger = 10;
    const int MaxBuildTravelSteps = 6;
    const int UrgentHunger = 18;

    public static void MakeMove(GameState gameState)
    {
        GridPoint player = gameState.PlayerPosition;
        int playerX = player.x;
        int playerY = player.y;

        int builtBlueprintRoads = 0;
        int currentTileX = playerX;
        int currentTileY = playerY;
        bool isCurrentBlueprint = IsBlueprintTile(currentTileX, currentTileY);
        bool currentIsRoad = gameState.GetTile(currentTileX, currentTileY) == TileType.Road;
        int buildSafeHunger = GetBuildSafeHunger(gameState.TotalMovesMade);
        bool canConsiderBuilding = gameState.TotalMovesMade < BuildCutoffMoves;

        Span<int> foodIndices = stackalloc int[TileCount];
        int foodCount = 0;
        for (int x = 0; x < GameState.MapWidth; x++)
        {
            for (int y = 0; y < GameState.MapHeight; y++)
            {
                TileType tile = gameState.GetTile(x, y);
                if (tile == TileType.Food)
                    foodIndices[foodCount++] = ToIndex(x, y);
                if (tile == TileType.Road && IsBlueprintTile(x, y))
                    builtBlueprintRoads++;
            }
        }

        bool inBuildPhase =
            canConsiderBuilding
            && builtBlueprintRoads < BuildGoalTiles
            && gameState.PlayerHunger < buildSafeHunger;

        Span<int> dist = stackalloc int[TileCount];
        Span<int> steps = stackalloc int[TileCount];
        Span<int> offRoadSteps = stackalloc int[TileCount];
        Span<int> firstMove = stackalloc int[TileCount];
        ComputePaths(gameState, playerX, playerY, dist, steps, offRoadSteps, firstMove);

        int bestFoodIndex = -1;
        int bestFoodDist = Inf;
        int bestFoodSteps = Inf;
        int bestFoodMetric = Inf;

        for (int i = 0; i < foodCount; i++)
        {
            int idx = foodIndices[i];
            int d = dist[idx];
            if (d >= Inf)
                continue;

            int s = steps[idx];
            int offRoad = offRoadSteps[idx];
            int metric = (d * 4) + (offRoad * 3) + s;
            if (d < bestFoodDist
                || (d == bestFoodDist && offRoad < offRoadSteps[bestFoodIndex < 0 ? idx : bestFoodIndex])
                || (d == bestFoodDist && offRoad == offRoadSteps[bestFoodIndex < 0 ? idx : bestFoodIndex] && s < bestFoodSteps)
                || (d == bestFoodDist && offRoad == offRoadSteps[bestFoodIndex < 0 ? idx : bestFoodIndex] && s == bestFoodSteps && metric < bestFoodMetric))
            {
                bestFoodMetric = metric;
                bestFoodDist = d;
                bestFoodSteps = s;
                bestFoodIndex = idx;
            }
        }

        bool shouldChaseFood = false;
        if (bestFoodIndex >= 0)
        {
            int chaseDistance = 5;
            if (foodCount >= 7)
                chaseDistance = 6;
            else if (foodCount >= 5)
                chaseDistance = 5;

            bool urgent = gameState.PlayerHunger >= UrgentHunger;
            int buildNearFoodCost = gameState.PlayerHunger >= 16 ? 6 : 4;
            bool nearFood = bestFoodDist <= (inBuildPhase && !urgent ? buildNearFoodCost : (chaseDistance * 2));
            bool sparseLateGame = !inBuildPhase && foodCount <= 2 && gameState.PlayerHunger >= 16 && bestFoodMetric <= 28;
            shouldChaseFood = urgent || nearFood || sparseLateGame;
        }

        if (shouldChaseFood)
        {
            if (canConsiderBuilding
                && isCurrentBlueprint
                && !currentIsRoad
                && bestFoodDist < Inf
                && ShouldPaveBeforeFood(gameState.PlayerHunger, bestFoodDist))
            {
                MoveType paveNow = AvoidPaveOverFood(gameState, playerX, playerY, MoveType.Pave);
                CommitMove(gameState, DecisionMode.BuildPave, paveNow, inBuildPhase, builtBlueprintRoads, foodCount, bestFoodIndex >= 0, bestFoodDist, bestFoodSteps, bestFoodMetric);
                return;
            }

            int moveValue = firstMove[bestFoodIndex];
            MoveType foodMove = moveValue >= 0 ? (MoveType)moveValue : MoveType.Pave;
            foodMove = AvoidPaveOverFood(gameState, playerX, playerY, foodMove);
            CommitMove(gameState, DecisionMode.ChaseFood, foodMove, inBuildPhase, builtBlueprintRoads, foodCount, bestFoodIndex >= 0, bestFoodDist, bestFoodSteps, bestFoodMetric);
            return;
        }

        if (inBuildPhase)
        {
            if (isCurrentBlueprint && !currentIsRoad)
            {
                MoveType move = AvoidPaveOverFood(gameState, playerX, playerY, MoveType.Pave);
                CommitMove(gameState, DecisionMode.BuildPave, move, inBuildPhase, builtBlueprintRoads, foodCount, bestFoodIndex >= 0, bestFoodDist, bestFoodSteps, bestFoodMetric);
                return;
            }

            int bestBuildIndex = -1;
            int bestBuildMetric = Inf;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (!IsBlueprintTile(x, y))
                        continue;
                    if (gameState.GetTile(x, y) == TileType.Road)
                        continue;

                    int idx = ToIndex(x, y);
                    if (dist[idx] >= Inf)
                        continue;

                    int travelSteps = steps[idx];
                    if (travelSteps > MaxBuildTravelSteps)
                        continue;

                    int metric = (dist[idx] * 3) + (offRoadSteps[idx] * 4) + travelSteps;
                    if (metric < bestBuildMetric)
                    {
                        bestBuildMetric = metric;
                        bestBuildIndex = idx;
                    }
                }
            }

            if (bestBuildIndex >= 0)
            {
                int moveValue = firstMove[bestBuildIndex];
                MoveType buildMove = moveValue >= 0 ? (MoveType)moveValue : MoveType.Pave;
                buildMove = AvoidPaveOverFood(gameState, playerX, playerY, buildMove);
                CommitMove(gameState, DecisionMode.BuildMove, buildMove, inBuildPhase, builtBlueprintRoads, foodCount, bestFoodIndex >= 0, bestFoodDist, bestFoodSteps, bestFoodMetric);
                return;
            }
        }

        if (isCurrentBlueprint && !currentIsRoad)
        {
            MoveType paveMove = AvoidPaveOverFood(gameState, playerX, playerY, MoveType.Pave);
            CommitMove(gameState, DecisionMode.BuildPave, paveMove, inBuildPhase, builtBlueprintRoads, foodCount, bestFoodIndex >= 0, bestFoodDist, bestFoodSteps, bestFoodMetric);
            return;
        }

        if (isCurrentBlueprint)
        {
            MoveType idleBlueprint = AvoidPaveOverFood(gameState, playerX, playerY, MoveType.Pave);
            idleBlueprint = PreferRoadCenterMoveOverIdle(gameState, playerX, playerY, idleBlueprint);
            CommitMove(gameState, DecisionMode.IdlePave, idleBlueprint, inBuildPhase, builtBlueprintRoads, foodCount, bestFoodIndex >= 0, bestFoodDist, bestFoodSteps, bestFoodMetric);
            return;
        }

        int nearestBlueprint = FindNearestBlueprint(gameState, steps, requireUnpaved: false);
        if (inBuildPhase && nearestBlueprint >= 0 && steps[nearestBlueprint] <= 6)
        {
            int moveValue = firstMove[nearestBlueprint];
            if (moveValue >= 0)
            {
                CommitMove(gameState, DecisionMode.BuildMove, (MoveType)moveValue, inBuildPhase, builtBlueprintRoads, foodCount, bestFoodIndex >= 0, bestFoodDist, bestFoodSteps, bestFoodMetric);
                return;
            }
        }

        MoveType driftMove = ChooseRoadBiasedDrift(gameState, playerX, playerY);
        CommitMove(gameState, DecisionMode.IdlePave, driftMove, inBuildPhase, builtBlueprintRoads, foodCount, bestFoodIndex >= 0, bestFoodDist, bestFoodSteps, bestFoodMetric);
    }

    static void CommitMove(
        GameState gameState,
        DecisionMode mode,
        MoveType move,
        bool inBuildPhase,
        int builtBlueprintRoads,
        int foodCount,
        bool hasFoodTarget,
        int bestFoodDist,
        int bestFoodSteps,
        int bestFoodMetric)
    {
        LastTrace = new DecisionTrace(
            mode,
            move,
            gameState.PlayerHunger,
            inBuildPhase,
            builtBlueprintRoads,
            foodCount,
            hasFoodTarget,
            bestFoodDist >= Inf ? -1 : bestFoodDist,
            bestFoodSteps >= Inf ? -1 : bestFoodSteps,
            bestFoodMetric >= Inf ? -1 : bestFoodMetric);
        gameState.MakeMove(move);
    }

    static void ComputePaths(
        GameState state,
        int startX,
        int startY,
        Span<int> dist,
        Span<int> steps,
        Span<int> offRoadSteps,
        Span<int> firstMove)
    {
        Span<bool> visited = stackalloc bool[TileCount];
        int startIndex = ToIndex(startX, startY);

        for (int i = 0; i < TileCount; i++)
        {
            dist[i] = Inf;
            steps[i] = Inf;
            offRoadSteps[i] = Inf;
            firstMove[i] = -1;
            visited[i] = false;
        }

        dist[startIndex] = 0;
        steps[startIndex] = 0;
        offRoadSteps[startIndex] = 0;
        firstMove[startIndex] = (int)MoveType.Pave;

        for (int iter = 0; iter < TileCount; iter++)
        {
            int bestIndex = -1;
            int bestDist = Inf;
            int bestOffRoad = Inf;
            int bestSteps = Inf;

            for (int i = 0; i < TileCount; i++)
            {
                if (visited[i])
                    continue;
                if (dist[i] < bestDist
                    || (dist[i] == bestDist && offRoadSteps[i] < bestOffRoad)
                    || (dist[i] == bestDist && offRoadSteps[i] == bestOffRoad && steps[i] < bestSteps))
                {
                    bestDist = dist[i];
                    bestOffRoad = offRoadSteps[i];
                    bestSteps = steps[i];
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || bestDist >= Inf)
                break;

            visited[bestIndex] = true;

            int x = bestIndex % Width;
            int y = bestIndex / Width;

            TryRelax(state, startIndex, bestIndex, x, y + 1, MoveType.Up, dist, steps, offRoadSteps, firstMove, visited);
            TryRelax(state, startIndex, bestIndex, x, y - 1, MoveType.Down, dist, steps, offRoadSteps, firstMove, visited);
            TryRelax(state, startIndex, bestIndex, x - 1, y, MoveType.Left, dist, steps, offRoadSteps, firstMove, visited);
            TryRelax(state, startIndex, bestIndex, x + 1, y, MoveType.Right, dist, steps, offRoadSteps, firstMove, visited);
        }
    }    

    static void TryRelax(
        GameState state,
        int startIndex,
        int fromIndex,
        int toX,
        int toY,
        MoveType direction,
        Span<int> dist,
        Span<int> steps,
        Span<int> offRoadSteps,
        Span<int> firstMove,
        Span<bool> visited)
    {
        if ((uint)toX >= Width || (uint)toY >= Height)
            return;

        int toIndex = ToIndex(toX, toY);
        if (visited[toIndex])
            return;

        int cost = state.GetTile(toX, toY) == TileType.Road
            ? GameState.HungerCostRoadMove
            : GameState.HungerCostNormal;
        int offRoadIncrement = state.GetTile(toX, toY) == TileType.Road ? 0 : 1;
        int newDist = dist[fromIndex] + cost;
        int newSteps = steps[fromIndex] + 1;
        int newOffRoadSteps = offRoadSteps[fromIndex] + offRoadIncrement;

        if (newDist < dist[toIndex]
            || (newDist == dist[toIndex] && newOffRoadSteps < offRoadSteps[toIndex])
            || (newDist == dist[toIndex] && newOffRoadSteps == offRoadSteps[toIndex] && newSteps < steps[toIndex]))
        {
            dist[toIndex] = newDist;
            steps[toIndex] = newSteps;
            offRoadSteps[toIndex] = newOffRoadSteps;
            firstMove[toIndex] = fromIndex == startIndex
                ? (int)direction
                : firstMove[fromIndex];
        }
    }

    static int ToIndex(int x, int y) => y * Width + x;

    static int GetBuildSafeHunger(int moveCount)
    {
        if (moveCount > 100)
            return 4;
        if (moveCount > 80)
            return 10;
        if (moveCount > 50)
            return 12;
        if (moveCount > 30)
            return 14;
        return InitialBuildSafeHunger;
    }

    static bool ShouldPaveBeforeFood(int currentHunger, int bestFoodDist)
    {
        // One extra turn to pave should still leave buffer before starving while moving toward food.
        int projectedHunger = currentHunger + GameState.HungerCostRoadMove + bestFoodDist;
        return projectedHunger <= GameState.LoseAtHunger - 4;
    }

    static MoveType PreferRoadCenterMoveOverIdle(GameState gameState, int playerX, int playerY, MoveType idleMove)
    {
        if (idleMove != MoveType.Pave)
            return idleMove;
        if (gameState.GetTile(playerX, playerY) != TileType.Road)
            return idleMove;

        int centerX = Width / 2;
        int centerY = Height / 2;
        int currentCenterDistance = Math.Abs(playerX - centerX) + Math.Abs(playerY - centerY);

        MoveType[] candidates = [MoveType.Up, MoveType.Down, MoveType.Left, MoveType.Right];
        MoveType bestMove = MoveType.Pave;
        int bestDistance = currentCenterDistance;

        for (int i = 0; i < candidates.Length; i++)
        {
            MoveType move = candidates[i];
            int nx = playerX;
            int ny = playerY;
            switch (move)
            {
                case MoveType.Up:
                    ny = Math.Min(playerY + 1, Height - 1);
                    break;
                case MoveType.Down:
                    ny = Math.Max(playerY - 1, 0);
                    break;
                case MoveType.Left:
                    nx = Math.Max(playerX - 1, 0);
                    break;
                case MoveType.Right:
                    nx = Math.Min(playerX + 1, Width - 1);
                    break;
            }

            if (nx == playerX && ny == playerY)
                continue;
            if (gameState.GetTile(nx, ny) != TileType.Road)
                continue;

            int distance = Math.Abs(nx - centerX) + Math.Abs(ny - centerY);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestMove = move;
            }
        }

        return bestMove == MoveType.Pave ? idleMove : bestMove;
    }

    static int FindNearestBlueprint(GameState gameState, Span<int> steps, bool requireUnpaved)
    {
        int bestIndex = -1;
        int bestSteps = Inf;

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (!IsBlueprintTile(x, y))
                    continue;
                if (requireUnpaved && gameState.GetTile(x, y) == TileType.Road)
                    continue;

                int idx = ToIndex(x, y);
                int s = steps[idx];
                if (s < bestSteps)
                {
                    bestSteps = s;
                    bestIndex = idx;
                }
            }
        }

        return bestIndex;
    }

    static MoveType ChooseRoadBiasedDrift(GameState gameState, int playerX, int playerY)
    {
        MoveType[] candidates = [MoveType.Up, MoveType.Down, MoveType.Left, MoveType.Right];
        int centerX = Width / 2;
        int centerY = Height / 2;

        MoveType bestMove = MoveType.Up;
        int bestScore = int.MaxValue;

        for (int i = 0; i < candidates.Length; i++)
        {
            MoveType move = candidates[i];
            int nx = playerX;
            int ny = playerY;
            switch (move)
            {
                case MoveType.Up:
                    ny = Math.Min(playerY + 1, Height - 1);
                    break;
                case MoveType.Down:
                    ny = Math.Max(playerY - 1, 0);
                    break;
                case MoveType.Left:
                    nx = Math.Max(playerX - 1, 0);
                    break;
                case MoveType.Right:
                    nx = Math.Min(playerX + 1, Width - 1);
                    break;
            }

            int score = gameState.GetTile(nx, ny) == TileType.Road ? 0 : 2;
            score += Math.Abs(nx - centerX) + Math.Abs(ny - centerY);
            if (IsBlueprintTile(nx, ny) && gameState.GetTile(nx, ny) != TileType.Road)
                score -= 2;

            if (score < bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }

        return bestMove;
    }

    static MoveType AvoidPaveOverFood(GameState gameState, int playerX, int playerY, MoveType preferredMove)
    {
        if (preferredMove != MoveType.Pave)
            return preferredMove;
        if (gameState.GetTile(playerX, playerY) != TileType.Food)
            return preferredMove;

        MoveType[] candidates = [MoveType.Up, MoveType.Down, MoveType.Left, MoveType.Right];
        int centerX = Width / 2;
        int centerY = Height / 2;
        MoveType bestMove = MoveType.Up;
        int bestScore = int.MaxValue;

        for (int i = 0; i < candidates.Length; i++)
        {
            MoveType move = candidates[i];
            int nx = playerX;
            int ny = playerY;
            switch (move)
            {
                case MoveType.Up:
                    ny = Math.Min(playerY + 1, Height - 1);
                    break;
                case MoveType.Down:
                    ny = Math.Max(playerY - 1, 0);
                    break;
                case MoveType.Left:
                    nx = Math.Max(playerX - 1, 0);
                    break;
                case MoveType.Right:
                    nx = Math.Min(playerX + 1, Width - 1);
                    break;
            }

            if (nx == playerX && ny == playerY)
                return move;

            int score = gameState.GetTile(nx, ny) == TileType.Road ? 0 : 10;
            score += Math.Abs(nx - centerX) + Math.Abs(ny - centerY);

            if (score < bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }

        return bestMove;
    }

    static bool IsBlueprintTile(int x, int y)
    {
        if (x <= 0 || x >= Width - 1 || y <= 0 || y >= Height - 1)
            return false;
        return x == 2 || x == 5 || x == 8 || y == 2 || y == 5 || y == 8;
    }
}
