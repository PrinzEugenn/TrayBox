using System;
using System.Runtime.InteropServices;
using System.Text;

// Query identity without opening or reading another process's modules.
internal static class ProcessAccess
{
    [DllImport("kernel32.dll",SetLastError=true)] private static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool QueryFullProcessImageNameW(IntPtr process,uint flags,StringBuilder name,ref uint size);
    [DllImport("kernel32.dll")] private static extern bool GetProcessTimes(IntPtr process,out long created,out long exited,out long kernel,out long user);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("advapi32.dll",SetLastError=true)] private static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
    [DllImport("advapi32.dll",SetLastError=true)] private static extern bool GetTokenInformation(IntPtr token,int cls,IntPtr buffer,int length,out int needed);
    [DllImport("advapi32.dll")] private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")] private static extern IntPtr GetSidSubAuthority(IntPtr sid,uint index);
    internal static bool TryRead(uint pid,out string path,out long created,out int integrity,out int error)
    {
        path=null; created=0; integrity=-1; error=0;
        IntPtr process=OpenProcess(0x1000,false,pid);
        if(process==IntPtr.Zero) { error=Marshal.GetLastWin32Error(); return false; }
        try {
            var name=new StringBuilder(32768); uint length=(uint)name.Capacity;
            if(!QueryFullProcessImageNameW(process,0,name,ref length)) { error=Marshal.GetLastWin32Error(); return false; }
            long exited,kernel,user;
            if(!GetProcessTimes(process,out created,out exited,out kernel,out user)) return false;
            path=name.ToString();
            IntPtr token;
            if(OpenProcessToken(process,8,out token)) try {
                int needed; GetTokenInformation(token,25,IntPtr.Zero,0,out needed);
                if(needed>0 && needed<65536) {
                    IntPtr buffer=Marshal.AllocHGlobal(needed);
                    try { if(GetTokenInformation(token,25,buffer,needed,out needed)) {
                        IntPtr sid=Marshal.ReadIntPtr(buffer); byte count=Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                        if(count>0) integrity=Marshal.ReadInt32(GetSidSubAuthority(sid,(uint)(count-1)));
                    } } finally { Marshal.FreeHGlobal(buffer); }
                }
            } finally { CloseHandle(token); }
            return true;
        } finally { CloseHandle(process); }
    }
    internal static string PathFor(uint pid)
    {
        string path; long created; int level,error;
        return TryRead(pid,out path,out created,out level,out error) ? path : null;
    }
}
