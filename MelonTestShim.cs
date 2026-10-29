using System;
using System.Collections.Generic;
namespace MelonLoader
{
    [AttributeUsage(AttributeTargets.Assembly)] public sealed class MelonInfoAttribute:Attribute
    { public MelonInfoAttribute(Type t,string name,string version,string author) {} }
    public abstract class MelonMod
    {
        public virtual void OnInitializeMelon() {} public virtual void OnUpdate() {} public virtual void OnGUI() {}
        public virtual void OnSceneWasUnloaded(int index,string name) {} public virtual void OnDeinitializeMelon() {}
    }
    public static class MelonLogger
    {
        public static readonly List<string> Lines=new();
        public static void Msg(string text) { Lines.Add(text); }
        public static void Error(string text) { Lines.Add("ERROR "+text); }
    }
}
namespace MelonLoader.Utils
{ public static class MelonEnvironment { public static string GameRootDirectory=>AppContext.BaseDirectory; } }
namespace MelonLoader.NativeUtils
{
    public class NativeHook<T> where T:Delegate
    {
        public NativeHook(IntPtr target,IntPtr detour) {}
        public T Trampoline { get; set; }=null!;
        public void Attach() {} public void Detach() {}
    }
}
