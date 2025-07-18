﻿/*
 * ==============================================================
 * Raycasting Engine using Raylib.
 * ==============================================================
 */

using System.Numerics;
using Raylib_cs;
using System.Runtime.InteropServices;

namespace Thurs;

internal class Program
{
    private const int ScreenWidth = 1280;
    private const int ScreenHeight = 720;
    private const int AngleSteps = 3600;

    private const int MapSize = 32;
    private static readonly int[,] Map = new int[MapSize, MapSize];
    private static readonly Random random = new();

    // =========================
    // Look up tables
    // =========================
    private static readonly double[] SinTable = new double[AngleSteps];
    private static readonly double[] CosTable = new double[AngleSteps];
    private static readonly (double rayDirX, double rayDirY)[] RayTable = new (double, double)[ScreenWidth];
    private static readonly double[] WallDistances = new double[ScreenWidth / 2];

    // Floor, ceiling, and texture rendering
    private static readonly Color[] FloorBuffer = new Color[ScreenWidth * ScreenHeight];
    private static Color[]? FlatFloorTexture;
    private static readonly Color[]? CeilingTexture;
    private static int CeilingWidth;
    private static int CeilingHeight;
    //=======================================
    private static readonly double[] TextureXOffsets = new double[ScreenWidth];
    private static readonly double[] DeltaDistXs = new double[ScreenWidth];
    private static readonly double[] DeltaDistYs = new double[ScreenWidth];
    private static Texture2D BgTexture;
    private static Texture2D GrassTexture;
    private static readonly float[] RowDistances = new float[ScreenHeight];
    private static readonly float[] FloorStepXs = new float[ScreenHeight];
    private static readonly float[] FloorStepYs = new float[ScreenHeight];
    //========================================
    private static double LastPlayerX = -1, LastPlayerY = -1;
    private static int LastAngleIdx = -1;

    private struct Enemy
    {
        public double X, Y;
        public double Speed;
        public bool IsAlive;
    }

    private struct Projectile
    {
        public double X, Y;
        public double DirX, DirY;
        public double Speed;
        public float Life;
    }

    private struct Grass
    {
        public double X, Y;
    }
    private static List<Grass> grasses = new List<Grass>();

    private static void InitTables(double dirX, double dirY, double planeX, double planeY)
    {
        // Sine and Cosine tables
        for (int i = 0; i < AngleSteps; i++)
        {
            double angle = i * Math.PI * 2 / AngleSteps;
            SinTable[i] = Math.Sin(angle);
            CosTable[i] = Math.Cos(angle);
        }

        // Ray direction table
        for (int x = 0; x < ScreenWidth; x++)
        {
            double cameraX = 2.0 * x / ScreenWidth - 1.0;
            RayTable[x] = (dirX + planeX * cameraX, dirY + planeY * cameraX);
            double rayDirX = RayTable[x].rayDirX;
            double rayDirY = RayTable[x].rayDirY;
            double angle = Math.Atan2(rayDirY, rayDirX);
            if (angle < 0) angle += 2 * Math.PI;
            TextureXOffsets[x] = (angle / (2 * Math.PI)) * CeilingWidth;
        }

        // Offset table
        for (int x = 0; x < ScreenWidth; x++)
        {
            double rayDirX = RayTable[x].rayDirX;
            double rayDirY = RayTable[x].rayDirY;
            DeltaDistXs[x] = Math.Abs(1 / rayDirX);
            DeltaDistYs[x] = Math.Abs(1 / rayDirY);
        }

        // Precompute row distances and steps to reduce per-frame computation
        double horizon = ScreenHeight / 2.0;
        for (int y = 0; y < ScreenHeight; y++)
        {
            RowDistances[y] = y < horizon ? 0 : (float)(ScreenHeight / (2.0 * y - ScreenHeight));
            if (y >= horizon)
            {
                float rowDistance = RowDistances[y];
                double stepFactor = rowDistance * 2.0 / ScreenWidth;
                FloorStepXs[y] = (float)(stepFactor * planeX);
                FloorStepYs[y] = (float)(stepFactor * planeY);
            }
        }
    }

