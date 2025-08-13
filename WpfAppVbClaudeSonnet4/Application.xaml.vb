Imports System.Security.AccessControl
Imports System.Security.Principal
Imports System.Threading

Public Class Application

  Private Shared _mutex As Mutex
  Private Shared _mutexName As String = "Global\MonApplicationUniqueWPF_" & Environment.UserName

  Protected Overrides Sub OnStartup(e As StartupEventArgs)
    ' Tentative de création du mutex avec des droits appropriés
    Try
      Dim allowEveryoneRule As New MutexAccessRule(New SecurityIdentifier(WellKnownSidType.WorldSid, Nothing), MutexRights.FullControl, AccessControlType.Allow)

      Dim securitySettings As New MutexSecurity()
      securitySettings.AddAccessRule(allowEveryoneRule)

      Dim mutexWasCreated As Boolean
      _mutex = New Mutex(False, _mutexName, mutexWasCreated, securitySettings)

      ' Vérifier si nous pouvons obtenir le mutex
      If Not _mutex.WaitOne(TimeSpan.Zero, False) Then
        ' Une autre instance est déjà en cours d'exécution
        Dim firstUserName As String = GetFirstInstanceUserName()
        Dim message As String = "L'application est déjà en cours d'exécution."
        If Not String.IsNullOrEmpty(firstUserName) Then
          message &= vbCrLf & "Lancée par : " & firstUserName
        End If

        MessageBox.Show(message, "Instance unique", MessageBoxButton.OK, MessageBoxImage.Information)

        ' Tenter de mettre au premier plan l'instance existante
        BringExistingInstanceToFront()

        ' Fermer cette instance
        Shutdown()
        Return
      End If

    Catch exception As UnauthorizedAccessException
      ' Fallback pour les utilisateurs sans privilèges
      _mutexName = "Local\MonApplicationUniqueWPF_" & Environment.UserName & "_" & Process.GetCurrentProcess().Id.ToString()

      Try
        Dim mutexWasCreated As Boolean
        _mutex = New Mutex(False, _mutexName, mutexWasCreated)

        If Not _mutex.WaitOne(TimeSpan.Zero, False) Then
          Dim firstUserName As String = GetFirstInstanceUserName()
          Dim message As String = "L'application est déjà en cours d'exécution."
          If Not String.IsNullOrEmpty(firstUserName) Then
            message &= vbCrLf & "Lancée par : " & firstUserName
          End If

          MessageBox.Show(message, "Instance unique", MessageBoxButton.OK, MessageBoxImage.Information)
          Shutdown()
          Return
        End If
      Catch innerEx As Exception
        ' En dernier recours, utiliser la détection par processus
        If IsAnotherInstanceRunning() Then
          MessageBox.Show("L'application est déjà en cours d'exécution.", "Instance unique", MessageBoxButton.OK, MessageBoxImage.Information)
          Shutdown()
          Return
        End If
      End Try

    Catch exception As Exception
      ' En cas d'erreur, utiliser la détection par processus
      If IsAnotherInstanceRunning() Then
        Dim firstUserName As String = GetFirstInstanceUserName()
        Dim message As String = "L'application est déjà en cours d'exécution."
        If Not String.IsNullOrEmpty(firstUserName) Then
          message &= vbCrLf & "Lancée par : " & firstUserName
        End If

        MessageBox.Show(message, "Instance unique", MessageBoxButton.OK, MessageBoxImage.Information)
        Shutdown()
        Return
      End If
    End Try

    ' Continuer le démarrage normal de l'application
    MyBase.OnStartup(e)
  End Sub

  Protected Overrides Sub OnExit(e As ExitEventArgs)
    ' Libérer le mutex lors de la fermeture
    If _mutex IsNot Nothing Then
      Try
        _mutex.ReleaseMutex()
        _mutex.Close()
      Catch exception As Exception
        ' Ignorer les erreurs lors de la libération
      End Try
    End If

    MyBase.OnExit(e)
  End Sub

  Private Function IsAnotherInstanceRunning() As Boolean
    Dim currentProcess As Process = Process.GetCurrentProcess()
    Dim processes() As Process = Process.GetProcessesByName(currentProcess.ProcessName)

    For Each process As Process In processes
      Try
        ' Vérifier si c'est un processus différent avec le même nom et le même utilisateur
        If process.Id <> currentProcess.Id AndAlso
           process.ProcessName = currentProcess.ProcessName AndAlso
           GetProcessOwner(process) = Environment.UserName Then
          Return True
        End If
      Catch exception As Exception
        ' Ignorer les erreurs d'accès aux processus
      End Try
    Next

    Return False
  End Function

  Private Function GetFirstInstanceUserName() As String
    Try
      Dim currentProcess As Process = Process.GetCurrentProcess()
      Dim processes() As Process = Process.GetProcessesByName(currentProcess.ProcessName)

      ' Trouver le processus le plus ancien (première instance)
      Dim oldestProcess As Process = Nothing
      Dim oldestStartTime As DateTime = DateTime.MaxValue

      For Each process As Process In processes
        Try
          If process.Id <> currentProcess.Id AndAlso
             Not process.HasExited AndAlso
             process.StartTime < oldestStartTime Then
            oldestStartTime = process.StartTime
            oldestProcess = process
          End If
        Catch exception As Exception
          ' Ignorer les erreurs d'accès aux informations du processus
        End Try
      Next

      If oldestProcess IsNot Nothing Then
        Return GetProcessOwner(oldestProcess)
      End If

    Catch exception As Exception
      ' En cas d'erreur, retourner une chaîne vide
    End Try

    Return String.Empty
  End Function

  Private Function GetProcessOwner(process As Process) As String
    Try
      Dim query As String = String.Format("SELECT * FROM Win32_Process WHERE ProcessId = {0}", process.Id)
      Using searcher As New Management.ManagementObjectSearcher(query)
        Using collection As Management.ManagementObjectCollection = searcher.Get()
          For Each item As Management.ManagementObject In collection
            Dim ownerInfo(1) As String
            item.InvokeMethod("GetOwner", ownerInfo)
            Return ownerInfo(0) ' Nom d'utilisateur
          Next
        End Using
      End Using
    Catch exception As Exception
      Return String.Empty
    End Try

    Return String.Empty
  End Function

  Private Sub BringExistingInstanceToFront()
    Try
      Dim currentProcess As Process = Process.GetCurrentProcess()
      Dim processes() As Process = Process.GetProcessesByName(currentProcess.ProcessName)

      For Each process As Process In processes
        If process.Id <> currentProcess.Id AndAlso
           Not process.HasExited AndAlso
           process.MainWindowHandle <> IntPtr.Zero Then

          ' Restaurer la fenêtre si elle est minimisée
          ShowWindow(process.MainWindowHandle, SW_RESTORE)
          ' Mettre au premier plan
          SetForegroundWindow(process.MainWindowHandle)
          Exit For
        End If
      Next
    Catch exception As Exception
      ' Ignorer les erreurs
    End Try
  End Sub

  ' Imports Win32 API
  Private Declare Auto Function ShowWindow Lib "user32.dll" (hWnd As IntPtr, nCmdShow As Integer) As Boolean
  Private Declare Auto Function SetForegroundWindow Lib "user32.dll" (hWnd As IntPtr) As Boolean
  Private Const SW_RESTORE As Integer = 9

End Class