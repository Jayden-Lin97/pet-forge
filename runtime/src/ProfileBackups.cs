using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace MaohaiziPet {
public static class ProfileBackups {
    public static void BeforeSave(string file,DateTime date){
        if(!File.Exists(file))return;
        string folder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file)),"backups");Directory.CreateDirectory(folder);
        string target=Path.Combine(folder,"settings-"+date.ToString("yyyy-MM-dd")+".xml");
        if(!File.Exists(target)){
            // Never replace the last known-good snapshot with malformed XML.
            using(var f=File.OpenRead(file))new XmlSerializer(typeof(PetSettings)).Deserialize(f);
            File.Copy(file,target,false);
        }
        foreach(string old in Directory.GetFiles(folder,"settings-????-??-??.xml").OrderByDescending(p=>p,StringComparer.Ordinal).Skip(14)){
            string full=Path.GetFullPath(old);if(string.Equals(Path.GetDirectoryName(full),Path.GetFullPath(folder),StringComparison.OrdinalIgnoreCase))File.Delete(full);
        }
    }
    public static PetSettings Recover(string root){
        string folder=Path.Combine(root,"backups");if(!Directory.Exists(folder))return null;
        foreach(string file in Directory.GetFiles(folder,"settings-????-??-??.xml").OrderByDescending(p=>p,StringComparer.Ordinal))try{using(var f=File.OpenRead(file))return (PetSettings)new XmlSerializer(typeof(PetSettings)).Deserialize(f);}catch{}
        return null;
    }
}
}
