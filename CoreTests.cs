using System;
using System.IO;
using System.Text.Json;
using DoorDetector;

static class Tests
{
    static int count;
    static void Check(bool ok,string label) { if(!ok) throw new Exception(label); count++; Console.WriteLine("PASS "+label); }
    static void Throws(Action action,string label)
    { try { action(); } catch(InvalidDataException) { count++; Console.WriteLine("PASS "+label); return; } throw new Exception(label); }
    static void Main()
    {
        Check(Policy.Supported(24) && Policy.Supported(74),"ordinary randomized characters supported");
        Check(!Policy.Supported(46) && !Policy.Supported(59),"group encounters cannot receive one guessed verdict");
        Check(!Policy.Supported(-1) && !Policy.Supported(75),"unknown character IDs fail closed");
        int rolls=0;
        Check(Policy.Roll(0,3,.25f,.75f,()=>{rolls++;return false;}),"below population minimum forces Visitor");
        Check(!Policy.Roll(3,3,.25f,.75f,()=>{rolls++;return true;}),"above population maximum forces Human");
        Check(rolls==0,"forced cases do not consume random draws");
        Check(Policy.Roll(1,2,.25f,.75f,()=>true) && !Policy.Roll(1,2,.25f,.75f,()=>false),"middle population preserves both random outcomes");
        Check(Policy.Roll(1,4,.25f,.75f,()=>true) && Policy.Roll(3,4,.25f,.75f,()=>true),"threshold equality takes random branch as native game does");
        Throws(()=>Policy.Roll(0,0,0,1,()=>true),"empty population rejected");
        Throws(()=>Policy.Roll(2,1,0,1,()=>true),"impossible population rejected");
        Throws(()=>Policy.Roll(0,1,float.NaN,1,()=>true),"invalid settings rejected");
        Throws(()=>Policy.Roll(0,1,.9f,.2f,()=>true),"reversed thresholds rejected");
        Check(Policy.Fingerprint("{\"b\":\"2\",\"a\":\"1\"}","{}") == Policy.Fingerprint("{\"a\":\"1\",\"b\":\"2\"}","{}"),"save dictionary enumeration order does not change checkpoint identity");
        Check(Policy.Fingerprint("{}","{}")=="" && Policy.Fingerprint("null","null")=="","empty new runs do not share a checkpoint");
        Check(Policy.Fingerprint("{\"a\":\"1\"}","{}")!=Policy.Fingerprint("{\"a\":\"1\"}","{\"r\":\"2\"}"),"reserve-save changes distinguish checkpoints");
        Throws(()=>Policy.Fingerprint("{\"a\":false}","{}"),"unexpected native snapshot data rejected");
        byte random=1; bool identity=false; int originalCalls=0;
        var pin=new IdentityPin(()=>random,v=>random=v,()=>identity,v=>identity=v,true);
        try { originalCalls++; if(random!=0) identity=false; }
        finally { pin.Dispose(); }
        Check(identity && random==1 && originalCalls==1,"admission runs exactly once, cannot reroll assigned identity, restores random flag");
        random=1; identity=true;
        pin=new IdentityPin(()=>random,v=>random=v,()=>identity,v=>identity=v,false);
        pin.Dispose(); pin.AdmissionRejected();
        Check(identity && random==1,"full house rejection restores previous asset state");
        random=1; identity=false;
        pin=new IdentityPin(()=>random,v=>random=v,()=>identity,v=>identity=v,true);
        try { try { throw new IOException("simulated game error"); } finally { pin.Dispose(); } } catch(IOException) {}
        Check(random==1,"game exception still restores random marker");
        random=0; identity=true;
        pin=new IdentityPin(()=>random,v=>random=v,()=>identity,v=>identity=v,false); pin.Dispose(); pin.Dispose();
        Check(random==0 && !identity,"pre-existing disabled marker is restored and cleanup is idempotent");
        string dir=Path.Combine(Path.GetTempPath(),"DoorDetectorTests-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file=Path.Combine(dir,"reservations.json");
            var ledger=new Ledger(file); ledger.Load("checkpoint-A");
            Check(ledger.Reserve("1:24",()=>true),"new encounter gets assigned outcome");
            Check(ledger.Reserve("1:24",()=>throw new Exception("Rerolled")),"repeated door event never rerolls");
            var fresh=new Ledger(file); fresh.Load("checkpoint-A");
            Check(fresh.Reserve("1:24",()=>throw new Exception("Lost reservation")),"pending identity survives process restart and checkpoint reload");
            fresh.Bind("checkpoint-B");
            Check(!fresh.Reserve("1:25",()=>false),"second guest is independent");
            fresh.Load("checkpoint-A");
            Check(!fresh.Current.ContainsKey("1:25") && fresh.Current["1:24"],"later guest does not contaminate an older checkpoint copy");
            fresh.Load("checkpoint-B");
            Check(fresh.Current.ContainsKey("1:25") && !fresh.Current["1:25"],"new checkpoint retains later reservations");
            fresh.Load(""); Check(fresh.Current.Count==0,"new game clears active state");
            Check(!fresh.Reserve("1:24",()=>false),"fresh run can assign opposite outcome for same character");
            fresh.Load(""); Check(fresh.Current.Count==0,"empty checkpoint does not reuse previous new run");
            File.WriteAllText(file,"{\"Version\":999,\"Snapshots\":{}}");
            Throws(()=>new Ledger(file),"unknown sidecar versions rejected");
            string blocker=Path.Combine(dir,"not-a-directory"); File.WriteAllText(blocker,"x");
            var failed=new Ledger(Path.Combine(blocker,"reservations.json")); failed.Load("checkpoint-C");
            try { failed.Reserve("1:30",()=>true); throw new Exception("Write failure not detected"); }
            catch(IOException) { Check(failed.Current.Count==0,"write failure rolls back newly assigned verdict"); }
            Check(Policy.Key(1,24)!=Policy.Key(2,24),"same character on different days has distinct encounter key");
        }
        finally { Directory.Delete(dir,true); }
        Console.WriteLine($"{count} checks passed.");
    }
}
