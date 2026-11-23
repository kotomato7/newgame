@echo off
rem ==========================================================================
rem  Cleanup for the Unity port of Pygame_wAI
rem  Deletes files of the old Unity version that the ported game does not use.
rem  Git files (.git, .gitignore) and Unity caches (Library etc.) are not touched.
rem  Please close the Unity Editor before running this file (double-click it).
rem  This file deletes itself when it finishes.
rem ==========================================================================
cd /d "%~dp0"
if not exist "Assets\Scripts\Unity\GameRoot.cs" (
    echo This file must be in the QTE_RPG_2D folder. Nothing was deleted.
    pause
    exit /b 1
)
echo Deleting files of the old version...

rem --- old scripts ---
call :deldir  "Assets\Scripts\Battle"
call :delfile "Assets\Scripts\Battle.meta"
call :delfile "Assets\Scripts\BackToGamescene.cs"
call :delfile "Assets\Scripts\BackToGamescene.cs.meta"
call :delfile "Assets\Scripts\GamesceneManager.cs"
call :delfile "Assets\Scripts\GamesceneManager.cs.meta"
call :delfile "Assets\Scripts\MenuManager.cs"
call :delfile "Assets\Scripts\MenuManager.cs.meta"
call :delfile "Assets\Scripts\ScreenFlash.cs"
call :delfile "Assets\Scripts\ScreenFlash.cs.meta"
call :delfile "Assets\Scripts\ShieldController.cs"
call :delfile "Assets\Scripts\ShieldController.cs.meta"

rem --- old scenes ---
call :delfile "Assets\Scenes\BattleScene1.unity"
call :delfile "Assets\Scenes\BattleScene1.unity.meta"
call :delfile "Assets\Scenes\BattleScene2.unity"
call :delfile "Assets\Scenes\BattleScene2.unity.meta"
call :delfile "Assets\Scenes\EquipScene1.unity"
call :delfile "Assets\Scenes\EquipScene1.unity.meta"
call :delfile "Assets\Scenes\GameScene1.unity"
call :delfile "Assets\Scenes\GameScene1.unity.meta"
call :delfile "Assets\Scenes\MainMenu.unity"
call :delfile "Assets\Scenes\MainMenu.unity.meta"
call :delfile "Assets\Scenes\SkillScene1.unity"
call :delfile "Assets\Scenes\SkillScene1.unity.meta"

rem --- old asset folders (prefabs, materials/images, NotoSansJP font, TextMesh Pro) ---
call :deldir  "Assets\Prefabs"
call :delfile "Assets\Prefabs.meta"
call :deldir  "Assets\Material"
call :delfile "Assets\Material.meta"
call :deldir  "Assets\Fonts"
call :delfile "Assets\Fonts.meta"
call :deldir  "Assets\TextMesh Pro"
call :delfile "Assets\TextMesh Pro.meta"

rem --- macOS junk files and the old solution file ---
call :delfile ".DS_Store"
if exist "Assets\" del /s /q /f /a "Assets\.DS_Store" >nul 2>&1
call :delfile "newgame.slnx"

echo.
echo Done. Open the project in Unity and play Assets/Scenes/Main.unity.
pause
(goto) 2>nul & del "%~f0"

:deldir
if exist "%~1\" (
    rmdir /s /q "%~1"
    echo   deleted folder %~1
)
goto :eof

:delfile
if exist "%~1" (
    del /f /q /a "%~1"
    echo   deleted %~1
)
goto :eof
