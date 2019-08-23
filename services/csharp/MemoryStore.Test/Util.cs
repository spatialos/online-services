using Moq;
using NUnit.Framework;
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

        // Conditions do not have Equals overriden. Performing #Equals(...) will return true iff they are the same 
        // object. By comparing their string representations, we reveal the actual contents of the condition and can
        // verify whether they impose the same condition or not.
        public static void AssertConditionsAreEqual(Condition expected, Condition received)
        {
            Assert.AreEqual(expected.ToString(), received.ToString());
        }
    }
}
