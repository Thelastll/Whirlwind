@echo off
setlocal

set DB=Data\data.db
set SQL=migration.sql

sqlite3.exe "%DB%" < "%SQL%"

echo Migration completed.
endlocal
exit /b