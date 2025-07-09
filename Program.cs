/*
 * ==============================================================
 * Raycasting Engine using Raylib.
 * ==============================================================
 */

using System.Numerics;
using Raylib_cs;

namespace Thurs;

internal class Program {
    private const int ScreenWidth = 1280;
    private const int ScreenHeight = 720;
    private const int AngleSteps = 3600;

    private const int MapSize = 32;
    private static readonly int[,] map = new int[MapSize, MapSize];
    private static readonly Random random = new();

    // Lookup tables
    private static readonly double[] SinTable = new double[AngleSteps];
    private static readonly double[] CosTable = new double[AngleSteps];
    private static readonly (double rayDirX, double rayDirY)[] RayTable = new (double, double)[ScreenWidth];
    private static Color[] floorBuffer = new Color[ScreenWidth * ScreenHeight];
    private static Color[] flatFloorTexture;
    private static Texture2D bgTexture;
    
    private static void InitTables(double dirX, double dirY, double planeX, double planeY) {
        // Sine and Cosine tables
        for (var i = 0; i < AngleSteps; i++) {
            var angle = i * Math.PI * 2 / AngleSteps;
            SinTable[i] = Math.Sin(angle);
            CosTable[i] = Math.Cos(angle);
        }

        // Ray direction table
        for (var x = 0; x < ScreenWidth; x++) {
            var cameraX = 2.0 * x / ScreenWidth - 1.0;
            RayTable[x] = (dirX + planeX * cameraX, dirY + planeY * cameraX);
        }
    }

    /*
     * =========================
     *  Random map generation
     * =========================
     */
    private static void GenerateRandomMap() {
        while (true) {
            const double wallProbability = 0.5; // Percent chance cell starts as wall
            const int smoothIter = 3; // Number of smoothing passes
            const int wallThreshold = 4; // Becomes wall if more than 4 neighbors are walls, etc..
            const double minEmptySpaceRatio = 0.3; // 30% empty space

            // Init interior
            for (var y = 1; y < MapSize - 1; y++)
            for (var x = 1; x < MapSize - 1; x++)
                map[y, x] = random.NextDouble() < wallProbability ? 1 : 0;

            // Smooth map with cellular automata
            for (var iteration = 0; iteration < smoothIter; iteration++) {
                var nextMap = new int[MapSize, MapSize];
                for (var y = 1; y < MapSize - 1; y++)
                for (var x = 1; x < MapSize - 1; x++) {
                    var wallCount = GetNeighborWallCount(x, y);
                    nextMap[y, x] = wallCount > wallThreshold ? 1 : 0;
                }

                // Update with smoothed version
                for (var y = 1; y < MapSize - 1; y++)
                for (var x = 1; x < MapSize - 1; x++)
                    map[y, x] = nextMap[y, x];
            }

            // Enclose map with walls
            for (var x = 0; x < MapSize; x++) {
                map[0, x] = 1; // Top edge
                map[MapSize - 1, x] = 1; // Bottom edge
            }

            for (var y = 0; y < MapSize; y++) {
                map[y, 0] = 1; // Left edge
                map[y, MapSize - 1] = 1; // Right Edge
            }

            // Ensure ample empty space
            var emptyCount = 0;
            for (var y = 0; y < MapSize; y++)
            for (var x = 0; x < MapSize; x++)
                if (map[y, x] == 0)
                    emptyCount++;

            var emptyRatio = (double)emptyCount / (MapSize * MapSize);
            if (emptyRatio < minEmptySpaceRatio) continue;

            break;
        }
    }

    private static int GetNeighborWallCount(int x, int y) {
        var count = 0;
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++) {
            if (dx == 0 && dy == 0) continue;
            var nx = x + dx;
            var ny = y + dy;
            if (nx is >= 0 and < MapSize && ny >= 0 && ny < MapSize && map[ny, nx] == 1)
                count++;
        }

