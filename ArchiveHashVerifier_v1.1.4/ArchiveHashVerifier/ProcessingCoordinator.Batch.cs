using System.Text;
namespace ArchiveHashVerifier;
public sealed partial class ProcessingCoordinator
{
 private async Task<OperationSummary> GenerateBatchAsync(IReadOnlyCollection<string> src, GenerationOptions opt, Func<string,ArtifactKind,CancellationToken,Task<bool>> ask, IProgress<FileReadProgress>? progress, Action<string>? log, CancellationToken ct, IProgress<OperationPhase>? phase)
 {
  var sum=new OperationSummary(); var files=src.Select(Path.GetFullPath).Order(StringComparer.OrdinalIgnoreCase).ToArray(); var dirs=files.Select(x=>Path.GetDirectoryName(x)!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  if(dirs.Length!=1){sum.Results.Add(new("","一括生成",ResultState.Error,"一括生成は、すべての対象ファイルが同じフォルダーに存在する場合のみ実行できます。"));return sum;}
  string dir=dirs[0]; var kinds=new HashSet<HashKind>(opt.Hashes); bool sign=opt.OpenPgpAscii||opt.OpenPgpBinary;if(sign)kinds.Add(HashKind.Sha512);
  var req=opt.Hashes.Select(k=>new R(HashCatalog.Get(k).ArtifactKind,HashCatalog.Get(k).DisplayName,BatchArtifacts.PathFor(dir,k),k,false)).ToList();
  if(sign){req.Add(new(ArtifactKind.Manifest,"OpenPGP manifest",Path.Combine(dir,BatchArtifacts.Manifest),null,false));if(opt.OpenPgpAscii)req.Add(new(ArtifactKind.OpenPgpAscii,"OpenPGP ASCII (.asc)",Path.Combine(dir,BatchArtifacts.Manifest+".asc"),null,true));if(opt.OpenPgpBinary)req.Add(new(ArtifactKind.OpenPgpBinary,"OpenPGP Binary (.sig)",Path.Combine(dir,BatchArtifacts.Manifest+".sig"),null,false));}
  if(files.Any(f=>req.Any(r=>f.Equals(r.Path,StringComparison.OrdinalIgnoreCase)))){sum.Results.Add(new(dir,"一括生成",ResultState.Error,"出力予定パスが元ファイルと衝突するため中止しました。"));return sum;}
  foreach(var r in req.Where(r=>File.Exists(r.Path)))if(!await ask(r.Path,r.Kind,ct)){sum.Results.Add(new(dir,r.Name,ResultState.Skipped,"既存成果物を上書きしませんでした。一括処理は開始しません。"));return sum;}
  if(sign&&(!gpg.IsAvailable||string.IsNullOrWhiteSpace(opt.SigningFingerprint))){sum.Results.Add(new(dir,"OpenPGP manifest",ResultState.Error,!gpg.IsAvailable?"GPGがインストールされていません。":"有効なGPG署名鍵が選択されていません。"));return sum;}
  var snap=files.ToDictionary(f=>f,SourceSnapshot.Capture,StringComparer.OrdinalIgnoreCase);var entries=new List<BatchManifestEntry>();var tmp=new List<(R r,string t,GpgVerifyResult? g)>(); long total=SafeTotalLength(files),before=0;
  try{
   foreach(var f in files){long len=SafeLength(f);await using var stream=new FileStream(f,FileMode.Open,FileAccess.Read,FileShare.Read,1024*1024,FileOptions.Asynchronous|FileOptions.SequentialScan);var values=await HashService.ComputeAsync(stream,kinds,f,before,total,progress,ct);entries.Add(new(Path.GetFileName(f),len,values));before+=len;}
   foreach(var r in req.Where(r=>r.KindHash is not null)){string t=CreateTempPath(r.Path);string text=string.Concat(entries.OrderBy(e=>e.Name,StringComparer.Ordinal).Select(e=>$"{e.Hashes[r.KindHash!.Value]}  {e.Name}\r\n"));await File.WriteAllTextAsync(t,text,new UTF8Encoding(false),ct);tmp.Add((r,t,null));}
   var man=req.FirstOrDefault(r=>r.Kind==ArtifactKind.Manifest);if(man is not null){string mt=CreateTempPath(man.Path);await File.WriteAllTextAsync(mt,BatchArtifacts.ManifestText(entries),new UTF8Encoding(false),ct);tmp.Add((man,mt,null));foreach(var r in req.Where(r=>r.Kind is ArtifactKind.OpenPgpAscii or ArtifactKind.OpenPgpBinary)){string t=CreateTempPath(r.Path);var g=await gpg.CreateDetachedSignatureAsync(mt,t,r.Ascii,opt.SigningFingerprint!,opt.AutoVerifyGpgSignatures,ct,phase);if(g.State is not (GpgVerifyState.Valid or GpgVerifyState.CreatedUnverified)){TryDelete(t);throw new InvalidOperationException(g.Message);}tmp.Add((r,t,g));}}
   if(snap.Any(x=>!x.Value.Matches(x.Key)))throw new InvalidOperationException("処理中に元ファイルが変更されたため成果物を確定しませんでした。");
   ct.ThrowIfCancellationRequested(); var cleanupWarnings=BatchTransaction.Commit(tmp.Select(x => (x.t, x.r.Path)).ToArray()); foreach(var x in tmp){sum.Results.Add(new(dir,x.r.Name,ResultState.Ok,x.g?.Message??"生成成功",x.g?.SignerUid,x.g?.SigningFingerprint));log?.Invoke($"OK [{x.r.Name}] {x.r.Path}");} foreach(string warning in cleanupWarnings){sum.Results.Add(new(dir,"backup cleanup",ResultState.Skipped,"成果物の確定は成功したがbackup cleanupに失敗: "+warning));log?.Invoke("WARNING: "+warning);}
  }catch(OperationCanceledException){sum.Results.Add(new(dir,"一括生成",ResultState.Cancelled,"キャンセル"));}catch(Exception ex){sum.Results.Add(new(dir,"一括生成",ResultState.Error,ex.Message));}finally{foreach(var x in tmp)TryDelete(x.t);}return sum;
 }
 private sealed record R(ArtifactKind Kind,string Name,string Path,HashKind? KindHash,bool Ascii);
}
