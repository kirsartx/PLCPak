Option Explicit
Dim fso, shell, dirPath, f, target
Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")
dirPath = fso.GetParentFolderName(WScript.ScriptFullName)
target = dirPath & "\PLCPak_v1.7.4.exe"
If fso.FileExists(target) Then
    shell.Run Chr(34) & target & Chr(34), 1, False
    WScript.Quit 0
End If
For Each f In fso.GetFolder(dirPath).Files
    If LCase(fso.GetExtensionName(f.Name)) = "exe" Then
        shell.Run Chr(34) & f.Path & Chr(34), 1, False
        WScript.Quit 0
    End If
Next
MsgBox "No .exe found in tool folder.", 16, "Launch failed"
WScript.Quit 1