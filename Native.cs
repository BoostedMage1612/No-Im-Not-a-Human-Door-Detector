using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace DoorDetector;

internal static unsafe class N
{
    private const string Dll = "GameAssembly.dll";
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr il2cpp_domain_get();
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr il2cpp_domain_get_assemblies(IntPtr domain, out UIntPtr count);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr il2cpp_assembly_get_image(IntPtr assembly);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr il2cpp_image_get_name(IntPtr image);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr il2cpp_class_from_name(IntPtr image, [MarshalAs(UnmanagedType.LPUTF8Str)] string ns, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr il2cpp_class_get_parent(IntPtr klass);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr il2cpp_class_get_field_from_name(IntPtr klass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern void il2cpp_field_get_value(IntPtr obj, IntPtr field, void* value);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern void il2cpp_field_set_value(IntPtr obj, IntPtr field, void* value);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr il2cpp_class_get_methods(IntPtr klass, ref IntPtr iter);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr il2cpp_method_get_name(IntPtr method);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint il2cpp_method_get_param_count(IntPtr method);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr il2cpp_method_get_param(IntPtr method, uint index);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr il2cpp_type_get_name(IntPtr type);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern void il2cpp_free(IntPtr p);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr il2cpp_runtime_invoke(IntPtr method, IntPtr obj, IntPtr* args, out IntPtr exception);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr il2cpp_object_unbox(IntPtr obj);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr il2cpp_object_get_class(IntPtr obj);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr il2cpp_string_new([MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr il2cpp_string_chars(IntPtr s);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int il2cpp_string_length(IntPtr s);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr il2cpp_gchandle_new(IntPtr obj, byte pinned);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr il2cpp_gchandle_get_target(IntPtr handle);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern void il2cpp_gchandle_free(IntPtr handle);
    static readonly ConcurrentDictionary<string,IntPtr> classes = new();
    static readonly ConcurrentDictionary<(IntPtr,string),IntPtr> fields = new();
    static readonly ConcurrentDictionary<(IntPtr,string),IntPtr> methods = new();
    internal static IntPtr Required(IntPtr p) => p != IntPtr.Zero ? p : throw new InvalidOperationException("Missing native object.");
    internal static IntPtr Class(string image, string ns, string name)
    {
        string key = image + ":" + ns + "." + name;
        if (classes.TryGetValue(key, out var result)) return result;
        var arr = il2cpp_domain_get_assemblies(Required(il2cpp_domain_get()), out var count);
        for (int i=0; i < checked((int)count.ToUInt64()); i++)
        {
            var img = il2cpp_assembly_get_image(Marshal.ReadIntPtr(arr,i*IntPtr.Size));
            if (Marshal.PtrToStringUTF8(il2cpp_image_get_name(img)) != image) continue;
            result = il2cpp_class_from_name(img,ns,name);
            if (result != IntPtr.Zero) { classes.TryAdd(key,result); return result; }
        }
        throw new MissingMemberException(key);
    }
    static IntPtr Field(IntPtr obj, string name)
    {
        if(obj==IntPtr.Zero) throw new InvalidOperationException("Missing native object while resolving field '"+name+"'.");
        var klass = il2cpp_object_get_class(obj);
        var key = (klass,name);
        if (fields.TryGetValue(key,out var found)) return found;
        for (var c=klass; c!=IntPtr.Zero; c=il2cpp_class_get_parent(c))
        {
            found=il2cpp_class_get_field_from_name(c,name);
            if(found!=IntPtr.Zero) { fields.TryAdd(key,found); return found; }
        }
        throw new MissingFieldException(name);
    }
    internal static T Read<T>(IntPtr obj,string name) where T:unmanaged
    {
        T value=default; il2cpp_field_get_value(obj,Field(obj,name),&value); return value;
    }
    internal static void Write<T>(IntPtr obj,string name,T value) where T:unmanaged => il2cpp_field_set_value(obj,Field(obj,name),&value);
    internal static IntPtr Method(IntPtr klass,string name,int count, params string[] types)
    {
        string signature=name+"/"+count+"/"+string.Join(",",types);
        var key=(klass,signature);
        if(methods.TryGetValue(key,out var cached)) return cached;
        for(var c=klass;c!=IntPtr.Zero;c=il2cpp_class_get_parent(c))
        {
            var iterator=IntPtr.Zero; IntPtr candidate; IntPtr match=IntPtr.Zero;
            while((candidate=il2cpp_class_get_methods(c,ref iterator))!=IntPtr.Zero)
            {
                if(il2cpp_method_get_param_count(candidate)!=count || Marshal.PtrToStringUTF8(il2cpp_method_get_name(candidate))!=name) continue;
                bool same=true;
                for(uint i=0;i<types.Length;i++)
                {
                    var t=il2cpp_type_get_name(il2cpp_method_get_param(candidate,i));
                    try { if(Marshal.PtrToStringUTF8(t)!=types[i]) same=false; }
                    finally { il2cpp_free(t); }
                }
                if(!same) continue;
                if(match!=IntPtr.Zero) throw new AmbiguousMatchException(name);
                match=candidate;
            }
            if(match!=IntPtr.Zero) { methods.TryAdd(key,match); return match; }
        }
        throw new MissingMethodException(signature);
    }
    internal static IntPtr Invoke(IntPtr method,IntPtr obj,params IntPtr[] args)
    {
        fixed(IntPtr* a=args)
        {
            var value=il2cpp_runtime_invoke(Required(method),obj,a,out var ex);
            if(ex!=IntPtr.Zero) throw new InvalidOperationException("Game method reported an exception.");
            return value;
        }
    }
    internal static IntPtr Call(IntPtr obj,string name,params IntPtr[] args) => Invoke(Method(il2cpp_object_get_class(Required(obj)),name,args.Length),obj,args);
    internal static T Unbox<T>(IntPtr obj) where T:unmanaged => *(T*)il2cpp_object_unbox(Required(obj));
    internal static T Get<T>(IntPtr obj,string name) where T:unmanaged => Unbox<T>(Call(obj,name));
    internal static string String(IntPtr s) => s==IntPtr.Zero?"":Marshal.PtrToStringUni(il2cpp_string_chars(s),il2cpp_string_length(s))!;
    internal static string Json(IntPtr obj)
    {
        var klass=Class("Newtonsoft.Json.dll","Newtonsoft.Json","JsonConvert");
        return String(Invoke(Method(klass,"SerializeObject",1,"System.Object"),IntPtr.Zero,obj));
    }
    internal static int[] IntList(IntPtr list)
    {
        int count=Get<int>(list,"get_Count");
        if(count<0 || count>256) throw new InvalidOperationException("Unexpected guest count.");
        int[] result=new int[count];
        for(int i=0;i<count;i++) result[i]=Unbox<int>(Call(list,"get_Item",(IntPtr)(&i)));
        return result;
    }
    internal static bool SavedBool(IntPtr dict,int type)
    {
        byte value=0;
        if(Unbox<byte>(Call(dict,"TryGetValue",(IntPtr)(&type),(IntPtr)(&value)))==0)
            throw new InvalidOperationException("Admission did not save an identity.");
        return value!=0;
    }
    internal static IntPtr Guest(IntPtr manager,int type) => Call(manager,"GetCharacter",(IntPtr)(&type));
    // null means the presentation objects are still being prepared. It does
    // not invalidate the identity already assigned to this live door encounter.
    internal static bool? IsDoorVisible(IntPtr room,IntPtr character)
    {
        if(Read<byte>(room,"_isSomeoneKnockingNow")!=0) return true;
        // EntranceRoom._dialogManager is never assigned in this build. The
        // game itself uses ARoom.DialogManager (confirmed in OnDialogStarted).
        var dialog=Read<IntPtr>(room,"DialogManager");
        if(dialog==IntPtr.Zero) return null;
        var view=Read<IntPtr>(dialog,"_dialogView");
        if(view==IntPtr.Zero) return null;
        if(Read<byte>(view,"_isActive")==0) return false;
        var shown=Read<IntPtr>(view,"_currentCharacter");
        return shown==IntPtr.Zero?null:shown==character;
    }
    internal static void VerifyHandleRoundTrip()
    {
        var sample=Required(il2cpp_string_new("DoorDetector native handle self-check"));
        using var rooted=new Handle(sample);
        if(rooted.Value!=sample) throw new InvalidOperationException("Unity native object-handle round trip failed.");
    }
    internal static bool KeyDown(int key)
    {
        var k=Class("UnityEngine.InputLegacyModule.dll","UnityEngine","Input");
        return Unbox<byte>(Invoke(Method(k,"GetKeyDown",1,"UnityEngine.KeyCode"),IntPtr.Zero,(IntPtr)(&key)))!=0;
    }
    [StructLayout(LayoutKind.Sequential)] struct Rect { public float x,y,w,h; }
    [StructLayout(LayoutKind.Sequential)] struct Matrix
    {
        public float a,b,c,d,e,f,g,h,i,j,k,l,m,n,o,p;
    }
    internal static void Draw(string text)
    {
        const float panelWidth=310, panelHeight=64, margin=12;
        var screen=Class("UnityEngine.CoreModule.dll","UnityEngine","Screen");
        int screenWidth=Unbox<int>(Invoke(Method(screen,"get_width",0),IntPtr.Zero));
        // A minimized/transitioning window can temporarily have no drawable area.
        if(screenWidth<=0) return;
        float uiScale=Math.Min(1.65f,screenWidth/(panelWidth+2*margin));
        var gui=Class("UnityEngine.IMGUIModule.dll","UnityEngine","GUI");
        var get=Method(gui,"get_matrix",0); var set=Method(gui,"set_matrix",1);
        var old=Unbox<Matrix>(Invoke(get,IntPtr.Zero));
        var scale=new Matrix{a=uiScale,f=uiScale,k=1,p=1};
        // Default IMGUI style is retained; scale makes the stripped game's built-in font readable.
        // Re-read the width each draw so the right anchor follows resolution/window changes.
        var rect=new Rect{x=screenWidth/uiScale-panelWidth-margin,y=margin,w=panelWidth,h=panelHeight};
        using var label=new Handle(il2cpp_string_new(text));
        try
        {
            Invoke(set,IntPtr.Zero,(IntPtr)(&scale));
            Invoke(Method(gui,"Box",2,"UnityEngine.Rect","System.String"),IntPtr.Zero,(IntPtr)(&rect),label.Value);
        }
        finally { Invoke(set,IntPtr.Zero,(IntPtr)(&old)); }
    }
}
internal sealed class Handle:IDisposable
{
    // Unity 6 uses a pointer-sized Il2CppGCHandle, not the legacy uint32 handle.
    IntPtr handle;
    internal Handle(IntPtr obj)
    {
        if(obj!=IntPtr.Zero) handle=N.Required(N.il2cpp_gchandle_new(obj,0));
    }
    internal IntPtr Value => handle==IntPtr.Zero?IntPtr.Zero:N.il2cpp_gchandle_get_target(handle);
    public void Dispose()
    {
        if(handle==IntPtr.Zero) return;
        var releasing=handle; handle=IntPtr.Zero;
        N.il2cpp_gchandle_free(releasing);
    }
}
internal sealed class AmbiguousMatchException:Exception { public AmbiguousMatchException(string s):base(s){} }
