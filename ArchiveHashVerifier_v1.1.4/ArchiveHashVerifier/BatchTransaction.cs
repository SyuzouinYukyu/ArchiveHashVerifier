namespace ArchiveHashVerifier;
internal static class BatchTransaction
{
    internal static Func<string,int,string,Exception?>? FaultInjector { get; set; }
    public static IReadOnlyList<string> Commit(IReadOnlyList<(string Temp,string Destination)> items)
    {
        var backups=new List<(string Destination,string Backup)>(); var committed=new List<string>(); int step=0;
        try
        {
            foreach(var x in items.Where(x=>File.Exists(x.Destination))){string b=Path.Combine(Path.GetDirectoryName(x.Destination)!,"."+Path.GetFileName(x.Destination)+".ahvbak-"+Guid.NewGuid().ToString("N"));Throw("backup",++step,x.Destination);File.Move(x.Destination,b);backups.Add((x.Destination,b));}
            foreach(var x in items){if(File.Exists(x.Destination))throw new IOException("確定先が再出現しました: "+x.Destination);Throw("commit",++step,x.Destination);File.Move(x.Temp,x.Destination);committed.Add(x.Destination);}
        }
        catch(Exception ex)
        {
            var failures=new List<string>();
            foreach(string d in committed.AsEnumerable().Reverse())try{Throw("rollback-delete",++step,d);if(File.Exists(d))File.Delete(d);}catch(Exception e){failures.Add(d+": "+e.Message);}
            foreach(var x in backups.AsEnumerable().Reverse())try{Throw("rollback-restore",++step,x.Destination);if(File.Exists(x.Backup)&&!File.Exists(x.Destination))File.Move(x.Backup,x.Destination);}catch(Exception e){failures.Add(x.Backup+" -> "+x.Destination+": "+e.Message);}
            if(failures.Count>0)throw new IOException("重大: ロールバックに失敗しました。backupを保持: "+string.Join(" | ",failures),ex);
            throw new IOException("一括成果物の確定に失敗しました。処理前状態へ復旧しました。 "+ex.Message,ex);
        }
        var warnings=new List<string>(); foreach(var x in backups)try{Throw("cleanup",++step,x.Backup);File.Delete(x.Backup);}catch(Exception e){warnings.Add(x.Backup+": "+e.Message);} return warnings;
    }
    private static void Throw(string stage,int step,string path){Exception? ex=FaultInjector?.Invoke(stage,step,path);if(ex is not null)throw ex;}
}