        return count;
    }

    private struct Enemy {
        public double X, Y;
        public double Speed;
        public bool IsAlive;
    }
    private struct Projectile {
        public double X, Y;
        public double DirX, DirY;
        public double Speed;
        public float Life;
    }
    
    private static void Main(string[] args) {
        GenerateRandomMap();

        // Player Variables
        var playerX = 8.5;
        var playerY = 8.5;
        var playerAngle = 0.0;
        const double moveSpeed = 0.1;
        const double rotSpeed = 0.003;
        const double sprintMult = 2.0;

        // Enemy variables
        var enemies = new List<Enemy>();
        var openCells = new List<(int x, int y)>();
        for (var y = 1; y < MapSize - 1; y++)
            for (var x = 1; x < MapSize - 1; x++)
                if (map[y, x] == 0)
                    openCells.Add((x, y));

        // Shuffle and pick 20 unique open cells
        for (var i = 0; i < 20 && openCells.Count > 0; i++) {
            var idx = random.Next(openCells.Count);
            var (x, y) = openCells[idx];
            openCells.RemoveAt(idx);
            enemies.Add(new Enemy {
                X = x + 0.5,
                Y = y + 0.5,
                Speed = 0.03,
                IsAlive = true
            });
        }

        var projectiles = new List<Projectile>();
        var shootCooldown = 0f;
        const float shootInterval = 1.0f;

        // Sine and Cosine tables
        for (var i = 0; i < AngleSteps; i++) {
            var angle = i * Math.PI * 2 / AngleSteps;
            SinTable[i] = Math.Sin(angle);
            CosTable[i] = Math.Cos(angle);
        }

        // Direction and plane vectors
        var dirX = CosTable[0];
        var dirY = SinTable[0];
        var planeX = -dirY * 0.66;
        var planeY = dirX * 0.66;

        Raylib.InitWindow(ScreenWidth, ScreenHeight, "~THURS~");
        Raylib.SetTargetFPS(60);
        Raylib.DisableCursor();

        var wallTexture = Raylib.LoadTexture(@"C:\Users\Mikey\source\repos\Thurs\mossy.png");
        Raylib.SetTextureFilter(wallTexture, TextureFilter.Bilinear);

        var floorImage = Raylib.LoadImage(@"C:\Users\Mikey\source\repos\Thurs\dampFloor.png");
        var textureWidth = floorImage.Width;
        var textureHeight = floorImage.Height;
        flatFloorTexture = new Color[textureWidth * textureHeight];
        for (var y = 0; y < textureHeight; y++)
        for (var x = 0; x < textureWidth; x++)
            flatFloorTexture[y * textureWidth + x] = Raylib.GetImageColor(floorImage, x, y); 
        Raylib.UnloadImage(floorImage);

        var bgImage = Raylib.GenImageColor(ScreenWidth, ScreenHeight, Color.Black);
        bgTexture = Raylib.LoadTextureFromImage(bgImage);
        Raylib.UnloadImage(bgImage);
        
        InitTables(dirX, dirY, planeX, planeY);

        while (!Raylib.WindowShouldClose()) {
            // Handle Movement
            var currentMoveSpeed = moveSpeed;
            if (Raylib.IsKeyDown(KeyboardKey.LeftShift)) currentMoveSpeed *= sprintMult;

            var moveX = 0.0;
            var moveY = 0.0;

            if (Raylib.IsKeyDown(KeyboardKey.W)) {
                moveX += dirX * currentMoveSpeed;
                moveY += dirY * currentMoveSpeed;
            }

            if (Raylib.IsKeyDown(KeyboardKey.S)) {
                moveX -= dirX * currentMoveSpeed;
                moveY -= dirY * currentMoveSpeed;
            }

            if (Raylib.IsKeyDown(KeyboardKey.A)) {
                moveX += dirY * currentMoveSpeed;
                moveY -= dirX * currentMoveSpeed;
            }

            if (Raylib.IsKeyDown(KeyboardKey.D)) {
                moveX -= dirY * currentMoveSpeed;
                moveY += dirX * currentMoveSpeed;
            }

            // Collision detection
            const double collisionRadius = 0.4;
            var newX = playerX + moveX;
            var newY = playerY + moveY;

            static bool IsCellPassable(double x, double y) {
                var ix = (int)x;
                var iy = (int)y;
                return map[iy, ix] == 0;
            }

            // X check
            var xPassable =
                IsCellPassable(newX + collisionRadius, playerY) &&
                IsCellPassable(newX - collisionRadius, playerY) &&
                IsCellPassable(newX + collisionRadius, playerY + collisionRadius) &&
                IsCellPassable(newX + collisionRadius, playerY - collisionRadius) &&
                IsCellPassable(newX - collisionRadius, playerY + collisionRadius) &&
                IsCellPassable(newX - collisionRadius, playerY - collisionRadius);

            if (xPassable) playerX = newX;

            // Y check
            var yPassable =
                IsCellPassable(playerX, newY + collisionRadius) &&
                IsCellPassable(playerX, newY - collisionRadius) &&
                IsCellPassable(playerX + collisionRadius, newY + collisionRadius) &&
                IsCellPassable(playerX - collisionRadius, newY + collisionRadius) &&
                IsCellPassable(playerX + collisionRadius, newY - collisionRadius) &&
                IsCellPassable(playerX - collisionRadius, newY - collisionRadius);

            if (yPassable) playerY = newY;

            var mouseDelta = Raylib.GetMouseDelta();
            playerAngle += mouseDelta.X * rotSpeed;
            if (Raylib.IsKeyDown(KeyboardKey.Right)) playerAngle += rotSpeed * 10;

            if (Raylib.IsKeyDown(KeyboardKey.Left)) playerAngle -= rotSpeed * 10;

            var angleIdx = (int)(playerAngle * AngleSteps / (2 * Math.PI) % AngleSteps);
            if (angleIdx < 0) angleIdx += AngleSteps;

            dirX = CosTable[angleIdx];
            dirY = SinTable[angleIdx];
            planeX = -dirY * 0.66;
            planeY = dirX * 0.66;

            // =================
            // Shooting implementation
            // =================
            shootCooldown -= Raylib.GetFrameTime();
            if (Raylib.IsKeyDown(KeyboardKey.Space) && shootCooldown <= 0f) {
                projectiles.Add(new Projectile {
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
            for (var i = projectiles.Count - 1; i >= 0; i--) {
                var p = projectiles[i];
                p.X += p.DirX * p.Speed;
                p.Y += p.DirY * p.Speed;
                p.Life -= Raylib.GetFrameTime();

                // Clean up if out of bounds, wall is hit, or expired
                var px = (int)p.X;
                var py = (int)p.Y;
                if (p.Life <= 0 || px < 0 || py < 0 || px >= MapSize || py >= MapSize || map[py, px] == 1) {
                    projectiles.RemoveAt(i);
                    continue;
                }

                projectiles[i] = p;
            }
            
            // ========================
            // Enemy AI and Collision
            // ========================
            for (var i = 0; i < enemies.Count; i++) {
                var enemy = enemies[i];
                if (!enemy.IsAlive) continue;
                
                // Follow player
                var dx = playerX - enemy.X;
                var dy = playerY - enemy.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance > 0.5) {
                    var eMoveX = dx / distance * enemy.Speed;
                    var eMoveY = dy / distance * enemy.Speed;
                    // Allow movement only in open cell
                    if (map[(int)(enemy.Y + eMoveY), (int)(enemy.X + eMoveX)] == 0) {
                        enemy.X += eMoveX;
                        enemy.Y += eMoveY;
                    }
                }

                // Projectile Collision
                for (var j = projectiles.Count - 1; j >= 0; j--) {
                    var p = projectiles[j];
                    var distToEnemy = Math.Sqrt(Math.Pow(enemy.X - p.X, 2) + Math.Pow(enemy.Y - p.Y, 2));
                    if (distToEnemy < 0.5) {
                        enemy.IsAlive = false;
                        projectiles.RemoveAt(j);
                        break;
                    }
                }

                enemies[i] = enemy;
            }

            InitTables(dirX, dirY, planeX, planeY);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            
            var ceilingColor = new Color(15, 15, 15, 255);

            var horizon = ScreenHeight / 2.0;
            for (var y = 0; y < ScreenHeight / 2; y++)
            for (var x = 0; x < ScreenWidth; x++)
                floorBuffer[y * ScreenWidth + x] = ceilingColor;

            for (var y = (int)horizon; y < ScreenHeight; y++) {
                var rowDistance = ScreenHeight / (2.0 * y - ScreenHeight);
                var stepFactor = rowDistance * 2.0 / ScreenWidth;
                var floorStepX = stepFactor * planeX;
                var floorStepY = stepFactor * planeY;
                var floorX = playerX + rowDistance * (dirX - planeX);
                var floorY = playerY + rowDistance * (dirY - planeY);
                
                for (var x = 0; x < ScreenWidth; x++) {
                    var texX = (int)(floorX * textureWidth) % textureWidth;
                    texX = (texX + textureWidth) % textureWidth;
                    var texY = (int)(floorY * textureHeight) % textureHeight;
                    texY = (texY + textureHeight) % textureHeight;
                    floorBuffer[y * ScreenWidth + x] = flatFloorTexture[texY * textureWidth + texX];
                    floorX += floorStepX;
                    floorY += floorStepY;
                }
            }

            unsafe {
                fixed (Color* ptr = floorBuffer) {
                    Raylib.UpdateTexture(bgTexture, (void*)ptr);
                }
            }
            Raylib.DrawTexture(bgTexture, 0, 0, Color.White);
            
            //var floorColor = new Color(25, 25, 25, 255);
            const double sigma = 8.0;

            var invDet = 1.0 / (planeX * dirY - planeY * dirX);

            // Raycasting loop
            for (var x = 0; x < ScreenWidth; x++) {
                var rayDirX = RayTable[x].rayDirX;
                var rayDirY = RayTable[x].rayDirY;

                var mapX = (int)playerX;
                var mapY = (int)playerY;
                double sideDistX, sideDistY;
                var deltaDistX = Math.Abs(1 / rayDirX);
                var deltaDistY = Math.Abs(1 / rayDirY);
                int stepX, stepY;
                var hit = false;
                var side = 0;

                if (rayDirX < 0) {
                    stepX = -1;
                    sideDistX = (playerX - mapX) * deltaDistX;
                } else {
                    stepX = 1;
                    sideDistX = (mapX + 1.0 - playerX) * deltaDistX;
                }

                if (rayDirY < 0) {
                    stepY = -1;
                    sideDistY = (playerY - mapY) * deltaDistY;
                } else {
                    stepY = 1;
                    sideDistY = (mapY + 1.0 - playerY) * deltaDistY;
                }

                // DDA Loop
                while (!hit) {
                    if (sideDistX < sideDistY) {
                        sideDistX += deltaDistX;
                        mapX += stepX;
                        side = 0;
                    } else {
                        sideDistY += deltaDistY;
                        mapY += stepY;
                        side = 1;
                    }

                    if (map[mapY, mapX] == 1) hit = true;
                }

                // Fish eye correction
                var t = side == 0
                    ? ((stepX == 1 ? mapX : mapX + 1) - playerX) / rayDirX
                    : ((stepY == 1 ? mapY : mapY + 1) - playerY) / rayDirY;

                var effectiveT = t - collisionRadius;
                if (effectiveT < 0.15) effectiveT = 0.15;

                var cosA = dirX * rayDirX + dirY * rayDirY;
                var perpT = effectiveT * cosA;
                if (perpT < 0.01) perpT = 0.01;

                var lineHeight = (int)(ScreenHeight / perpT);
                var drawStart = -lineHeight / 2 + ScreenHeight / 2;
                if (drawStart < 0) drawStart = 0;
                var drawEnd = lineHeight / 2 + ScreenHeight / 2;
                if (drawEnd >= ScreenHeight) drawEnd = ScreenHeight - 1;

                var wallX = side == 0 ? playerY + t * rayDirY : playerX + t * rayDirX;
                wallX -= Math.Floor(wallX);
                if (wallX < 0) wallX = 0;
                if (wallX >= 1) wallX = 0.9999;

                var texWallX = (int)(wallX * wallTexture.Width);
                texWallX = Math.Clamp(texWallX, 0, wallTexture.Width - 1);

                switch (side) {
                    case 0 when rayDirX > 0:
                    case 1 when rayDirY < 0:
                        texWallX = wallTexture.Width - texWallX - 1;
                        break;
                }
                
                // Dynamic lighting
                var brightness = Math.Exp(-t / sigma);
                var gray = (byte)(brightness * 255);
                var tint = new Color((int)gray, gray, gray, 255);

                var sourceRec = new Rectangle(texWallX, 0, 1, wallTexture.Height);
                var destRec = new Rectangle(x, drawStart, 1, drawEnd - drawStart);
                Raylib.DrawTexturePro(wallTexture, sourceRec, destRec, new Vector2(0, 0), 0, tint);

                /*double floorXWall, floorYWall;

                // 4 different wall sides
                if (side == 0 && rayDirX > 0) {
                    floorXWall = mapX;
                    floorYWall = mapY + wallX;
                } else if (side == 0 && rayDirX < 0) {
                    floorXWall = mapX + 1.0;
                    floorYWall = mapY + wallX;
                } else if (side == 1 && rayDirY > 0) {
                    floorXWall = mapX;
                    floorYWall = mapY;
                } else {
                    floorXWall = mapX;
                    floorYWall = mapY + 1.0;
                }

                var distWall = perpT;
                var distPlayer = 0.0;

                if (drawEnd < 0) drawEnd = ScreenHeight;    // Bottom pixel set to 0 */
                
            }

            foreach (var enemy in enemies.Where(e => e.IsAlive)) {
                var dx = enemy.X - playerX;
                var dy = enemy.Y - playerY;

                var transformX = invDet * (dirY * dx - dirX * dy);
                var transformY = invDet * (-planeY * dx + planeX * dy);

                if (transformY > 0) {
                    var enemySize = (int)(ScreenHeight / transformY * 0.2);
                    if (enemySize < 5) enemySize = 5;
                    var screenX = (int)(640d * (1 + transformX / transformY));
                    var screenY = ScreenHeight / 2 + enemySize / 2;

                    Raylib.DrawRectangle(screenX - enemySize / 2, screenY - enemySize / 2, enemySize, enemySize, Color.Red);
                }
            }

            foreach (var p in projectiles) {
                var dx = p.X - playerX;
                var dy = p.Y - playerY;

                var transformX = invDet * (dirY * dx - dirX * dy);
                var transformY = invDet * (-planeY * dx + planeX * dy);

                if (transformY > 0) {
                    var screenX = (int)(640d * (1 + transformX / transformY));
                    if (screenX is >= 0 and < ScreenWidth) {
                        var screenY = ScreenHeight / 2;
                        var radius = (float)(ScreenHeight / transformY * 0.25f);
                        if (radius < 1) radius = 1;
                        Raylib.DrawCircle(screenX, screenY, radius, Color.Gold);
                    }
                }
            }

            Raylib.EndDrawing();
        }

        Raylib.UnloadTexture(wallTexture);
        Raylib.UnloadTexture(bgTexture);
        Raylib.CloseWindow();
    }

}