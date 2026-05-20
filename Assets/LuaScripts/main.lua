-- main.lua
-- Lua entry point. Executed automatically by LuaBootstrap.Initialize() via require.

GameMain = GameMain or {}

function GameMain:OnInit()
    print("[Lua] main.lua OnInit called")
end

function GameMain:OnGameStart()
    print("[Lua] main.lua OnGameStart called")
end

function GameMain:OnLuaUpdateComplete()
    print("[Lua] Hot update complete — please restart the app to apply Lua changes")
end

-- Auto-run on first load
GameMain:OnInit()
print("lua SDFGHJKL")