Set fso = CreateObject("Scripting.FileSystemObject")
dirPath = fso.GetParentFolderName(WScript.ScriptFullName)
target = dirPath & "\PLCPak_v1.8.0.exe"
If fso.FileExists(target) Then
    CreateObject("WScript.Shell").Run """" & target & """", 1, False
Else
    ps1 = dirPath & "\PLCPak.ps1"
    If fso.FileExists(ps1) Then
        CreateObject("WScript.Shell").Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -File """ & ps1 & """", 1, False
    Else
        MsgBox "找不到 PLCPak_v1.8.0.exe 或 PLCPak.ps1", 16, "PLCPak"
    End If
End If