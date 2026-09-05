@echo off
setlocal enabledelayedexpansion

set DB=Data\data.db
set RESERV=Data\data_reserv.db
set SQL=migration.sql

echo === Migration Start ===
echo Main database: %DB%
echo Backup file: %RESERV%
echo.

if not exist "%RESERV%" (
    echo Backup not found. Creating data_reserv.db...

    copy "%DB%" "%RESERV%" >nul 2>&1

    if errorlevel 1 (
        echo.
        echo Failed to create backup. If you continue, data may be lost.
        set /p ANSWER="Continue without backup? (y/n): "

        if /i "!ANSWER!"=="n" (
            echo Operation cancelled by user.
            echo Press any key to exit...
            pause >nul
            exit /b
        )

        echo Continuing without backup...
    ) else (
        echo Backup created successfully.
    )
) else (
    echo Backup found. Restoring data.db from data_reserv.db...
    copy /y "%RESERV%" "%DB%" >nul 2>&1

    if errorlevel 1 (
        echo Failed to restore from backup.
        echo Migration cannot continue.
        echo Press any key to exit...
        pause >nul
        exit /b
    )

    echo Restore completed.
)

echo.
echo === Applying migration ===

sqlite3.exe "%DB%" < "%SQL%"
if errorlevel 1 (
    echo.
    echo !!! Migration error !!!
    echo The database may be partially modified.
    echo Press any key to exit...
    pause >nul
    exit /b 1
)

echo.
echo Migration completed successfully.

echo Removing backup file...
del "%RESERV%" >nul 2>&1

if errorlevel 1 (
    echo Failed to delete backup file.
    echo You may remove it manually: %RESERV%
) else (
    echo Backup file removed.
)

exit /b
