using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text.Json;
using CustomDancePlayer;

var output=Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var results=new List<object>();
string Make(string name, params (string name,string text)[] entries)
{
    var file=Path.Combine(output,name+".me");
    using(var zip=ZipFile.Open(file,ZipArchiveMode.Create))
        foreach(var entry in entries)
            using(var writer=new StreamWriter(zip.CreateEntry(entry.name).Open()))writer.Write(entry.text);
    return file;
}
void Check(string name,Action test)
{
    test();results.Add(new{name,pass=true});
}
void Reject(string name,string file)
{
    Check(name,()=>{
        try{OfficialDancePackage.Extract(file,Path.Combine(output,"cache"));}
        catch(InvalidDataException){return;}
        throw new Exception("Unsafe archive accepted: "+name);
    });
}
var valid=Make("valid",("dance_meta.json","{\"title\":\"Test\"}"),("nested/test.bundle","bundle"));
Check("valid-nested-extraction",()=>{var folder=OfficialDancePackage.Extract(valid,Path.Combine(output,"cache"));if(File.ReadAllText(Path.Combine(folder,"nested/test.bundle"))!="bundle")throw new Exception("Missing content");});
Check("metadata",()=>{if((string)OfficialDancePackage.ReadMetadata(valid)["title"]!="Test")throw new Exception("Missing title");});
Check("object-mod-excluded",()=>{if(OfficialDancePackage.ReadMetadata(Make("object",("mod.json","{}")))!=null)throw new Exception("Object accepted");});
Reject("parent-traversal",Make("traversal",("../escaped.txt","x")));
Reject("backslash-traversal",Make("backslash",("..\\escaped.txt","x")));
Reject("absolute-path",Make("absolute",("C:/escaped.txt","x")));
Reject("alternate-stream",Make("ads",("test.bundle:stream","x")));
var entries=new (string,string)[513];for(int i=0;i<entries.Length;i++)entries[i]=("entry"+i,"x");
Reject("entry-count-limit",Make("many",entries));
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("Package checks passed: "+results.Count);
