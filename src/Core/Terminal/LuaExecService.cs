using System;
using System.Threading.Tasks;
using Witch;
using XLua;

namespace WitchModMCP.Terminal
{
    public static class LuaExecService
    {
        public static object[] Execute(string code)
        {
            var luaEnv = ScriptExecutor.luaEnv;
            if (luaEnv == null)
                throw new InvalidOperationException("luaEnv is null");

            return luaEnv.DoString(code, "WitchModMCP", null);
        }
    }
}
