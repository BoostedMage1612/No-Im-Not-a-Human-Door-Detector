using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using MelonLoader;
using MelonLoader.NativeUtils;
using MelonLoader.Utils;
using DoorDetector;

[assembly: MelonInfo(typeof(DetectorMod), "Door Detector", "0.1.3", "Jimmy / Codex")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETCoreApp,Version=v6.0")]

namespace DoorDetector;

public sealed class DetectorMod:MelonMod
{
    public const string GameHash="DE2C120D90E757916D74C298F264040DF0CDA1DDAA8C412E438AEEE72EB4DC3F";
    public const string MetadataHash="1773FD058179E577578E08E9B94155B53B029927967F6B189727D709435923C7";
    [DllImport("kernel32",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string module);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void ArrivalCall(IntPtr self,IntPtr character,IntPtr condition,IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void AdmissionCall(IntPtr self,IntPtr character,byte exile,IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void CharacterCall(IntPtr self,IntPtr character,IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void SimpleCall(IntPtr self,IntPtr method);
    NativeHook<ArrivalCall>? arrival;
    NativeHook<AdmissionCall>? admission;
    NativeHook<CharacterCall>? refuse;
    readonly List<Delegate> callbacks=new();
    readonly List<Action> detach=new();
    readonly List<Action> attach=new();
    Ledger? ledger;
    Handle? save;
    Pending? pending;
    bool ready,visible=true,guiFailed,persistenceReady=true;
    bool firstUpdate=true,firstGui=true;
    string status="Starting...";
    string folder="";
    DateTime nextPoll;
    IntPtr module;
    sealed class Pending:IDisposable
    {
        public Handle Room,Character;
        public int Type,Day;
        public bool Random,Visitor,Known,Assigned,PollWarning,DisplayLogged;
        public string Why="Unknown",Name="Guest";
        public Pending(IntPtr room,IntPtr character) { Room=new(room); Character=new(character); }
        public void Dispose() { Room.Dispose(); Character.Dispose(); }
    }
    public override void OnInitializeMelon()
    {
        try
        {
            folder=Path.Combine(MelonEnvironment.GameRootDirectory,"UserData","DoorDetector");
            Directory.CreateDirectory(folder);
            if(!OperatingSystem.IsWindows() || IntPtr.Size!=8) throw new NotSupportedException("Windows x64 only.");
            string root=MelonEnvironment.GameRootDirectory;
            string game=Path.Combine(root,"GameAssembly.dll");
            string metadata=Path.Combine(root,"NoImNotAHuman_Data","il2cpp_data","Metadata","global-metadata.dat");
            if(FileHash(game)!=GameHash || FileHash(metadata)!=MetadataHash)
                throw new NotSupportedException("Different game build. Expected Steam build 23967901. No game hooks installed.");
            module=N.Required(GetModuleHandle("GameAssembly.dll"));
            Log("STARTUP 0.1.3: verifying pointer-sized native object handles.");
            N.VerifyHandleRoundTrip();
            Log("STARTUP: 64-bit native handle creation, lookup and release passed.");
            ledger=new Ledger(Path.Combine(folder,"reservations.json"));
            // All code signatures are checked before any hooks are attached.
            arrival=Prepare(0x601FD0,"48895c241048896c2418488974242057",new ArrivalCall(OnArrival));
            admission=Prepare(0x695520,"48895c241048896c2418488974242041",new AdmissionCall(OnAdmission));
            refuse=Prepare(0x6967D0,"48895c2408574883ec20803d0ca63104",new CharacterCall(OnRefuse));
            PrepareSimple(0x67CD40,"40534883ec20803d5d3e330400488bd9",BeforeLoad,true);
            PrepareSimple(0x67CC70,"40534883ec30803d313f330400488bd9",BeforeClear,true);
            PrepareSimple(0x67D6D0,"48895c2410488974241848894c240857",AfterSnapshot,false);
            PrepareSimple(0x67DB10,"48895c2410488974241848894c240857",AfterSnapshot,false);
            PrepareSimple(0x67D680,"40534883ec20803d0b35330400488bd9",AfterSnapshot,false);
            foreach(var action in attach) action();
            ready=true; status="Waiting for a knock";
            Log("READY: exact game hashes and 8 native hook signatures verified. Random identities are reserved at the door. F8 toggles overlay.");
        }
        catch(Exception e)
        {
            foreach(var action in detach.AsEnumerable().Reverse()) { try{action();}catch{} }
            ready=false; status="UNKNOWN - detector unavailable"; Fail("Startup",e);
        }
    }
    static string FileHash(string path) { using var stream=File.OpenRead(path); using var sha=SHA256.Create(); return Convert.ToHexString(sha.ComputeHash(stream)); }
    NativeHook<T> Prepare<T>(int rva,string hex,T callback) where T:Delegate
    {
        var target=IntPtr.Add(module,rva);
        byte[] actual=new byte[hex.Length/2]; Marshal.Copy(target,actual,0,actual.Length);
        if(!actual.SequenceEqual(Convert.FromHexString(hex))) throw new InvalidOperationException("Conflicting hook at "+rva.ToString("X")+". Close the game and remove other character-status mods.");
        callbacks.Add(callback);
        var hook=new NativeHook<T>(target,Marshal.GetFunctionPointerForDelegate(callback));
        attach.Add(hook.Attach); detach.Add(hook.Detach); return hook;
    }
    void PrepareSimple(int rva,string hex,Action<IntPtr> work,bool before)
    {
        NativeHook<SimpleCall>? hook=null;
        SimpleCall callback=(self,method)=>
        {
            if(before) SafeSnapshotWork(work,self);
            hook!.Trampoline(self,method);
            if(!before) SafeSnapshotWork(work,self);
        };
        hook=Prepare(rva,hex,callback);
    }
    void SafeSnapshotWork(Action<IntPtr> work,IntPtr self)
    {
        try { work(self); }
        catch(Exception e) { persistenceReady=false; Invalidate("Save state unavailable"); Fail("Save synchronization",e); }
    }
    void BeforeLoad(IntPtr handler)
    {
        ClearPending();
        var data=N.Read<IntPtr>(handler,"<Data>k__BackingField");
        SetSave(data); ledger!.Load(Snapshot(data)); persistenceReady=true;
        status="Waiting for a knock"; Log("LOAD: reservation checkpoint restored.");
    }
    void BeforeClear(IntPtr handler)
    {
        ClearPending(); save?.Dispose(); save=null; ledger!.Load("");
        persistenceReady=true; status="Waiting for a knock";
        Log("NEW RUN: cleared active reservation state.");
    }
    void SetSave(IntPtr obj)
    {
        N.Required(obj);
        if(save?.Value==obj) return;
        save?.Dispose(); save=new Handle(obj);
    }
    static string Snapshot(IntPtr obj) => Policy.Fingerprint(
        N.Json(N.Read<IntPtr>(obj,"_jsonValues")),N.Json(N.Read<IntPtr>(obj,"_jsonValuesReserve")));
    void AfterSnapshot(IntPtr data)
    {
        SetSave(data);
        // After an error, do not overwrite a checkpoint with an untrusted ledger.
        if(!persistenceReady) return;
        ledger!.Bind(Snapshot(data));
    }
    static IntPtr Manager(Pending p) => N.Read<IntPtr>(p.Room.Value,"_charactersManager");
    static IntPtr SaveData(IntPtr manager) => N.Read<IntPtr>(manager,"_saveData");
    static int[] Inside(IntPtr manager) => N.IntList(N.Read<IntPtr>(SaveData(manager),"<CharactersInside>k__BackingField"));
    static int Day(IntPtr room) => N.Get<int>(N.Read<IntPtr>(room,"DayNightController"),"get_Day");
    void OnArrival(IntPtr self,IntPtr character,IntPtr condition,IntPtr method)
    {
        arrival!.Trampoline(self,character,condition,method);
        if(!ready) return;
        try
        {
            ClearPending(); pending=new Pending(self,N.Required(character)); var p=pending;
            p.Type=N.Read<int>(character,"_characterType"); p.Name=Names.For(p.Type); p.Day=Day(self);
            p.Random=N.Read<byte>(character,"_isStatusRandomlyGenerated")!=0;
            if(!Policy.Supported(p.Type)) { p.Why="Special or group encounter"; UpdateStatus(); return; }
            if(p.Random)
            {
                if(!persistenceReady) throw new InvalidOperationException("Reservation persistence is unavailable.");
                p.Visitor=ledger!.Reserve(Policy.Key(p.Day,p.Type),()=>Roll(p));
                p.Assigned=true; p.Why="Assigned at the door";
            }
            else { p.Visitor=N.Read<byte>(character,"_isImposter")!=0; p.Why="Game status"; }
            p.Known=true; UpdateStatus();
            Log($"DOOR: day={p.Day} type={p.Type} name={p.Name} verdict={(p.Visitor?"VISITOR":"HUMAN")} random={p.Random}.");
        }
        catch(Exception e) { Invalidate("Status unavailable"); Fail("Door arrival",e); }
    }
    bool Roll(Pending p)
    {
        var manager=Manager(p); var inside=Inside(manager);
        if(inside.Contains(p.Type)) throw new InvalidOperationException("Door character is already inside.");
        int visitors=inside.Count(type=>N.Read<byte>(N.Guest(manager,type),"_isImposter")!=0);
        // In this build LetIn adds the incoming guest before its inlined population check.
        if(N.Read<byte>(p.Character.Value,"_isImposter")!=0) visitors++;
        var settings=N.Read<IntPtr>(manager,"_randomGenerationSettings");
        return Policy.Roll(visitors,inside.Length+1,N.Read<float>(settings,"_minImpostersPercent"),
            N.Read<float>(settings,"_maxImpostersPercent"),()=>RandomNumberGenerator.GetInt32(2)==0);
    }
    void OnAdmission(IntPtr self,IntPtr character,byte exile,IntPtr method)
    {
        var p=pending;
        bool matched=false;
        IdentityPin? pin=null;
        try
        {
            matched=ready && p!=null && (p.Known || p.Assigned) && p.Character.Value==character && Manager(p)==self;
            if(matched && p!.Random)
            {
                if(Day(p.Room.Value)!=p.Day || Inside(self).Contains(p.Type)) throw new InvalidOperationException("Encounter changed before admission.");
                // Disable the INLINED roll, not just the unused GenerateStatus method.
                pin=new IdentityPin(()=>N.Read<byte>(character,"_isStatusRandomlyGenerated"),
                    value=>N.Write(character,"_isStatusRandomlyGenerated",value),
                    ()=>N.Read<byte>(character,"_isImposter")!=0,
                    value=>N.Write(character,"_isImposter",(byte)(value?1:0)),p.Visitor);
            }
        }
        catch(Exception e)
        {
            pin?.AdmissionRejected(); pin=null;
            matched=false; Invalidate("Encounter changed"); Fail("Admission preparation",e);
        }
        try { admission!.Trampoline(self,character,exile,method); }
        finally { pin?.Dispose(); }
        if(!matched || p==null) return;
        try
        {
            if(!Inside(self).Contains(p.Type))
            {
                pin?.AdmissionRejected();
                Log("ADMISSION DEFERRED: room unavailable; retained door reservation."); return;
            }
            bool actual=N.Read<byte>(character,"_isImposter")!=0;
            bool saved=N.SavedBool(N.Read<IntPtr>(SaveData(self),"<AreCharactersImposters>k__BackingField"),p.Type);
            if(actual!=p.Visitor || saved!=p.Visitor)
                throw new InvalidOperationException("Door/admission mismatch. Stop using this test build and send diagnostics.");
            Log($"VERIFIED ADMISSION: day={p.Day} type={p.Type} door={(p.Visitor?"VISITOR":"HUMAN")} runtime={actual} saved={saved}; random flag restored.");
            if(ReferenceEquals(p,pending)) { ClearPending(); status="Waiting for a knock"; }
        }
        catch(Exception e) { Invalidate("Admission check failed"); Fail("Admission verification",e); }
    }
    void OnRefuse(IntPtr self,IntPtr character,IntPtr method)
    {
        refuse!.Trampoline(self,character,method);
        try { if(pending?.Character.Value==character) { ClearPending(); status="Waiting for a knock"; } }
        catch(Exception e) { Fail("Refusal",e); }
    }
    void ClearPending() { pending?.Dispose(); pending=null; }
    void Invalidate(string why) { if(pending!=null) { pending.Known=false; pending.Why=why; } status="UNKNOWN - "+why; }
    void UpdateStatus()
    {
        var p=pending;
        status=p==null?"Waiting for a knock":p.Name+"\n"+(p.Known?(p.Visitor?"VISITOR":"HUMAN"):"UNKNOWN")+" - "+p.Why;
    }
    public override void OnUpdate()
    {
        try
        {
            bool trace=firstUpdate; firstUpdate=false;
            if(trace) Log("CHECKPOINT: first input update entered.");
            if(N.KeyDown(289)) visible=!visible;
            if(trace) Log("CHECKPOINT: first input query passed.");
            if(N.KeyDown(290)) Log("MANUAL DIAGNOSTIC: ready="+ready+" persistence="+persistenceReady+" status="+status.Replace('\n',' '));
            if(!ready || pending==null || DateTime.UtcNow<nextPoll) return;
            nextPoll=DateTime.UtcNow.AddMilliseconds(250);
            var p=pending;
            if(Day(p.Room.Value)!=p.Day || N.Read<int>(p.Room.Value,"_currentCharacter")!=p.Type)
            { ClearPending(); status="Waiting for a knock"; return; }
            bool? showing=N.IsDoorVisible(p.Room.Value,p.Character.Value);
            // Missing optional UI references during tutorial/scene transitions
            // must not erase a known identity or permanently stop polling.
            if(showing!=false)
            {
                UpdateStatus();
                if(!p.DisplayLogged)
                {
                    p.DisplayLogged=true;
                    Log($"DISPLAY: day={p.Day} type={p.Type} verdict={(p.Known?(p.Visitor?"VISITOR":"HUMAN"):"UNKNOWN")} overlayVisible={visible}.");
                }
            }
            else status="Waiting for a knock";
            if(p.PollWarning) { p.PollWarning=false; Log("DISPLAY: door-state polling recovered."); }
        }
        catch(Exception e)
        {
            // A presentation/context query is separate from identity validity.
            // Retry next tick, retaining any reserved identity for admission.
            status="Checking door state...";
            if(pending!=null)
            {
                if(!pending.PollWarning) Fail("Display state (will retry)",e);
                pending.PollWarning=true;
            }
            else Fail("Update",e);
        }
    }
    public override void OnGUI()
    {
        if(!visible || guiFailed) return;
        try
        {
            bool trace=firstGui; firstGui=false;
            if(trace) Log("CHECKPOINT: first overlay draw entered.");
            N.Draw("DOOR DETECTOR  |  F8 hide\n"+status);
            if(trace) Log("CHECKPOINT: first overlay draw completed.");
        }
        catch(Exception e) { guiFailed=true; Fail("Overlay",e); }
    }
    public override void OnSceneWasUnloaded(int buildIndex,string sceneName)
    { nextPoll=DateTime.UtcNow.AddMilliseconds(250); if(ready) status="Waiting for a knock"; }
    public override void OnDeinitializeMelon()
    {
        ready=false;
        foreach(var action in detach.AsEnumerable().Reverse()) { try{action();}catch{} }
        ClearPending(); save?.Dispose();
    }
    void Log(string text)
    {
        MelonLogger.Msg(text);
        try { File.AppendAllText(Path.Combine(folder,"DoorDetector.log"),DateTime.UtcNow.ToString("O")+" "+text+Environment.NewLine); } catch{}
    }
    void Fail(string where,Exception e)
    {
        string message=where+": "+e.GetType().Name+": "+e.Message;
        MelonLogger.Error(message); Log("ERROR "+message);
    }
}
internal static class Names
{
    static readonly string[] names="Sanya|TV|Courier|Neighbour|Esenin|Anxiety|Daughter|Cold|Fan|Prophet|Super Fake|Widow|Scammer|Doc|Gas Mask|Wolfhound|Hunter|Not True|Greatmother|Sexy|Phone|Anger|Luka|Former FEMA|Foreigner|Alkonost|Blind|Marauder|Nun|Taxi Driver|Firefighter|Fugitive|Teacher|Edgar|Raskolnikov|Gravedigger|Provocateur|Mother|FEMA Wife|Vigilante|Theorist|Buddy|Fortune Teller|Dude|Best Son|Big Lebowski|Couple|Fat Man|Wheelchair|Cultist One|Cultist Two|Cultist Three|Cultist Priest|Ballerina|Intruder|Mushroom Eater|Miner|Sirin|Empty|Twins|Player|Jacob|Static Boy|Tourist|Unstable|Rocker|Experienced|Jacket|Tough|Bald|Nervous|Leper|Fairytaller|Funny Guy|Alt".Split('|');
    internal static string For(int type) => type>=0 && type<names.Length?names[type]:"Guest "+type;
}
