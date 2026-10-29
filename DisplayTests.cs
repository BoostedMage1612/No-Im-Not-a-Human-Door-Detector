using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using DoorDetector;
using MelonLoader;
static class DisplayTests
{
    const string Dll="GameAssembly.dll";
    [DllImport(Dll)] static extern void fixture_setup(int day,int type,int knock,int dialog,int view,int active,int shown,int dayReady);
    [DllImport(Dll)] static extern IntPtr fixture_room();
    [DllImport(Dll)] static extern IntPtr fixture_guest();
    [DllImport(Dll)] static extern int fixture_legacy_reads();
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static readonly Type Pending=typeof(DetectorMod).GetNestedType("Pending",BindingFlags.NonPublic)!;
    static readonly string Folder=Path.Combine(Path.GetTempPath(),"DoorDisplayTests-"+Guid.NewGuid().ToString("N"));
    static int checks;
    static void Set(object obj,string field,object? value) => obj.GetType().GetField(field,Flags)!.SetValue(obj,value);
    static object? Get(object obj,string field) => obj.GetType().GetField(field,Flags)!.GetValue(obj);
    static void Check(bool value,string name) { if(!value) throw new Exception(name); checks++;Console.WriteLine("PASS "+name); }
    static string Status(DetectorMod mod) => (string)Get(mod,"status")!;
    static object Encounter(DetectorMod mod,int day,int type,string name,bool visitor)
    {
        (Get(mod,"pending") as IDisposable)?.Dispose();
        var p=Activator.CreateInstance(Pending,Flags,null,new object[]{fixture_room(),fixture_guest()},null)!;
        Set(p,"Type",type);Set(p,"Day",day);Set(p,"Name",name);Set(p,"Known",true);Set(p,"Visitor",visitor);
        Set(p,"Assigned",true);Set(p,"Why","Game status");Set(mod,"pending",p);Set(mod,"status",name+"\n"+(visitor?"VISITOR":"HUMAN"));
        return p;
    }
    static void Poll(DetectorMod mod) { Set(mod,"nextPoll",DateTime.MinValue);mod.OnUpdate(); }
    static int Main()
    {
        Directory.CreateDirectory(Folder);
        var mod=new DetectorMod();Set(mod,"ready",true);Set(mod,"folder",Folder);Set(mod,"firstUpdate",false);
        try
        {
            // Actual production OnUpdate runs against a native API fixture.
            // This initial case fails with the unchanged 0.1.1 production source.
            var p=Encounter(mod,0,3,"Neighbour",false);
            fixture_setup(0,3,1,1,1,0,0,1);Poll(mod);
            Check(Status(mod).Contains("Neighbour") && Status(mod).Contains("HUMAN"),"tutorial neighbour remains labelled while knocking despite null unused field");
            Check(fixture_legacy_reads()==0,"display uses inherited DialogManager and never reads unused _dialogManager");
            fixture_setup(0,3,0,1,1,1,1,1);Poll(mod);
            Check(Status(mod).Contains("HUMAN"),"same verdict remains visible after opening peephole");
            fixture_setup(0,3,0,1,1,0,1,1);Poll(mod);
            Check(Status(mod)=="Waiting for a knock" && (bool)Get(p,"Known")!,"closing dialogue hides panel content without invalidating identity");
            fixture_setup(0,3,0,1,1,1,1,1);Poll(mod);
            Check(Status(mod).Contains("HUMAN"),"reopening dialogue recovers label");
            fixture_setup(0,3,0,0,0,0,0,1);Poll(mod);
            Check(Status(mod).Contains("HUMAN") && (bool)Get(p,"Known")!,"temporarily absent dialogue manager preserves known encounter");
            fixture_setup(0,3,0,1,0,0,0,1);Poll(mod);
            Check(Status(mod).Contains("HUMAN"),"temporarily absent view preserves known encounter");
            fixture_setup(0,3,0,1,1,1,0,1);Poll(mod);
            Check(Status(mod).Contains("HUMAN"),"view awaiting character assignment preserves verdict");
            fixture_setup(0,3,0,1,1,1,2,1);Poll(mod);
            Check(Status(mod)=="Waiting for a knock","another character's dialogue cannot display this guest's verdict");
            fixture_setup(0,3,1,1,1,0,0,0);Poll(mod);
            Check(Status(mod)=="Checking door state..." && (bool)Get(p,"Known")!,"transient context error retains reservation and requests retry");
            int errors=MelonLogger.Lines.FindAll(x=>x.Contains("ERROR")).Count;Poll(mod);
            Check(MelonLogger.Lines.FindAll(x=>x.Contains("ERROR")).Count==errors,"repeated transient failures do not flood log");
            fixture_setup(0,3,1,1,1,0,0,1);Poll(mod);
            Check(Status(mod).Contains("HUMAN"),"polling resumes after context returns instead of permanently disabling guest");
            mod.OnSceneWasUnloaded(1,"TutorialOverlay");Poll(mod);
            Check(Status(mod).Contains("HUMAN"),"scene transition does not permanently disable door polling");
            fixture_setup(1,3,0,1,1,0,0,1);Poll(mod);
            Check(Get(mod,"pending")==null && Status(mod)=="Waiting for a knock","skipping tutorial clears old day-zero encounter");
            var guests=new[]{(1,4,"Esenin",false),(1,5,"Anxiety",true),(1,66,"Experienced",false),(2,37,"Mother",true),(2,71,"Leper",true),(2,43,"Dude",false)};
            foreach(var g in guests)
            {
                Encounter(mod,g.Item1,g.Item2,g.Item3,g.Item4);
                fixture_setup(g.Item1,g.Item2,1,1,1,0,0,1);Poll(mod);
                Check(Status(mod).Contains(g.Item3) && Status(mod).Contains(g.Item4?"VISITOR":"HUMAN"),"recorded encounter stays visible: "+g.Item3);
            }
            fixture_setup(2,74,1,1,1,0,0,1);Poll(mod);
            Check(Get(mod,"pending")==null,"character change clears stale verdict");
            Console.WriteLine(checks+" display lifecycle checks passed.");return 0;
        }
        catch(Exception e) { Console.Error.WriteLine("FAIL "+e.Message);return 1; }
        finally { (Get(mod,"pending") as IDisposable)?.Dispose();Directory.Delete(Folder,true); }
    }
}
