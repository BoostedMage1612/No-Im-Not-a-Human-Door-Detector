using System;
using System.Runtime.InteropServices;
using DoorDetector;
static class NativeHandleTests
{
    const string Native="GameAssembly.dll";
    [DllImport(Native)] static extern void fixture_reset();
    [DllImport(Native)] static extern int fixture_creates();
    [DllImport(Native)] static extern int fixture_reads();
    [DllImport(Native)] static extern int fixture_frees();
    [DllImport(Native)] static extern int fixture_bad_handles();
    static int checks;
    static void Check(bool ok,string name) { if(!ok) throw new Exception(name); checks++; Console.WriteLine("PASS "+name); }
    static int Main()
    {
        try
        {
            Check(IntPtr.Size==8,"test executes in a 64-bit process");
            fixture_reset();
            var obj=new IntPtr(0x000056789ABCDEF0L);
            var h=new Handle(obj);
            Check(h.Value==obj,"native handle above 4 GiB round-trips without truncation");
            Check(fixture_creates()==1 && fixture_reads()==1 && fixture_bad_handles()==0,"native creation and lookup receive the full handle");
            h.Dispose();
            Check(fixture_frees()==1 && fixture_bad_handles()==0,"native free receives the full pointer-sized handle");
            Check(h.Value==IntPtr.Zero && fixture_reads()==1,"disposed handle never calls native lookup");
            h.Dispose();
            Check(fixture_frees()==1,"repeated disposal does not double-free");
            fixture_reset();
            using(var empty=new Handle(IntPtr.Zero)) Check(empty.Value==IntPtr.Zero,"null object creates an empty handle");
            Check(fixture_creates()==0 && fixture_frees()==0 && fixture_reads()==0,"empty handle never reaches native API");
            for(int i=0;i<1000;i++) { using var repeat=new Handle(obj); CheckOnce(repeat.Value==obj); }
            Check(fixture_creates()==1000 && fixture_frees()==1000 && fixture_bad_handles()==0,"1000 overlay-like allocation/disposal cycles retain full-width handles");
            Console.WriteLine(checks+" native-boundary checks passed.");return 0;
        }
        catch(Exception e) { Console.Error.WriteLine("FAIL "+e.Message); return 1; }
    }
    static void CheckOnce(bool ok) { if(!ok) throw new Exception("Handle corrupted during repeated allocations"); }
}
