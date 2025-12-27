@echo off
echo ==========================================
echo 1. SBORKA PROEKTA (DOCKER)
echo ==========================================
docker-compose build
IF %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Docker build failed!
    echo Skipping docker build...
)

echo.
echo ==========================================
echo 2. ZAPUSK TESTOV (UNIT & INTEGRATION)
echo ==========================================
cd backend.Tests
dotnet test
IF %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Tests failed!
    pause
    exit /b %ERRORLEVEL%
)
cd ..

echo.
echo ==========================================
echo 3. ZAPUSK PRILOZHENIYA
echo ==========================================
echo Starting containers...
docker-compose up -d

echo.
echo ==========================================
echo USPESHNO!
echo Frontend: http://localhost:3000
echo Backend:  http://localhost:5283
echo ==========================================
pause