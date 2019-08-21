using Moq;
using StackExchange.Redis;

namespace MemoryStore.Test
{
    public class Util
    {
        public static LoadedLuaScript CreateMockLoadedLuaScript()
        {
            var server = new Mock<IServer>();
            return CreateMockLoadedLuaScript(server);
        }

        public static LoadedLuaScript CreateMockLoadedLuaScript(Mock<IServer> server)
        {
            var script = LuaScript.Prepare("");
            server.Setup(srv => srv.ScriptLoad(script.ExecutableScript, CommandFlags.None)).Returns(new byte[8]);
            return script.Load(server.Object);
        }
    }
}