    /*
     * =========================
     *  Random map generation
     * =========================
     */
    private static void GenerateRandomMap()
    {
        while (true)
        {
            const double wallProbability = 0.5;       // Percent chance cell starts as wall
            const int smoothIter = 3;                // Number of smoothing passes
            const int wallThreshold = 4;            // Becomes wall if more than 4 neighbors are walls, etc..
            const double minEmptySpaceRatio = 0.3; // 30% empty space

            // Init interior
            for (int y = 1; y < MapSize - 1; y++)
                for (int x = 1; x < MapSize - 1; x++)
                    Map[y, x] = random.NextDouble() < wallProbability ? 1 : 0;

            // Smooth map with cellular automata
            for (int iteration = 0; iteration < smoothIter; iteration++)
            {
                int[,] nextMap = new int[MapSize, MapSize];
                for (int y = 1; y < MapSize - 1; y++)
                    for (int x = 1; x < MapSize - 1; x++)
                    {
                        int wallCount = GetNeighborWallCount(x, y);
                        nextMap[y, x] = wallCount > wallThreshold ? 1 : 0;
                    }

                // Update with smoothed version
                for (int y = 1; y < MapSize - 1; y++)
                    for (int x = 1; x < MapSize - 1; x++)
                        Map[y, x] = nextMap[y, x];
            }

            // Enclose map with walls
            for (int x = 0; x < MapSize; x++)
            {
                Map[0, x] = 1; // Top edge
                Map[MapSize - 1, x] = 1; // Bottom edge
            }

            for (int y = 0; y < MapSize; y++)
            {
                Map[y, 0] = 1; // Left edge
                Map[y, MapSize - 1] = 1; // Right Edge
            }

            // Ensure ample empty space
            int emptyCount = 0;
            for (int y = 0; y < MapSize; y++)
                for (int x = 0; x < MapSize; x++)
                    if (Map[y, x] == 0)
                        emptyCount++;

            double emptyRatio = (double)emptyCount / (MapSize * MapSize);
            if (emptyRatio < minEmptySpaceRatio) continue;

            break;
        }
    }

    private static int GetNeighborWallCount(int x, int y)
    {
        int count = 0;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx;
                int ny = y + dy;
                if (nx is >= 0 and < MapSize && ny >= 0 && ny < MapSize && Map[ny, nx] == 1)
                    count++;
            }

        return count;
    }

    private static void Main(string[] args)
    {
        GenerateRandomMap();

        // Billboard population
        for (int y = 0; y < MapSize; y++)
            for (int x = 0; x < MapSize; x++)
            {
                if (Map[y, x] == 0 && random.NextDouble() < 0.1)
                {
                    double posX = x + random.NextDouble();
                    double posY = y + random.NextDouble();
                    grasses.Add(new Grass { X = posX, Y = posY });
                }
            }

        // Player Variables
        double playerX = 8.5;
        double playerY = 8.5;
        double playerAngle = 0.0;
        const double moveSpeed = 0.07;
        const double rotSpeed = 0.003;
        const double sprintMult = 1.7;

        // Enemy variables
        List<Enemy> enemies = new();
        List<(int x, int y)> openCells = new();
        for (int y = 1; y < MapSize - 1; y++)
            for (int x = 1; x < MapSize - 1; x++)
                if (Map[y, x] == 0)
                    openCells.Add((x, y));

        // Shuffle and pick 20 unique open cells
        for (int i = 0; i < 5 && openCells.Count > 0; i++)
        {
            int idx = random.Next(openCells.Count);
            (int x, int y) = openCells[idx];
            openCells.RemoveAt(idx);
            enemies.Add(new Enemy
            {
                X = x + 0.5,
                Y = y + 0.5,
                Speed = 0.03,
                IsAlive = true
            });
        }

        List<Projectile> projectiles = new();
        float shootCooldown = 0f;
        const float shootInterval = 1.0f;

        // Sine and Cosine tables
        for (int i = 0; i < AngleSteps; i++)
        {
            double angle = i * Math.PI * 2 / AngleSteps;
            SinTable[i] = Math.Sin(angle);
            CosTable[i] = Math.Cos(angle);
        }

        // Direction and plane vectors
        double dirX = CosTable[0];
        double dirY = SinTable[0];
        double planeX = -dirY * 0.66;
        double planeY = dirX * 0.66;

        // ===================
        // Window init and texturing
        // ===================
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "~THURS~");
        Raylib.SetTargetFPS(60);
        Raylib.DisableCursor();

        Texture2D wallTexture = Raylib.LoadTexture(@"C:\Users\Mikey\source\repos\Thurs\Assets\dampFloor.png");
        Raylib.SetTextureFilter(wallTexture, TextureFilter.Bilinear);

        Image floorImage = Raylib.LoadImage(@"C:\Users\Mikey\source\repos\Thurs\Assets\grass.png");
        int textureWidth = floorImage.Width;
        int textureHeight = floorImage.Height;

        FlatFloorTexture = new Color[textureWidth * textureHeight];
        Span<Color> floorTextureSpan = FlatFloorTexture;
        for (int y = 0; y < textureHeight; y++)
            for (int x = 0; x < textureWidth; x++)
                floorTextureSpan[y * textureWidth + x] = Raylib.GetImageColor(floorImage, x, y);
        Raylib.UnloadImage(floorImage);

        Image bgImage = Raylib.GenImageColor(ScreenWidth, ScreenHeight, Color.Black);
        BgTexture = Raylib.LoadTextureFromImage(bgImage);
        Raylib.UnloadImage(bgImage);

        var ceilingImage = Raylib.LoadImage(@"C:\Users\Mikey\source\repos\Thurs\Assets\nightSky.png");
        CeilingWidth = ceilingImage.Width;
        CeilingHeight = ceilingImage.Height;
        Color[] ceilingTexture = new Color[CeilingWidth * CeilingHeight];
        Span<Color> ceilingTextureSpan = ceilingTexture;
        for (int y = 0; y < CeilingHeight; y++)
            for (int x = 0; x < CeilingWidth; x++)
                ceilingTextureSpan[y * CeilingWidth + x] = Raylib.GetImageColor(ceilingImage, x, y);
        Raylib.UnloadImage(ceilingImage);

        GrassTexture = Raylib.LoadTexture(@"C:\Users\Mikey\source\repos\Thurs\Assets\grassBillboard.png");
        Raylib.SetTextureFilter(GrassTexture, TextureFilter.Bilinear);

        InitTables(dirX, dirY, planeX, planeY);

        Color ambientColor = new(30, 30, 30, 255);
        Vector3 lightDir = Vector3.Normalize(new Vector3(0.5f, -1, 0.5f));

        Color fogColor = new(0, 0, 0, 255);
        float fogStart = 8.0f;
        float fogEnd = 20.0f;

        Color finalTint = Color.Black;



        // ===================
        // MAIN LOOP
        // ===================
        while (!Raylib.WindowShouldClose())
        {
            // Handle Movement
            double currentMoveSpeed = moveSpeed;
            if (Raylib.IsKeyDown(KeyboardKey.LeftShift)) currentMoveSpeed *= sprintMult;

            double moveX = 0.0;
            double moveY = 0.0;

            if (Raylib.IsKeyDown(KeyboardKey.W))
            {
                moveX += dirX * currentMoveSpeed;
                moveY += dirY * currentMoveSpeed;
            }

            if (Raylib.IsKeyDown(KeyboardKey.S))
            {
                moveX -= dirX * currentMoveSpeed;
                moveY -= dirY * currentMoveSpeed;
            }

            if (Raylib.IsKeyDown(KeyboardKey.A))
            {
                moveX += dirY * currentMoveSpeed;
                moveY -= dirX * currentMoveSpeed;
            }

            if (Raylib.IsKeyDown(KeyboardKey.D))
            {
                moveX -= dirY * currentMoveSpeed;
                moveY += dirX * currentMoveSpeed;
            }

            if (Raylib.IsKeyDown(KeyboardKey.M))
            {
                Raylib.EnableCursor();

            }

            // Collision detection
            const double collisionRadius = 0.1;
            double newX = playerX + moveX;
            double newY = playerY + moveY;

            static bool IsCellPassable(double x, double y)
            {
                int ix = (int)x;
                int iy = (int)y;
                return Map[iy, ix] == 0;
            }

            // X check
            bool xPassable =
                IsCellPassable(newX + collisionRadius, playerY) &&
                IsCellPassable(newX - collisionRadius, playerY) &&
                IsCellPassable(newX + collisionRadius, playerY + collisionRadius) &&
                IsCellPassable(newX + collisionRadius, playerY - collisionRadius) &&
                IsCellPassable(newX - collisionRadius, playerY + collisionRadius) &&
                IsCellPassable(newX - collisionRadius, playerY - collisionRadius);

            if (xPassable) playerX = newX;

            // Y check
            bool yPassable =
                IsCellPassable(playerX, newY + collisionRadius) &&
                IsCellPassable(playerX, newY - collisionRadius) &&
                IsCellPassable(playerX + collisionRadius, newY + collisionRadius) &&
                IsCellPassable(playerX - collisionRadius, newY + collisionRadius) &&
                IsCellPassable(playerX + collisionRadius, newY - collisionRadius) &&
                IsCellPassable(playerX - collisionRadius, newY - collisionRadius);

            if (yPassable) playerY = newY;

            Vector2 mouseDelta = Raylib.GetMouseDelta();
            playerAngle += mouseDelta.X * rotSpeed;
            if (Raylib.IsKeyDown(KeyboardKey.Right)) playerAngle += rotSpeed * 10;

            if (Raylib.IsKeyDown(KeyboardKey.Left)) playerAngle -= rotSpeed * 10;

            int angleIdx = (int)(playerAngle * AngleSteps / (2 * Math.PI) % AngleSteps);
            if (angleIdx < 0) angleIdx += AngleSteps;

            dirX = CosTable[angleIdx];
            dirY = SinTable[angleIdx];
            planeX = -dirY * 0.66;
            planeY = dirX * 0.66;

            InitTables(dirX, dirY, planeX, planeY);

            // =================
            // Shooting implementation
            // =================
            shootCooldown -= Raylib.GetFrameTime();
            if (Raylib.IsKeyDown(KeyboardKey.Space) && shootCooldown <= 0f)
            {
                projectiles.Add(new Projectile
                {
                    X = playerX,
                    Y = playerY,
                    DirX = dirX,
                    DirY = dirY,
                    Speed = 0.25,
                    Life = 2.0f
                });
                shootCooldown = shootInterval;
            }

            // Update projectile
            var projectilesSpan = CollectionsMarshal.AsSpan(projectiles);
            for (int i = projectilesSpan.Length - 1; i >= 0; i--)
            {
                ref Projectile p = ref projectilesSpan[i];
                p.X += p.DirX * p.Speed;
                p.Y += p.DirY * p.Speed;
                p.Life -= Raylib.GetFrameTime();

                // Clean up if out of bounds, wall is hit, or expired
                int px = (int)p.X;
                int py = (int)p.Y;
                if (p.Life <= 0 || px < 0 || py < 0 || px >= MapSize || py >= MapSize || Map[py, px] == 1)
                    projectiles.RemoveAt(i);
                continue;
            }

            // ========================
            // Enemy AI and Collision
            // ========================
            var enemiesSpan = CollectionsMarshal.AsSpan(enemies);
            for (int i = 0; i < enemiesSpan.Length; i++)
            {
                ref Enemy enemy = ref enemiesSpan[i];
                if (!enemy.IsAlive) continue;

                // Follow player
                double dx = playerX - enemy.X;
                double dy = playerY - enemy.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance > 0.5)
                {
                    double eMoveX = dx / distance * enemy.Speed;
                    double eMoveY = dy / distance * enemy.Speed;
                    // Allow movement only in open cell
                    if (Map[(int)(enemy.Y + eMoveY), (int)(enemy.X + eMoveX)] == 0)
                    {
                        enemy.X += eMoveX;
                        enemy.Y += eMoveY;
                    }
                }

                // Projectile Collision
                projectilesSpan = CollectionsMarshal.AsSpan(projectiles);
                for (int j = projectilesSpan.Length - 1; j >= 0; j--)
                {
                    Projectile p = projectiles[j];
                    double distToEnemy = Math.Sqrt(Math.Pow(enemy.X - p.X, 2) + Math.Pow(enemy.Y - p.Y, 2));
                    if (distToEnemy < 0.5)
                    {
                        enemy.IsAlive = false;
                        projectiles.RemoveAt(j);
                        break;
                    }
                }
            }

            if (playerX != LastPlayerX || playerY != LastPlayerY || angleIdx != LastAngleIdx)
            {
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Black);

                Span<Color> floorSpan = FloorBuffer;

                double horizon = ScreenHeight / 2.0;

                for (int y = 0; y < horizon; y++)
                {
                    double textureY = (y / horizon) * CeilingHeight;
                    textureY = Math.Clamp(textureY, 0, CeilingHeight - 1);
                    int texY = (int)textureY;

                    for (int x = 0; x < ScreenWidth; x++)
                    {
                        double textureX = TextureXOffsets[x];
                        textureX %= CeilingWidth;
                        if (textureX < 0) textureX += CeilingWidth;
                        int texX = (int)textureX;
                        if (ceilingTexture != null && texY >= 0 && texY < CeilingHeight && texX >= 0 && texX < CeilingHeight)
                        {
                            floorSpan[y * ScreenWidth + x] = ceilingTexture[texY * CeilingWidth + texX];
                        }
                    }
                }

                for (int y = (int)horizon; y < ScreenHeight; y++)
                {
                    float rowDistance = RowDistances[y];
                    float floorStepX = FloorStepXs[y];
                    float floorStepY = FloorStepYs[y];
                    double floorX = playerX + rowDistance * (dirX - planeX);
                    double floorY = playerY + rowDistance * (dirY - planeY);

                    for (int x = 0; x < ScreenWidth; x++)
                    {
                        int texX = (int)(floorX * textureWidth) % textureWidth;
                        texX = (texX + textureWidth) % textureWidth;
                        int texY = (int)(floorY * textureHeight) % textureHeight;
                        texY = (texY + textureHeight) % textureHeight;
                        floorSpan[y * ScreenWidth + x] = floorTextureSpan[texY * textureWidth + texX];
                        floorX += floorStepX;
                        floorY += floorStepY;
                    }
                }

                unsafe
                {
                    fixed (Color* ptr = FloorBuffer)
                    {
                        Raylib.UpdateTexture(BgTexture, ptr);
                    }
                }
                LastPlayerX = playerX;
                LastPlayerY = playerY;
                LastAngleIdx = angleIdx;
            }
            Raylib.DrawTexture(BgTexture, 0, 0, Color.White);

            //var floorColor = new Color(25, 25, 25, 255);
            //const double sigma = 8.0;

            double invDet = 1.0 / (planeX * dirY - planeY * dirX);

            // Raycasting loop
            for (int x = 0; x < ScreenWidth; x += 2)
            {
                double rayDirX = RayTable[x].rayDirX;
                double rayDirY = RayTable[x].rayDirY;

                int mapX = (int)playerX;
                int mapY = (int)playerY;
                double sideDistX, sideDistY;
                double deltaDistX = DeltaDistXs[x];
                double deltaDistY = DeltaDistYs[x];
                int stepX, stepY;
                bool hit = false;
                int side = 0;

                if (rayDirX < 0)
                {
                    stepX = -1;
                    sideDistX = (playerX - mapX) * deltaDistX;
                } else
                {
                    stepX = 1;
                    sideDistX = (mapX + 1.0 - playerX) * deltaDistX;
                }

                if (rayDirY < 0)
                {
                    stepY = -1;
                    sideDistY = (playerY - mapY) * deltaDistY;
                } else
                {
                    stepY = 1;
                    sideDistY = (mapY + 1.0 - playerY) * deltaDistY;
                }

                // DDA Loop
                while (!hit)
                {
                    if (sideDistX < sideDistY)
                    {
                        sideDistX += deltaDistX;
                        mapX += stepX;
                        side = 0;
                    } else
                    {
                        sideDistY += deltaDistY;
                        mapY += stepY;
                        side = 1;
                    }

                    if (Map[mapY, mapX] == 1) hit = true;
                }

                // Fish eye correction
                double t = side == 0
                    ? ((stepX == 1 ? mapX : mapX + 1) - playerX) / rayDirX
                    : ((stepY == 1 ? mapY : mapY + 1) - playerY) / rayDirY;

                double effectiveT = t - collisionRadius;
                if (effectiveT < 0.15) effectiveT = 0.15;

                double cosA = dirX * rayDirX + dirY * rayDirY;
                double perpT = effectiveT * cosA;
                if (perpT < 0.01) perpT = 0.01;

                int lineHeight = (int)(ScreenHeight / perpT);
                int drawStart = -lineHeight / 2 + ScreenHeight / 2;
                if (drawStart < 0) drawStart = 0;
                int drawEnd = lineHeight / 2 + ScreenHeight / 2;
                if (drawEnd >= ScreenHeight) drawEnd = ScreenHeight - 1;

                double wallX;
                if (side == 0)
                    wallX = playerY + t * rayDirY;
                else
                    wallX = playerX + t * rayDirX;
                wallX -= Math.Floor(wallX);

                int texWallX = (int)(wallX * wallTexture.Width);
                if ((side == 0 && rayDirX > 0) || (side == 1 && rayDirY < 0))
                    texWallX = wallTexture.Width - texWallX - 1;
                texWallX = Math.Clamp(texWallX, 0, wallTexture.Width - 1);
                WallDistances[x / 2] = t;

                // Dynamic lighting
                float lightIntensity = Vector3.Dot(lightDir, new Vector3(side == 0 ? 1 : 0, 0, side == 1 ? 1 : 0));
                lightIntensity = Math.Max(0, lightIntensity);

                finalTint.R = (byte)Math.Clamp(ambientColor.R + (int)(lightIntensity * 255), 0, 255);
                finalTint.G = (byte)Math.Clamp(ambientColor.G + (int)(lightIntensity * 255), 0, 255);
                finalTint.B = (byte)Math.Clamp(ambientColor.B + (int)(lightIntensity * 255), 0, 255);
                finalTint.A = 255;

                // Calculate and apply fog
                double fogFactor = Math.Clamp((effectiveT - fogStart) / (fogEnd - fogStart), 0.0f, 1.0f);
                finalTint.R = (byte)((1.0f - fogFactor) * finalTint.R + fogFactor * fogColor.R);
                finalTint.G = (byte)((1.0f - fogFactor) * finalTint.G + fogFactor * fogColor.G);
                finalTint.B = (byte)((1.0f - fogFactor) * finalTint.B + fogFactor * fogColor.B);

                Rectangle sourceRec = new(texWallX, 0, 1, wallTexture.Height);
                Rectangle destRec = new(x, drawStart, 2, drawEnd - drawStart);
                Raylib.DrawTexturePro(wallTexture, sourceRec, destRec, new Vector2(0, 0), 0, finalTint);
            }

            // Render Grass
            const double grassScale = 0.4;
            float aspectRatio = (float)GrassTexture.Width / GrassTexture.Height;
            Span<Grass> grassesSpan = CollectionsMarshal.AsSpan(grasses);
            for (int i = 0; i < grassesSpan.Length; i++)
            {
                ref Grass grass = ref grassesSpan[i];
                double dx = grass.X - playerX;
                double dy = grass.Y - playerY;
                double transformX = invDet * (dirY * dx - dirX * dy);
                double transformY = invDet * (-planeY * dx + planeX * dy);
                if (transformY > 0 && transformY < fogEnd)
                {
                    int screenX = (int)(ScreenWidth / 2.0 * (1 + transformX / transformY));
                    if (screenX >= 0 && screenX < ScreenWidth)
                    {
                        int rayIndex = screenX / 2;
                        if (transformY < WallDistances[rayIndex])
                        {
                            int grassHeight = (int)(ScreenHeight / transformY * grassScale);
                            int grassWidth = (int)(grassHeight * aspectRatio);
                            double yBase = ScreenHeight / 2.0 + ScreenHeight / (2.0 * transformY);
                            int yBaseClamped = (int)Math.Min(yBase, ScreenHeight - 1);
                            int screenY = yBaseClamped - grassHeight;

                            Rectangle sourceRec;
                            Rectangle destRec;
                            if (screenY < 0)
                            {
                                float clippedHeight = grassHeight + screenY;
                                float sourceYStart = (float)(-screenY) / grassHeight * GrassTexture.Height;
                                sourceRec = new Rectangle(0, sourceYStart, GrassTexture.Width, clippedHeight / grassHeight * GrassTexture.Height);
                                destRec = new Rectangle(screenX - grassWidth / 2, 0, grassWidth, clippedHeight);
                            } else
                            {
                                sourceRec = new Rectangle(0, 0, GrassTexture.Width, GrassTexture.Height);
                                destRec = new Rectangle(screenX - grassWidth / 2, screenY, grassWidth, grassHeight);
                            }

                            double fogFactor = Math.Clamp((transformY - fogStart) / (fogEnd - fogStart), 0.0, 1.0);
                            Color grassTint = new Color(
                                (byte)(255 * (1 - fogFactor)),
                                (byte)(255 * (1 - fogFactor)),
                                (byte)(255 * (1 - fogFactor)),
                                (byte)255
                            );
                            Raylib.DrawTexturePro(GrassTexture, sourceRec, destRec, new Vector2(0, 0), 0, grassTint); ;
                            //Raylib.DrawLine(screenX, yBaseClamped, screenX, yBaseClamped - 10, Color.Red);
                        }
                    }
                }
            }

            foreach (Enemy enemy in enemies.Where(e => e.IsAlive))
            {
                double dx = enemy.X - playerX;
                double dy = enemy.Y - playerY;
                double transformX = invDet * (dirY * dx - dirX * dy);
                double transformY = invDet * (-planeY * dx + planeX * dy);

                if (transformY > 0)
                { // Enemy in front of player
                    int screenX = (int)(640d * (1 + transformX / transformY));
                    if (screenX >= 0 && screenX < ScreenWidth)
                    {
                        int rayIndex = screenX / 2;
                        if (transformY < WallDistances[rayIndex])
                        {
                            int enemySize = (int)(ScreenHeight / transformY * 0.2);
                            if (enemySize < 5) enemySize = 5;
                            int screenY = ScreenHeight / 2 + enemySize / 2;
                            Raylib.DrawRectangle(screenX - enemySize / 2, screenY - enemySize / 2, enemySize, enemySize,
                                Color.Red);

                        }
                    }
                }

            }


            foreach (Projectile p in projectiles)
            {
                double dx = p.X - playerX;
                double dy = p.Y - playerY;

                double transformX = invDet * (dirY * dx - dirX * dy);
                double transformY = invDet * (-planeY * dx + planeX * dy);

                if (transformY > 0)
                {
                    int screenX = (int)(640d * (1 + transformX / transformY));
                    if (screenX is >= 0 and < ScreenWidth)
                    {
                        int screenY = ScreenHeight / 2;
                        float radius = (float)(ScreenHeight / transformY * 0.15f);
                        if (radius < 1) radius = 1;
                        Raylib.DrawCircle(screenX, screenY, radius, Color.Gold);
                    }
                }
            }

            Raylib.EndDrawing();
        }

        Raylib.UnloadTexture(wallTexture);
        Raylib.UnloadTexture(BgTexture);
        Raylib.UnloadTexture(GrassTexture);
        Raylib.CloseWindow();
    }

}